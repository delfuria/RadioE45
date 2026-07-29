using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Concurrent.Futures;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.Session;
using Google.Common.Util.Concurrent;
using Java.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Audio;
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

    private static IServiceProvider? Services => IPlatformApplication.Current?.Services;

    private static IAzuraStationCatalog? Catalog => Services?.GetService<IAzuraStationCatalog>();

    private static IStreamUrlProber? Prober => Services?.GetService<IStreamUrlProber>();

    public override void OnCreate()
    {
        base.OnCreate();

        _logger = Services?.GetService<ILogger<RadioPlaybackService>>();

        try
        {
            AudioAttributes audioAttributes = new AudioAttributes.Builder()!
                .SetUsage(C.UsageMedia)!
                .SetContentType(C.AudioContentTypeMusic)!
                .Build()!;

            _player = new ExoPlayerBuilder(this)!
                .SetAudioAttributes(audioAttributes, true)!
                .SetHandleAudioBecomingNoisy(true)!
                .SetWakeMode(C.WakeModeNetwork)!
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
            MediaLibrarySession.Builder builder = new MediaLibrarySession.Builder(this, _player, _libraryCallback)!;

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
    public override void OnTaskRemoved(Intent? rootIntent)
    {
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
    }

    public override void OnDestroy()
    {
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
            .SetMediaId(station.Id.ToString())!
            .SetMediaMetadata(metadata)!;

        if (playable)
        {
            string? url = ResolveStreamUrl(station);
            if (!string.IsNullOrEmpty(url))
                item.SetUri(url);
        }

        return item.Build()!;
    }

    private static bool HasTitle(MediaMetadata? metadata)
        => metadata?.Title is { } title && !string.IsNullOrEmpty(title.ToString());

    // Proactive resolution: probes every candidate URL and uses the first one that actually
    // answers, instead of waiting for playback to fail (OnStreamError) before trying the next.
    // Falls back to the first candidate if none of them answer — playback is still attempted
    // rather than blocked, and OnStreamError keeps advancing from there on further failures.
    // Only called for the station about to play right now (selection / voice search), not for
    // the rest of the Android Auto queue, so a station tap doesn't wait on every other station.
    private async Task<MediaItem> BuildStationItemProbedAsync(AzuraStation station, MediaMetadata? metadataOverride)
    {
        MediaMetadata metadata = HasTitle(metadataOverride) ? metadataOverride! : BuildStationMetadata(station);

        IReadOnlyList<string> candidates = GetStreamCandidates(station);
        string? winner = candidates.Count > 0 && Prober is { } prober
            ? await prober.ProbeFirstReachableAsync(candidates.ToArray(), CancellationToken.None)
            : null;

        MediaItem.Builder item = new MediaItem.Builder()!
            .SetMediaId(station.Id.ToString())!
            .SetMediaMetadata(metadata)!;

        string? url = winner ?? candidates.FirstOrDefault();
        if (!string.IsNullOrEmpty(url))
            item.SetUri(url);

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

    // ExoPlayer plays HLS and ICY/MP3 streams directly. StreamUrlFallback is built from the DB
    // (https://{UrlBase}{StreamUrl}, e.g. https://radioe45.ddns.net:8060/radio.mp3) and is the
    // reliable PUBLIC url; the API's ListenUrl (StreamUrl) currently returns a LAN address
    // (192.168.1.100) that only resolves inside the station's own network — see the TODO in
    // AzuraStationCatalog.Map. So prefer the public fallback, then the API urls as last resort.
    private static IReadOnlyList<string> GetStreamCandidates(AzuraStation station)
        => new[] { station.OnAirStreamUrl, station.StreamUrlFallback, station.HlsUrl, station.StreamUrl }
            .Where(u => !string.IsNullOrEmpty(u))
            .Distinct()
            .ToArray()!;

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
            // Every candidate failed — reset so a later manual retry starts from the best URL again.
            _streamAttempts.TryRemove(mediaId, out _);
            _logger?.LogWarning("RadioPlaybackService: all {Count} stream URLs failed for station {Id}", candidateCount, mediaId);
            return;
        }

        _streamAttempts[mediaId] = nextAttempt;
        _logger?.LogInformation("RadioPlaybackService: stream error, trying fallback URL #{Attempt} for station {Id}", nextAttempt, mediaId);

        player.ReplaceMediaItem(index, BuildStationItem(station, playable: true, metadataOverride: current.MediaMetadata));
        player.Prepare();
        player.Play();
    }

    // A URL that reaches READY is the working one — forget its fallback progress.
    private void OnStreamReady()
    {
        if (_player?.CurrentMediaItem?.MediaId is { } mediaId)
            _streamAttempts.TryRemove(mediaId, out _);
    }

    private sealed class PlayerListener : Java.Lang.Object, IPlayerListener
    {
        private readonly RadioPlaybackService _service;

        public PlayerListener(RadioPlaybackService service) => _service = service;

        public void OnPlayerError(PlaybackException? error) => _service.OnStreamError();

        public void OnPlaybackStateChanged(int playbackState)
        {
            if (playbackState == BasePlayer.InterfaceConsts.StateReady)
                _service.OnStreamReady();
        }
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
