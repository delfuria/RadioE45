using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Concurrent.Futures;
using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.ExoPlayer.Source;
using AndroidX.Media3.Session;
using Google.Common.Util.Concurrent;
using Java.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Radio;
using System.Runtime.InteropServices;

namespace RadioE45;

// Single source of truth for Android playback: one ExoPlayer + one MediaLibrarySession.
// Android Auto, Bluetooth (AVRCP), the media notification and the lock screen all bind to
// THIS session, so play/pause/stop/next/prev, the browse tree and artwork are consistent.
// Replaces the old two-session, UI-MediaElement-dependent setup (RadioMediaBrowserService +
// AndroidMediaNotificationService).
[Service(
    Name = "com.radioe45.app.RadioPlaybackService",
    Exported = true,
    ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
[IntentFilter(new[]
{
    "androidx.media3.session.MediaLibraryService",
    "android.media.browse.MediaBrowserService",
})]
public sealed class RadioPlaybackService : MediaLibraryService
{
    private const string RootId = "ROOT";

    // Metadata extras key carrying whether the resolved URL is actually the station's HLS stream —
    // read client-side by Media3AudioService.OnMediaItemTransition. The MediaItem's URI itself is NOT
    // mirrored to the controller (Media3 strips it across the session), so this is the only channel
    // for the client to know which stream format is really playing, as opposed to just what the
    // station supports (AzuraStation.HlsEnabled).
    internal const string HlsExtraKey = "radioe45.is_hls";

    private IExoPlayer? _player;
    private MediaLibrarySession? _session;
    private ILogger<RadioPlaybackService>? _logger;
    private PlayerListener? _playerListener;

    // Held on purpose: once handed to the session builder, the callback is referenced only from Java.
    // Without a managed root its peer can be collected while the session is idle, and the next call
    // from a controller (Android Auto binding on connect, for one) lands on a dead peer — a native
    // SIGSEGV rather than a managed exception.
    private LibraryCallback? _libraryCallback;

    // Per-station stream-URL fallback: mediaId → index into the ordered candidate list. Advanced when
    // ExoPlayer errors on the current URL, reset to 0 once a URL plays (see PlayerListener). Concurrent
    // because it's touched from the player thread (errors) and the session callback tasks (resolve).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _streamAttempts = new();

    // Re-opens the stream with a bounded backoff once every candidate URL has failed (a tunnel, a coverage
    // gap longer than the buffer). Cancelled by anything that ends the intent to play — see CancelRecovery.
    private readonly StreamRecoveryBackoff _recovery = new();
    private string? _recoveryMediaId;

    // Set when the backoff ran out: from then on the session shows the error, until the listener acts.
    private bool _recoveryGaveUp;

    // Only registered while a recovery is wanted (see WatchNetwork). Held in a field: Java keeps just a
    // weak reference to the callback.
    private Android.Net.ConnectivityManager.NetworkCallback? _networkCallback;

    private static IServiceProvider? Services => IPlatformApplication.Current?.Services;

    private static IAzuraStationCatalog? Catalog => Services?.GetService<IAzuraStationCatalog>();

    private static IStreamUrlProber? Prober => Services?.GetService<IStreamUrlProber>();

    private static IAppSettingsRepository? SettingsRepo => Services?.GetService<IAppSettingsRepository>();

    // Refreshed on every proactive selection (BuildStationItemProbedAsync — the only place that reads
    // AppSettings, since GetStreamCandidates itself is also called from synchronous paths). A stale
    // read for the rest of that session is fine: the setting rarely changes mid-play.
    private bool _preferHls;

    public override void OnCreate()
    {
        base.OnCreate();

        _current = this;
        _logger = Services?.GetService<ILogger<RadioPlaybackService>>();

        try
        {
            AudioAttributes audioAttributes = new AudioAttributes.Builder()!
                .SetUsage(C.UsageMedia)!
                .SetContentType(C.AudioContentTypeMusic)!
                .Build()!;

            // The stream that actually plays (StreamUrlFallback, see GetStreamCandidates — the
            // first candidate, OnAirStreamUrl, is never populated) is a plain Icecast/MP3 URL, not
            // HLS/DASH: ExoPlayer has no "live edge" concept for it and, left on the default
            // DefaultLoadControl (up to ~50s of buffer), happily buffers tens of seconds ahead of
            // real time on a good connection. That buffered lag is what makes a track change sound
            // like it's repeating the previous song — the audio is just minutes-old relative to
            // what other clients (iOS/macOS) are hearing live. Capping the buffer window keeps
            // ExoPlayer close to the live edge instead of racing ahead to fill a generous default.
            ILoadControl loadControl = new DefaultLoadControl.Builder()!
                .SetBufferDurationsMs(3000, 8000, 1000, 2000)!
                .Build()!;

            // AzuraCast reports listen_url with the scheme it was configured with (often http://),
            // while a reverse proxy in front of it (e.g. Caddy) answers 308 → https://. ExoPlayer's
            // DefaultHttpDataSource refuses cross-protocol redirects by default and fails with
            // "Source error"; every other platform player follows them, so allow it here too.
            DefaultHttpDataSource.Factory httpFactory = new DefaultHttpDataSource.Factory()!
                .SetAllowCrossProtocolRedirects(true)!;
            DefaultMediaSourceFactory mediaSourceFactory =
                new DefaultMediaSourceFactory(new DefaultDataSource.Factory(this, httpFactory));

            _player = new ExoPlayerBuilder(this)!
                .SetMediaSourceFactory(mediaSourceFactory)!
                .SetAudioAttributes(audioAttributes, true)!
                .SetHandleAudioBecomingNoisy(true)!
                .SetWakeMode(C.WakeModeNetwork)!
                .SetLoadControl(loadControl)!
                .Build();

            // Wrap around the station list instead of stopping at its ends. With the default
            // REPEAT_MODE_OFF the player drops SKIP_TO_PREVIOUS from the session actions on the
            // first station and SKIP_TO_NEXT on the last, so Android Auto and the steering-wheel
            // buttons grey out one direction — with two stations that means one of the two is
            // always dead. REPEAT_MODE_ALL makes ExoPlayer wrap on its own and keeps both actions
            // advertised at every position; nothing else has to track indices.
            if (_player is not null)
                _player.RepeatMode = BasePlayer.InterfaceConsts.RepeatModeAll;

            _playerListener = new PlayerListener(this);
            _player?.AddListener(_playerListener);

            _libraryCallback = new LibraryCallback(this);
            MediaLibrarySession.Builder builder = new MediaLibrarySession.Builder(this, new LivePlayer(_player!, CancelRecovery, () => !_recoveryGaveUp), _libraryCallback)!;

            // Tapping the media notification, lock-screen or the Android Auto card should open the app.
            PendingIntent? sessionActivity = BuildSessionActivity();
            if (sessionActivity is not null)
                builder.SetSessionActivity(sessionActivity);

            _session = builder.Build();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "RadioPlaybackService: failed to initialize Media3 session");
        }
    }

    public override MediaLibrarySession? OnGetSessionFromMediaLibraryService(MediaSession.ControllerInfo? controllerInfo) => _session;

    public override MediaSession? OnGetSession(MediaSession.ControllerInfo? controllerInfo) => _session;

    // Closing the app closes playback. Swiping the task off the recents list tears down the player,
    // the session and the foreground service unconditionally — playback does not outlive the UI.
    //
    // This deliberately gives up Android's ability to keep a foreground service alive past task
    // removal, so that all three heads behave the same: iOS force-quit kills the audio session and
    // a Windows MediaElement dies with its window, neither platform being able to do otherwise.
    // Backgrounding (home button, screen off, another app) is untouched and still plays.
    //
    // One exception: with Android Auto connected, the radio belongs to the car as much as to the phone, and
    // ending it there leaves the driver on an empty station list. The close is then deferred until the car
    // is gone (CloseWhenAutoIsGone): same end result, only later.
    public override void OnTaskRemoved(Intent? rootIntent)
    {
        if (IsCarConnected())
        {
            _logger?.LogInformation("RadioPlaybackService: app closed while Android Auto is connected, closing {Delay}s after it leaves", (int)CloseDelayAfterAuto.TotalSeconds);
            BeginCloseWatch();
            return;
        }

        CloseApp();
    }

    private void CloseApp()
    {
        EndCloseWatch();
        CancelRecovery();

        IExoPlayer? player = _player;
        if (player is not null)
        {
            // Stop before StopSelf so audio and the notification go away in the same frame; leaving
            // it to OnDestroy lets the stream run on for as long as the service takes to unwind.
            player.PlayWhenReady = false;
            player.Stop();
            player.ClearMediaItems();
        }

        StopSelf();

        // Android may keep the .NET process cached after the task is swiped away, ready to fast-relaunch
        // it on the next tap — but this app's whole singleton graph (OnAirViewModel included) lives in
        // that process, so a "relaunch" reusing it briefly shows the last screen and stale state instead
        // of a clean start. Killing the process here makes swipe-away a real exit on Android too, matching
        // iOS force-quit and closing the Windows window.
        Android.OS.Process.KillProcess(Android.OS.Process.MyPid());
    }

    // The app was swiped away while Android Auto was connected, so the close is pending. The controllers are
    // polled rather than waited for through session callbacks: the answer that matters is "is the car still
    // there", and a missed callback would leave a closed app playing on the phone for good.
    private static readonly TimeSpan CloseDelayAfterAuto = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CloseWatchInterval = TimeSpan.FromSeconds(3);
    private bool _closeWhenAutoLeaves;
    private int _closeGeneration;
    private DateTime? _autoGoneSince;
    private INowPlayingService? _closeWatchNowPlaying;
    private static RadioPlaybackService? _current;

    private bool IsAutoController(MediaSession.ControllerInfo controller)
        => _session is { } session && (session.IsAutoCompanionController(controller) || session.IsAutomotiveController(controller));

    private bool HasAutoController()
    {
        IList<MediaSession.ControllerInfo>? controllers = _session?.ConnectedControllers;
        return controllers is not null && controllers.Any(IsAutoController);
    }

    // The Android Auto app keeps its media controller bound to the session after the car is gone, so the
    // controller list says nothing about the car. The connection state Android Auto publishes for apps does
    // (androidx.car.app CarConnection): 0 not connected, 1 native Android Automotive, 2 projection. If it
    // cannot be read, fall back to the controllers: the radio may then outlive the car, but never ends under it.
    private bool IsCarConnected()
    {
        try
        {
            using Android.Database.ICursor? cursor = ContentResolver?.Query(
                Android.Net.Uri.Parse("content://androidx.car.app.connection")!, new[] { "CarConnectionState" }, null, null, null);
            if (cursor is not null && cursor.MoveToFirst())
            {
                int column = cursor.GetColumnIndex("CarConnectionState");
                if (column >= 0)
                    return cursor.GetInt(column) != 0;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "RadioPlaybackService: car connection state not readable");
        }

        return HasAutoController();
    }

    private void BeginCloseWatch()
    {
        _closeWhenAutoLeaves = true;
        _autoGoneSince = null;
        int generation = ++_closeGeneration;

        // The app's own now-playing push is gone with its screen, so the song in the car would freeze.
        _closeWatchNowPlaying = Services?.GetService<INowPlayingService>();
        if (_closeWatchNowPlaying is not null)
            _closeWatchNowPlaying.NowPlayingUpdated += OnNowPlayingWhileClosing;
        FollowStationWhileClosing(_player?.CurrentMediaItem?.MediaId);

        new Handler(Looper.MainLooper!).PostDelayed(() => CheckCloseWatch(generation), (long)CloseWatchInterval.TotalMilliseconds);
    }

    private void EndCloseWatch()
    {
        _closeWhenAutoLeaves = false;
        _autoGoneSince = null;
        _closeGeneration++;

        if (_closeWatchNowPlaying is not null)
            _closeWatchNowPlaying.NowPlayingUpdated -= OnNowPlayingWhileClosing;
        _closeWatchNowPlaying = null;
        _followedMediaId = null;
    }

    // The app's own polling follows the station the app chose. With the app gone and the car picking another
    // station, nothing would poll it and the song of the previous station would stay on the session, so the
    // service starts the polling for whatever station is playing (a new start replaces the previous one).
    private string? _followedMediaId;

    private void FollowStationWhileClosing(string? mediaId)
    {
        INowPlayingService? nowPlaying = _closeWatchNowPlaying;
        if (!_closeWhenAutoLeaves || nowPlaying is null || mediaId is null || mediaId == _followedMediaId)
            return;

        AzuraStation? station = Catalog?.Stations.FirstOrDefault(s => s.Id.ToString() == mediaId);
        if (station is null)
            return;

        _followedMediaId = mediaId;
        _ = Task.Run(async () =>
        {
            try
            {
                await nowPlaying.StartPollingAsync(station, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "RadioPlaybackService: could not follow the now playing of station {Id} while closing", mediaId);
            }
        });
    }

    private void CheckCloseWatch(int generation)
    {
        if (generation != _closeGeneration || !_closeWhenAutoLeaves)
            return;

        if (IsCarConnected())
        {
            if (_autoGoneSince is not null)
                _logger?.LogInformation("RadioPlaybackService: Android Auto is back, the close is cancelled until it leaves again");
            _autoGoneSince = null;
        }
        else
        {
            if (_autoGoneSince is null)
            {
                _autoGoneSince = DateTime.UtcNow;
                _logger?.LogInformation("RadioPlaybackService: Android Auto left, closing in {Delay}s unless it comes back", (int)CloseDelayAfterAuto.TotalSeconds);
            }
            else if (DateTime.UtcNow - _autoGoneSince.Value >= CloseDelayAfterAuto)
            {
                _logger?.LogInformation("RadioPlaybackService: Android Auto is gone, closing the app that was swiped away");
                CloseApp();
                return;
            }
        }

        new Handler(Looper.MainLooper!).PostDelayed(() => CheckCloseWatch(generation), (long)CloseWatchInterval.TotalMilliseconds);
    }

    // While the close is pending nothing in the app pushes the song to the session any more (the screen that
    // did is gone), so the service does it: same fields the app sets, same item, no restart of the stream.
    private void OnNowPlayingWhileClosing(object? sender, NowPlayingInfo info)
    {
        new Handler(Looper.MainLooper!).Post(() =>
        {
            IExoPlayer? player = _player;
            MediaItem? current = player?.CurrentMediaItem;
            if (!_closeWhenAutoLeaves || player is null || current is null || string.IsNullOrWhiteSpace(info.Title)
                || current.MediaId != _followedMediaId)
                return;

            if (current.MediaMetadata?.Title?.ToString() == info.Title && current.MediaMetadata?.Artist?.ToString() == info.Artist)
                return;

            MediaMetadata.Builder metadata = new MediaMetadata.Builder()!
                .SetTitle(info.Title)!
                .SetArtist(info.Artist)!
                .SetSubtitle(info.Artist)!
                .SetIsPlayable(Java.Lang.Boolean.True)!
                .SetMediaType((Java.Lang.Integer)MediaMetadata.MediaTypeRadioStation)!;

            if (!string.IsNullOrEmpty(info.ArtworkUrl))
                metadata.SetArtworkUri(Android.Net.Uri.Parse(info.ArtworkUrl));

            player.ReplaceMediaItem(player.CurrentMediaItemIndex, current.BuildUpon()!.SetMediaMetadata(metadata.Build())!.Build()!);
        });
    }

    // MainActivity: the listener opened the app again after swiping it away with the car connected. It is
    // open now, so it must not be closed under their hands when the car leaves.
    internal static void NotifyAppOpened()
    {
        if (_current is { _closeWhenAutoLeaves: true } service)
        {
            service._logger?.LogInformation("RadioPlaybackService: app opened again, the pending close is cancelled");
            service.EndCloseWatch();
        }
    }

    public override void OnDestroy()
    {
        if (ReferenceEquals(_current, this))
            _current = null;

        EndCloseWatch();
        CancelRecovery();

        _session?.Release();
        _session = null;

        if (_player is not null && _playerListener is not null)
            _player.RemoveListener(_playerListener);
        _playerListener = null;

        _player?.Release();
        _player = null;

        // Safe to drop only now: the session that held it from Java is gone.
        _libraryCallback = null;

        base.OnDestroy();
    }

    // PendingIntent to the app's launcher activity, used as the session activity so notification /
    // Android Auto taps bring the UI forward. Immutable is required on Android 12+.
    private PendingIntent? BuildSessionActivity()
    {
        Intent? launch = PackageManager?.GetLaunchIntentForPackage(PackageName!);
        if (launch is null)
            return null;

        launch.AddFlags(ActivityFlags.SingleTop);
        return PendingIntent.GetActivity(this, 0, launch, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
    }

    // ── Browse tree / media item construction ─────────────────────────────────

    private static MediaItem BuildRootItem()
    {
        MediaMetadata metadata = new MediaMetadata.Builder()!
            .SetTitle("RadioE45")!
            .SetIsBrowsable(Java.Lang.Boolean.True)!
            .SetIsPlayable(Java.Lang.Boolean.False)!
            .SetMediaType((Java.Lang.Integer)MediaMetadata.MediaTypeFolderMixed)!
            .Build()!;

        return new MediaItem.Builder()!
            .SetMediaId(RootId)!
            .SetMediaMetadata(metadata)!
            .Build()!;
    }

    private static MediaMetadata BuildStationMetadata(AzuraStation station)
    {
        MediaMetadata.Builder metadata = new MediaMetadata.Builder()!
            .SetTitle(station.Name)!
            .SetArtist(station.Description)!
            .SetSubtitle(station.Description)!
            .SetIsBrowsable(Java.Lang.Boolean.False)!
            .SetIsPlayable(Java.Lang.Boolean.True)!
            .SetMediaType((Java.Lang.Integer)MediaMetadata.MediaTypeRadioStation)!;

        if (!string.IsNullOrEmpty(station.LogoUrl))
            metadata.SetArtworkUri(Android.Net.Uri.Parse(station.LogoUrl));

        return metadata.Build()!;
    }

    private MediaItem BuildStationItem(AzuraStation station, bool playable)
        => BuildStationItem(station, playable, metadataOverride: null);

    // metadataOverride preserves live now-playing metadata sent by a controller (e.g. the phone UI
    // pushing the current song via ReplaceMediaItem). Media3 strips the URI across the session, so
    // OnAddMediaItems must re-resolve it here WITHOUT discarding that metadata.
    private MediaItem BuildStationItem(AzuraStation station, bool playable, MediaMetadata? metadataOverride)
    {
        MediaMetadata metadata = HasTitle(metadataOverride) ? metadataOverride! : BuildStationMetadata(station);

        MediaItem.Builder item = new MediaItem.Builder()!
            .SetMediaId(station.Id.ToString())!;

        if (playable)
        {
            string? url = ResolveStreamUrl(station);
            if (!string.IsNullOrEmpty(url))
            {
                item.SetUri(url);
                item.SetLiveConfiguration(BuildLiveConfiguration());
                metadata = WithHlsFlag(metadata, IsHlsUrl(station, url));
            }
        }

        return item.SetMediaMetadata(metadata)!.Build()!;
    }

    private static bool HasTitle(MediaMetadata? metadata)
        => metadata?.Title is { } title && !string.IsNullOrEmpty(title.ToString());

    private static bool IsHlsUrl(AzuraStation station, string url)
        => !string.IsNullOrEmpty(station.HlsUrl) && url == station.HlsUrl;

    // Re-attached on every rebuild (including the metadataOverride/song-poll path — see
    // BuildStationItemProbedAsync) so the flag can never go stale by being silently dropped when
    // metadata gets replaced for an unrelated reason (a title update, say).
    private static MediaMetadata WithHlsFlag(MediaMetadata metadata, bool isHls)
    {
        Android.OS.Bundle extras = new();
        extras.PutBoolean(HlsExtraKey, isHls);
        return metadata.BuildUpon()!.SetExtras(extras)!.Build()!;
    }

    // Unset, ExoPlayer's HLS default target offset is derived from the segment duration and commonly
    // lands well past 10s — the live audio then trails the AzuraCast now-playing metadata (which comes
    // from a separate, near-real-time API poll) by the same amount, so a track change is heard several
    // seconds after the UI has already announced it: sounds like the previous track "repeating" into
    // the new one. Pinning a low target offset keeps ExoPlayer's small built-in speed-up/slow-down
    // (~3%, inaudible) converging toward the live edge instead of parking wherever the default lands.
    private static MediaItem.LiveConfiguration BuildLiveConfiguration()
        => new MediaItem.LiveConfiguration.Builder()!
            .SetTargetOffsetMs(3000)!
            .Build()!;

    // Proactive resolution: probes every candidate URL and uses the first one that actually
    // answers, instead of waiting for playback to fail (OnStreamError) before trying the next.
    // Falls back to the first candidate if none of them answer — playback is still attempted
    // rather than blocked, and OnStreamError keeps advancing from there on further failures.
    // Only called for the station about to play right now (selection / voice search), not for
    // the rest of the Android Auto queue, so a station tap doesn't wait on every other station.
    private async Task<MediaItem> BuildStationItemProbedAsync(AzuraStation station, MediaMetadata? metadataOverride)
    {
        MediaMetadata metadata = HasTitle(metadataOverride) ? metadataOverride! : BuildStationMetadata(station);

        if (SettingsRepo is { } settingsRepo)
            _preferHls = (await settingsRepo.GetAsync()).PreferHlsStream;

        IReadOnlyList<string> candidates = GetStreamCandidates(station);
        string? winner = candidates.Count > 0 && Prober is { } prober
            ? await prober.ProbeFirstReachableAsync(candidates.ToArray(), CancellationToken.None)
            : null;

        MediaItem.Builder item = new MediaItem.Builder()!
            .SetMediaId(station.Id.ToString())!;

        string? url = winner ?? candidates.FirstOrDefault();
        if (!string.IsNullOrEmpty(url))
        {
            item.SetUri(url);
            item.SetLiveConfiguration(BuildLiveConfiguration());
            metadata = WithHlsFlag(metadata, IsHlsUrl(station, url));
        }

        item.SetMediaMetadata(metadata);

        // Remember which candidate actually won so a later playback error (OnStreamError) resumes
        // the fallback chain from there instead of restarting at candidate 0.
        int wonIndex = 0;
        if (winner is not null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == winner) { wonIndex = i; break; }
            }
        }
        _streamAttempts[station.Id.ToString()] = wonIndex;

        return item.Build()!;
    }

    // ExoPlayer plays HLS and ICY/MP3 streams directly. Default is the direct Icecast/MP3 fallback
    // first: even a low-latency AzuraCast HLS config (short segments) has a structural floor of
    // ~segment_length × segments_in_playlist behind the live edge (standard HLS forbids starting
    // closer than ~3 segments from the end), which the direct stream doesn't have — see the
    // AppSettings.PreferHlsStream doc comment. HLS is offered as an opt-in (Settings) for its other
    // benefits (adaptive bitrate, cleaner reconnects) when the delay is an acceptable trade-off.
    // StreamUrlFallback is built from the DB (https://{UrlBase}{StreamUrl}, e.g.
    // https://radioe45.ddns.net:8060/radio.mp3) and is the reliable PUBLIC url; the API's ListenUrl
    // (StreamUrl) currently returns a LAN address (192.168.1.100) that only resolves inside the
    // station's own network — see the TODO in AzuraStationCatalog.Map, so it stays last either way.
    // Private-host URLs are dropped for public stations, same rule as LiveStreamAudioService
    // (PrivateHostDetector.FilterCandidates); LAN stations keep every candidate.
    private IReadOnlyList<string> GetStreamCandidates(AzuraStation station)
    {
        string?[] ordered = _preferHls
            ? new[] { station.OnAirStreamUrl, station.HlsUrl, station.StreamUrlFallback, station.StreamUrl }
            : new[] { station.OnAirStreamUrl, station.StreamUrlFallback, station.StreamUrl, station.HlsUrl };

        return PrivateHostDetector.FilterCandidates(
            ordered
                .Where(u => !string.IsNullOrEmpty(u))
                .Distinct()
                .ToArray()!,
            station);
    }

    // The URL for the current fallback attempt on this station (clamped to the last candidate).
    private string? ResolveStreamUrl(AzuraStation station)
    {
        IReadOnlyList<string> candidates = GetStreamCandidates(station);
        if (candidates.Count == 0)
            return null;

        int attempt = _streamAttempts.TryGetValue(station.Id.ToString(), out int a) ? a : 0;
        return candidates[Math.Min(attempt, candidates.Count - 1)];
    }

    // ── Stream error fallback ─────────────────────────────────────────────────

    // On a playback error, advance to the next candidate URL for the current station and re-prepare.
    // Runs on the player's application thread (listener callback), so player access is safe here.
    private void OnStreamError()
    {
        IExoPlayer? player = _player;
        if (player is null || player.MediaItemCount == 0)
            return;

        // Paused while the stream was still opening: it failed, but nobody is waiting for it. Trying the next
        // URL would end in player.Play() and undo the pause; the Play that comes later opens it again.
        if (!player.PlayWhenReady)
            return;

        int index = player.CurrentMediaItemIndex;
        MediaItem? current = player.CurrentMediaItem;
        if (index < 0 || current?.MediaId is not { } mediaId)
            return;

        AzuraStation? station = Catalog?.Stations.FirstOrDefault(s => s.Id.ToString() == mediaId);
        if (station is null)
            return;

        int candidateCount = GetStreamCandidates(station).Count;
        int nextAttempt = (_streamAttempts.TryGetValue(mediaId, out int a) ? a : 0) + 1;
        if (nextAttempt >= candidateCount)
        {
            // Every candidate failed — reset so the retry (or a later manual Play) starts from the best URL again.
            _streamAttempts.TryRemove(mediaId, out _);
            _logger?.LogWarning("RadioPlaybackService: all {Count} stream URLs failed for station {Id}", candidateCount, mediaId);
            ScheduleRecovery(mediaId);
            return;
        }

        _streamAttempts[mediaId] = nextAttempt;
        _logger?.LogInformation("RadioPlaybackService: stream error, trying fallback URL #{Attempt} for station {Id}", nextAttempt, mediaId);

        player.ReplaceMediaItem(index, BuildStationItem(station, playable: true, metadataOverride: current.MediaMetadata));
        player.Prepare();
        player.Play();
    }

    // After the last candidate failed (no network at all), try again with a growing delay instead of
    // leaving the radio silent until the listener presses Play. Bounded by StreamRecoveryBackoff and
    // dropped by CancelRecovery, so it never revives a stream the listener paused, stopped or left.
    private void ScheduleRecovery(string mediaId)
    {
        if (!_recovery.TryScheduleNext(out TimeSpan delay, out int generation))
        {
            _logger?.LogWarning("RadioPlaybackService: giving up recovery of station {Id} after {Attempts} attempts", mediaId, _recovery.Attempt);
            _recoveryGaveUp = true;
            NudgeSession();
            return;
        }

        _recoveryMediaId = mediaId;
        WatchNetwork();
        _logger?.LogInformation("RadioPlaybackService: recovery #{Attempt} for station {Id} in {Delay}s", _recovery.Attempt, mediaId, (int)delay.TotalSeconds);
        new Handler(Looper.MainLooper!).PostDelayed(() => RecoverStream(mediaId, generation), (long)delay.TotalMilliseconds);
    }

    // The timer alone leaves the radio silent until the next step (up to 30 s) after the network is back, and
    // for good once the attempts ran out (a long tunnel or garage). While a recovery is wanted, a default
    // network coming back retries at once with a fresh schedule. _recoveryMediaId stays set after giving up,
    // which is what keeps this armed until the listener pauses, stops, changes station or the stream plays.
    private void WatchNetwork()
    {
        if (_networkCallback is not null)
            return;

        try
        {
            if (GetSystemService(ConnectivityService) is not Android.Net.ConnectivityManager manager)
                return;

            _networkCallback = new RecoveryNetworkCallback(this, manager.ActiveNetwork);
            manager.RegisterDefaultNetworkCallback(_networkCallback);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "RadioPlaybackService: could not watch the network, recovery stays timer-only");
            _networkCallback = null;
        }
    }

    private void StopWatchingNetwork()
    {
        Android.Net.ConnectivityManager.NetworkCallback? callback = _networkCallback;
        if (callback is null)
            return;

        _networkCallback = null;
        try
        {
            (GetSystemService(ConnectivityService) as Android.Net.ConnectivityManager)?.UnregisterNetworkCallback(callback);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "RadioPlaybackService: network callback was already gone");
        }
    }

    // Binder thread: hop to the main looper before touching the player.
    private void OnNetworkReturned()
    {
        new Handler(Looper.MainLooper!).Post(() =>
        {
            string? mediaId = _recoveryMediaId;
            IExoPlayer? player = _player;
            if (mediaId is null || player is null || !player.PlayWhenReady)
                return;

            _logger?.LogInformation("RadioPlaybackService: network is back, retrying station {Id} now", mediaId);
            RecoverStream(mediaId, _recovery.Restart());
        });
    }

    private sealed class RecoveryNetworkCallback : Android.Net.ConnectivityManager.NetworkCallback
    {
        private readonly RadioPlaybackService _service;
        private Android.Net.Network? _alreadyUp;

        public RecoveryNetworkCallback(RadioPlaybackService service, Android.Net.Network? alreadyUp)
        {
            _service = service;
            _alreadyUp = alreadyUp;
        }

        public override void OnAvailable(Android.Net.Network network)
        {
            // Registering reports the network that is already up: not news, the stream failed on it.
            if (_alreadyUp is not null && _alreadyUp.Equals(network))
                return;

            _service.OnNetworkReturned();
        }

        public override void OnLost(Android.Net.Network network) => _alreadyUp = null;
    }

    private void RecoverStream(string mediaId, int generation)
    {
        IExoPlayer? player = _player;
        if (player is null || !_recovery.IsCurrent(generation) || !player.PlayWhenReady)
            return;

        int index = player.CurrentMediaItemIndex;
        MediaItem? current = player.CurrentMediaItem;
        if (index < 0 || current?.MediaId != mediaId)
            return;

        AzuraStation? station = Catalog?.Stations.FirstOrDefault(s => s.Id.ToString() == mediaId);
        if (station is null)
            return;

        _logger?.LogInformation("RadioPlaybackService: recovery attempt {Attempt} for station {Id}", _recovery.Attempt, mediaId);
        player.ReplaceMediaItem(index, BuildStationItem(station, playable: true, metadataOverride: current.MediaMetadata));
        player.Prepare();
        player.Play();
    }

    // The session reads the player error through LivePlayer, which hides it while a recovery is running. When the
    // recovery gives up nothing in the player changes, so the session would go on showing a buffering stream
    // for good: a harmless change of playback speed (nothing is playing, the stream is closed) makes it read the
    // player again and report the error.
    private void NudgeSession()
    {
        IExoPlayer? player = _player;
        if (player is null)
            return;

        player.PlaybackParameters = new PlaybackParameters(1.001f);
        player.PlaybackParameters = PlaybackParameters.Default;
    }

    // The listener no longer wants this stream (pause, stop, another station, app closed) or it plays again.
    private void CancelRecovery()
    {
        _recovery.Cancel();
        _recoveryMediaId = null;
        _recoveryGaveUp = false;
        StopWatchingNetwork();
    }

    // A live stream that stayed paused for a while (the TV's standby pauses the session, a media key, a long call)
    // would resume from the buffer it kept: minutes behind what everyone else hears. Past this pause length a
    // Play closes the connection and opens it again at the live edge, as when the app starts from scratch.
    private static readonly TimeSpan ReconnectAfterPause = TimeSpan.FromSeconds(5);

    private void ReconnectToLiveEdge()
    {
        // Posted: the player must not be driven from inside its own listener callback.
        new Handler(Looper.MainLooper!).Post(() =>
        {
            IExoPlayer? player = _player;
            if (player is null || player.PlaybackState == BasePlayer.InterfaceConsts.StateIdle)
                return;

            _logger?.LogInformation("RadioPlaybackService: resumed after a long pause, reconnecting to the live stream");

            MediaItem? current = player.CurrentMediaItem;
            AzuraStation? station = current?.MediaId is { } mediaId
                ? Catalog?.Stations.FirstOrDefault(s => s.Id.ToString() == mediaId)
                : null;
            if (current is null || station is null)
            {
                player.Stop();
                player.Prepare();
                return;
            }

            // Stop() goes through STATE_IDLE, which Android Auto reads as NONE and leaves the now-playing
            // screen. Replacing the item with a new cache key and tag makes ExoPlayer build a new media
            // source (same URI and metadata alone would only update the item in place), so the stream
            // reconnects at the live edge and the state goes straight to BUFFERING.
            string reconnectId = Guid.NewGuid().ToString("N");
            MediaItem fresh = BuildStationItem(station, playable: true, metadataOverride: current.MediaMetadata)
                .BuildUpon()!
                .SetCustomCacheKey(reconnectId)!
                .SetTag(new Java.Lang.String(reconnectId))!
                .Build()!;
            player.ReplaceMediaItem(player.CurrentMediaItemIndex, fresh);
        });
    }

    // A URL that reaches READY is the working one — forget its fallback progress.
    private void OnStreamReady()
    {
        if (_player?.CurrentMediaItem?.MediaId is { } mediaId)
            _streamAttempts.TryRemove(mediaId, out _);

        CancelRecovery();
    }

    // A different station means a different stream: its failures start from the first backoff step.
    private void OnMediaItemChanged(string? mediaId)
    {
        if (_recoveryMediaId is not null && mediaId != _recoveryMediaId)
            CancelRecovery();

        FollowStationWhileClosing(mediaId);
    }

    // Pause from the session (notification, lock screen, Bluetooth, media keys, Android Auto, in-app) is a
    // real pause, so the session reports PAUSED: Stop() would report NONE and Android Auto drops its
    // now-playing screen. It must still not resume minutes behind the live edge: past ReconnectAfterPause
    // the Play that follows closes the stream and opens it again at the live edge (PlayerListener).
    private sealed class LivePlayer : ForwardingPlayer
    {
        private readonly IExoPlayer _inner;
        private readonly Action _cancelRecovery;
        private readonly Func<bool> _mayRecover;

        public LivePlayer(IExoPlayer inner, Action cancelRecovery, Func<bool> mayRecover) : base(inner)
        {
            _inner = inner;
            _cancelRecovery = cancelRecovery;
            _mayRecover = mayRecover;
        }

        // While the service is still retrying (a coverage gap, a bad URL with fallbacks left) the stream is not
        // failed, it is being reopened. Reporting the player error would put Android Auto on a full-screen
        // "Source error" with no controls, and back on the now-playing screen at every retry. So the session
        // sees a buffering stream until the retries run out; only then the error is real.
        private bool IsRecovering => _inner.PlayWhenReady && _inner.PlayerError is not null && _mayRecover();

        // A stream that fails while the listener has it paused is not a failure they can see or act on: Play
        // opens it again. Showing it would turn a pause into "Source error".
        private bool PausedAfterFailure => !_inner.PlayWhenReady && _inner.PlayerError is not null;

        public override PlaybackException? PlayerError => IsRecovering || PausedAfterFailure ? null : _inner.PlayerError;

        // A stream that is still opening when the listener pauses stays BUFFERING in the player, and the session
        // maps BUFFERING to a buffering state whatever playWhenReady says: the car keeps showing a stream that
        // is loading, with the pause button, though the listener asked for silence. A paused player is READY
        // to the session, which then reports PAUSED.
        public override int PlaybackState
        {
            get
            {
                int state = _inner.PlaybackState;
                if (IsRecovering)
                    return BasePlayer.InterfaceConsts.StateBuffering;

                if (PausedAfterFailure)
                    return BasePlayer.InterfaceConsts.StateReady;

                return !_inner.PlayWhenReady && state == BasePlayer.InterfaceConsts.StateBuffering
                    ? BasePlayer.InterfaceConsts.StateReady
                    : state;
            }
        }

        public override void Pause()
        {
            _cancelRecovery();
            _inner.Pause();
        }

        public override void Stop()
        {
            _cancelRecovery();
            _inner.Stop();
        }

        public override void Play()
        {
            // Play on a failed stream reopens it right away: the pending backoff retry is redundant.
            _cancelRecovery();
            if (_inner.PlaybackState == BasePlayer.InterfaceConsts.StateIdle && _inner.MediaItemCount > 0)
                _inner.Prepare();
            _inner.Play();
        }
    }

    private sealed class PlayerListener : Java.Lang.Object, IPlayerListener
    {
        private readonly RadioPlaybackService _service;

        public PlayerListener(RadioPlaybackService service) => _service = service;

        private DateTime? _pausedSince;

        public void OnPlayerError(PlaybackException? error) => _service.OnStreamError();

        public void OnPlayWhenReadyChanged(bool playWhenReady, int reason)
        {
            if (!playWhenReady)
            {
                // Paused, or audio focus lost: not the moment to reopen the stream.
                _service.CancelRecovery();
                _pausedSince ??= DateTime.UtcNow;
                return;
            }

            DateTime? since = _pausedSince;
            _pausedSince = null;
            if (since is { } pausedAt && DateTime.UtcNow - pausedAt > ReconnectAfterPause)
                _service.ReconnectToLiveEdge();
        }

        // A call or the assistant takes audio focus only for a while: ExoPlayer then keeps playWhenReady
        // and sets a suppression reason instead, so OnPlayWhenReadyChanged above never sees it. Past the
        // same threshold the stream resumed on the connection it kept idle all that time — behind live.
        private DateTime? _suppressedSince;

        public void OnPlaybackSuppressionReasonChanged(int playbackSuppressionReason)
        {
            if (playbackSuppressionReason != BasePlayer.InterfaceConsts.PlaybackSuppressionReasonNone)
            {
                _suppressedSince ??= DateTime.UtcNow;
                return;
            }

            DateTime? since = _suppressedSince;
            _suppressedSince = null;
            if (since is { } suppressedAt && DateTime.UtcNow - suppressedAt > ReconnectAfterPause)
                _service.ReconnectToLiveEdge();
        }

        public void OnPlaybackStateChanged(int playbackState)
        {
            if (playbackState == BasePlayer.InterfaceConsts.StateReady)
                _service.OnStreamReady();
        }

        public void OnMediaItemTransition(MediaItem? mediaItem, int reason) => _service.OnMediaItemChanged(mediaItem?.MediaId);
    }

    private static async Task<IList<AzuraStation>> GetStationsAsync()
    {
        IAzuraStationCatalog? catalog = Catalog;
        if (catalog is null)
            return Array.Empty<AzuraStation>();

        if (catalog.Stations.Count == 0)
            await catalog.LoadAsync();

        return catalog.Stations.ToList();
    }

    // ── ListenableFuture helper ───────────────────────────────────────────────

    // Media3 callbacks return a Guava ListenableFuture. We build one from an async Task via
    // CallbackToFutureAdapter (the only immediate-future-style helper bound on this platform).
    private static IListenableFuture FutureFromTask(Func<Task<Java.Lang.Object?>> work)
        => (IListenableFuture)CallbackToFutureAdapter.GetFuture(new TaskResolver(work))!;

    private sealed class TaskResolver : Java.Lang.Object, CallbackToFutureAdapter.IResolver
    {
        private readonly Func<Task<Java.Lang.Object?>> _work;

        // While the future is pending, the adapter holds this resolver from Java only. Pin the peer
        // for exactly that window so a GC mid-flight can't pull the ground out from under it.
        private GCHandle _root;

        public TaskResolver(Func<Task<Java.Lang.Object?>> work) => _work = work;

        public Java.Lang.Object? AttachCompleter(CallbackToFutureAdapter.Completer? completer)
        {
            _root = GCHandle.Alloc(this);
            _ = CompleteAsync(completer!);
            return "RadioE45-future";
        }

        private async Task CompleteAsync(CallbackToFutureAdapter.Completer completer)
        {
            try
            {
                Java.Lang.Object? value = await _work();
                completer.Set(value);
            }
            catch (Exception ex)
            {
                completer.SetException(Java.Lang.Throwable.FromException(ex));
            }
            finally
            {
                if (_root.IsAllocated)
                    _root.Free();
            }
        }
    }

    // ── Session callback ──────────────────────────────────────────────────────

    private sealed class LibraryCallback : Java.Lang.Object, MediaLibrarySession.ICallback
    {
        private readonly RadioPlaybackService _service;

        public LibraryCallback(RadioPlaybackService service) => _service = service;

        public IListenableFuture OnGetLibraryRoot(MediaLibrarySession? session, MediaSession.ControllerInfo? browser, LibraryParams? @params)
            => FutureFromTask(() => Task.FromResult<Java.Lang.Object?>(LibraryResult.OfItem(BuildRootItem(), @params)));

        public IListenableFuture OnGetItem(MediaLibrarySession? session, MediaSession.ControllerInfo? browser, string? mediaId)
            => FutureFromTask(async () =>
            {
                if (mediaId == RootId)
                    return LibraryResult.OfItem(BuildRootItem(), (LibraryParams?)null);

                IList<AzuraStation> stations = await GetStationsAsync();
                AzuraStation? station = stations.FirstOrDefault(s => s.Id.ToString() == mediaId);
                if (station is null)
                    return LibraryResult.OfError(LibraryResult.ResultErrorBadValue);

                return LibraryResult.OfItem(_service.BuildStationItem(station, playable: true), (LibraryParams?)null);
            });

        public IListenableFuture OnGetChildren(MediaLibrarySession? session, MediaSession.ControllerInfo? browser, string? parentId, int page, int pageSize, LibraryParams? @params)
            => FutureFromTask(async () =>
            {
                if (parentId != RootId)
                    return LibraryResult.OfItemList(new List<MediaItem>(), @params);

                IList<AzuraStation> stations = await GetStationsAsync();
                List<MediaItem> items = stations.Select(s => _service.BuildStationItem(s, playable: false)).ToList();
                return LibraryResult.OfItemList(items, @params);
            });

        // When a controller (Android Auto / Assistant) selects a browse item it sends media items
        // that carry only the mediaId (no URI, stripped across the binder). Rebuild each with its
        // resolved stream URI so ExoPlayer can play it.
        public IListenableFuture OnAddMediaItems(MediaSession? session, MediaSession.ControllerInfo? controller, IList<MediaItem>? mediaItems)
            => FutureFromTask(async () =>
            {
                IList<AzuraStation> stations = await GetStationsAsync();
                List<MediaItem> resolved = await ResolveItemsAsync(mediaItems ?? new List<MediaItem>(), stations);
                return JavaList(resolved);
            });

        // Expand a single selected station into the full station queue positioned on it, so
        // Seek-to-Next/Previous moves between stations from the car / steering-wheel buttons.
        public IListenableFuture OnSetMediaItems(MediaSession? session, MediaSession.ControllerInfo? controller, IList<MediaItem>? mediaItems, int startIndex, long startPositionMs)
            => FutureFromTask(async () =>
            {
                IList<AzuraStation> stations = await GetStationsAsync();
                mediaItems ??= new List<MediaItem>();

                // A fresh station selection — start every station from its best URL again.
                _service._streamAttempts.Clear();

                MediaItem? requested = mediaItems.Count > 0
                    ? (startIndex >= 0 && startIndex < mediaItems.Count ? mediaItems[startIndex] : mediaItems[0])
                    : null;
                string? selectedId = requested?.MediaId;

                // Voice search (Gemini / Google Assistant): Media3 has no OnPlayFromSearch — Assistant's
                // legacy playFromSearch call arrives here as a MediaItem with no resolvable MediaId but a
                // populated RequestMetadata.SearchQuery, translated internally by Media3 before reaching us.
                if (requested is not null && (selectedId is null || stations.All(s => s.Id.ToString() != selectedId)))
                {
                    string? query = TryGetSearchQuery(requested);
                    if (query is not null)
                        selectedId = ResolveSearchStation(query, stations)?.Id.ToString() ?? selectedId;
                }

                List<MediaItem> queue = stations.Select(s => _service.BuildStationItem(s, playable: true)).ToList();
                if (queue.Count == 0)
                {
                    queue = await ResolveItemsAsync(mediaItems, stations);
                    return new MediaSession.MediaItemsWithStartPosition(queue, 0, startPositionMs);
                }

                int index = selectedId is null ? 0 : queue.FindIndex(i => i.MediaId == selectedId);
                if (index < 0) index = 0;

                // Proactively probe only the station about to play — probing the whole queue up
                // front would add latency to every station tap. The rest of the queue keeps its
                // lazily-resolved (candidate 0) URI and only gets probed reactively via OnStreamError.
                AzuraStation? selectedStation = stations.FirstOrDefault(s => s.Id.ToString() == queue[index].MediaId);
                if (selectedStation is not null)
                    queue[index] = await _service.BuildStationItemProbedAsync(selectedStation, queue[index].MediaMetadata);

                return new MediaSession.MediaItemsWithStartPosition(queue, index, startPositionMs);
            });

        private async Task<List<MediaItem>> ResolveItemsAsync(IList<MediaItem> mediaItems, IList<AzuraStation> stations)
        {
            List<MediaItem> resolved = new(mediaItems.Count);
            foreach (MediaItem item in mediaItems)
            {
                AzuraStation? station = stations.FirstOrDefault(s => s.Id.ToString() == item.MediaId);

                if (station is null)
                {
                    string? query = TryGetSearchQuery(item);
                    if (query is not null)
                        station = ResolveSearchStation(query, stations);
                }

                resolved.Add(station is not null
                    ? await _service.BuildStationItemProbedAsync(station, item.MediaMetadata)
                    : item);
            }
            return resolved;
        }

        // query.Length == 0 means a generic voice command ("play something"): prefer the
        // favorite station, else the first one. A non-empty query matches by name, falling
        // back to the first station if nothing matches, same tolerance as the generic case.
        private AzuraStation? ResolveSearchStation(string query, IList<AzuraStation> stations)
        {
            IAzuraStationCatalog? catalog = Catalog;

            if (query.Length == 0)
                return catalog?.GetFavorite() ?? catalog?.GetFirst();

            AzuraStation? match = stations.FirstOrDefault(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
            if (match is null)
                _service._logger?.LogWarning("RadioPlaybackService: no station matches voice search '{Query}', using the first available", query);

            return match ?? catalog?.GetFirst();
        }

        // Binding gap: MediaItem.Builder.SetRequestMetadata exists but there is no bound getter for
        // the Java field MediaItem.requestMetadata, so it is read via Java reflection instead. Returns
        // null when RequestMetadata isn't set (not a search request at all), empty string for a
        // generic voice command, otherwise the search text.
        private static string? TryGetSearchQuery(MediaItem item)
        {
            try
            {
                Java.Lang.Reflect.Field? field = item.Class?.GetField("requestMetadata");
                Java.Lang.Object? raw = field?.Get(item);
                MediaItem.RequestMetadata? metadata = raw?.JavaCast<MediaItem.RequestMetadata>();
                return metadata?.SearchQuery;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Java.Util.ArrayList JavaList(List<MediaItem> items)
        {
            Java.Util.ArrayList list = new();
            foreach (MediaItem item in items)
                list.Add(item);
            return list;
        }
    }
}
