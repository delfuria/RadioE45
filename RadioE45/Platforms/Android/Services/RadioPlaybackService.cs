using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Concurrent.Futures;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.Session;
using Google.Common.Util.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Radio;

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

    // Per-station stream-URL fallback: mediaId → index into the ordered candidate list. Advanced when
    // ExoPlayer errors on the current URL, reset to 0 once a URL plays (see PlayerListener). Concurrent
    // because it's touched from the player thread (errors) and the session callback tasks (resolve).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _streamAttempts = new();

    private static IServiceProvider? Services => IPlatformApplication.Current?.Services;

    private static IAzuraStationCatalog? Catalog => Services?.GetService<IAzuraStationCatalog>();

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

            _playerListener = new PlayerListener(this);
            _player?.AddListener(_playerListener);

            MediaLibrarySession.Builder builder = new MediaLibrarySession.Builder(this, _player, new LibraryCallback(this))!;

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

    // When the user swipes the app off the recents list, keep playing in the background (radio /
    // Android Auto UX — audio must survive the phone UI closing). Only tear down the foreground
    // service + notification if playback isn't ongoing.
    public override void OnTaskRemoved(Intent? rootIntent)
    {
        IExoPlayer? player = _player;
        if (player is null || !player.PlayWhenReady || player.MediaItemCount == 0)
        {
            StopSelf();
        }
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

        public TaskResolver(Func<Task<Java.Lang.Object?>> work) => _work = work;

        public Java.Lang.Object? AttachCompleter(CallbackToFutureAdapter.Completer? completer)
        {
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
                List<MediaItem> resolved = ResolveItems(mediaItems!, stations);
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

                string? selectedId = mediaItems.Count > 0
                    ? (startIndex >= 0 && startIndex < mediaItems.Count ? mediaItems[startIndex] : mediaItems[0]).MediaId
                    : null;

                List<MediaItem> queue = stations.Select(s => _service.BuildStationItem(s, playable: true)).ToList();
                if (queue.Count == 0)
                {
                    queue = ResolveItems(mediaItems, stations);
                    return new MediaSession.MediaItemsWithStartPosition(queue, 0, startPositionMs);
                }

                int index = selectedId is null ? 0 : queue.FindIndex(i => i.MediaId == selectedId);
                if (index < 0) index = 0;

                return new MediaSession.MediaItemsWithStartPosition(queue, index, startPositionMs);
            });

        private List<MediaItem> ResolveItems(IList<MediaItem> mediaItems, IList<AzuraStation> stations)
        {
            List<MediaItem> resolved = new(mediaItems.Count);
            foreach (MediaItem item in mediaItems)
            {
                AzuraStation? station = stations.FirstOrDefault(s => s.Id.ToString() == item.MediaId);
                resolved.Add(station is not null
                    ? _service.BuildStationItem(station, playable: true, metadataOverride: item.MediaMetadata)
                    : item);
            }
            return resolved;
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
