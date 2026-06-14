using Android.App;
using Android.Content;
using Android.Media;
using Android.Media.Browse;
using Android.Media.Session;
using Android.OS;
using Android.Service.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Radio;

namespace RadioE45;

[Service(
    Name = "com.radioe45.app.RadioMediaBrowserService",
    Exported = true)]
[IntentFilter(new[] { "android.media.browse.MediaBrowserService" })]
public sealed class RadioMediaBrowserService : MediaBrowserService
{
    private const string RootId = "ROOT";

    private static readonly HashSet<string> AllowedCallers = new(StringComparer.Ordinal)
    {
        "com.google.android.projection.gearhead",
        "com.google.android.mediasimulator",
    };

    private MediaSession? _session;
    private ILogger<RadioMediaBrowserService>? _logger;

    public override void OnCreate()
    {
        base.OnCreate();

        _logger = IPlatformApplication.Current?.Services?.GetService<ILogger<RadioMediaBrowserService>>();

        try
        {
            _session = new MediaSession(this, "RadioE45Auto");
            _session.SetCallback(new AutoMediaCallback());
            _session.SetFlags(MediaSessionFlags.HandlesMediaButtons | MediaSessionFlags.HandlesTransportControls);
            _session.Active = true;
            SessionToken = _session.SessionToken;

            SyncSessionFromStore();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "RadioMediaBrowserService: failed to create MediaSession");
        }

        AndroidNowPlayingStateStore.SnapshotChanged += OnSnapshotChanged;

    }

    public override void OnDestroy()
    {
        AndroidNowPlayingStateStore.SnapshotChanged -= OnSnapshotChanged;
        _session?.Release();
        _session = null;
        base.OnDestroy();
    }

    public override BrowserRoot? OnGetRoot(string clientPackageName, int clientUid, Bundle? rootHints)
    {
        if (!AllowedCallers.Contains(clientPackageName))
            return null;

        return new BrowserRoot(RootId, null);
    }

    public override void OnLoadChildren(string parentId, Result result)
    {
        if (parentId != RootId)
        {
            result.SendResult(new Java.Util.ArrayList());
            return;
        }

        result.Detach();
        _ = SendStationsAsync(result);
    }

    private async Task SendStationsAsync(Result result)
    {
        try
        {
            IAzuraStationCatalog? catalog = IPlatformApplication.Current?.Services?.GetService<IAzuraStationCatalog>();
            if (catalog is null)
            {
                result.SendResult(new Java.Util.ArrayList());
                return;
            }

            if (catalog.Stations.Count == 0)
                await catalog.LoadAsync();

            Java.Util.ArrayList items = new();
            foreach (AzuraStation station in catalog.Stations)
            {
                MediaDescription.Builder desc = new MediaDescription.Builder();
                desc.SetMediaId(station.Id.ToString());
                desc.SetTitle(station.Name);
                desc.SetSubtitle(station.Description);

                if (!string.IsNullOrEmpty(station.LogoUrl))
                    desc.SetIconUri(Android.Net.Uri.Parse(station.LogoUrl));

                // Android.Media.Browse.MediaItemFlags: Playable = 2, Browsable = 1
                items.Add(new MediaBrowser.MediaItem(desc.Build()!, (Android.Media.Browse.MediaItemFlags)2));
            }

            result.SendResult(items);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "RadioMediaBrowserService: error loading stations");
            result.SendResult(new Java.Util.ArrayList());
        }
    }

    private void OnSnapshotChanged(PlatformNowPlayingSnapshot snapshot)
    {
        if (_session is null) return;
        SyncSession(_session, snapshot);
    }

    private void SyncSessionFromStore()
    {
        if (_session is null) return;
        (PlatformNowPlayingSnapshot snapshot, _) = AndroidNowPlayingStateStore.GetState();
        SyncSession(_session, snapshot);
    }

    private static void SyncSession(MediaSession session, PlatformNowPlayingSnapshot snapshot)
    {
        MediaMetadata.Builder meta = new();
        meta.PutString(MediaMetadata.MetadataKeyTitle, snapshot.Title);
        meta.PutString(MediaMetadata.MetadataKeyArtist, snapshot.Artist);
        meta.PutString(MediaMetadata.MetadataKeyDisplayTitle, snapshot.Title);
        meta.PutString(MediaMetadata.MetadataKeyDisplaySubtitle, snapshot.Artist);
        if (snapshot.DurationSeconds.HasValue)
            meta.PutLong(MediaMetadata.MetadataKeyDuration, snapshot.DurationSeconds.Value * 1000L);
        session.SetMetadata(meta.Build());

        PlaybackStateCode code = snapshot.IsPlaying ? PlaybackStateCode.Playing : PlaybackStateCode.Paused;
        long pos = snapshot.ElapsedSeconds.HasValue
            ? snapshot.ElapsedSeconds.Value * 1000L
            : PlaybackState.PlaybackPositionUnknown;
        float speed = snapshot.IsPlaying ? 1f : 0f;

        PlaybackState.Builder pb = new();
        pb.SetState(code, pos, speed, SystemClock.ElapsedRealtime());
        pb.SetActions(
            PlaybackState.ActionPlay |
            PlaybackState.ActionPause |
            PlaybackState.ActionPlayPause |
            PlaybackState.ActionStop |
            PlaybackState.ActionPlayFromMediaId);
        session.SetPlaybackState(pb.Build());
    }

    private sealed class AutoMediaCallback : MediaSession.Callback
    {
        private static IAudioService? Audio =>
            IPlatformApplication.Current?.Services?.GetService<IAudioService>();

        private static IAzuraStationCatalog? Catalog =>
            IPlatformApplication.Current?.Services?.GetService<IAzuraStationCatalog>();

        public override void OnPlay()
        {
            IAudioService? audio = Audio;
            if (audio is null) return;

            if (audio.CurrentStation is not null)
            {
                _ = audio.ResumeAsync();
            }
            else
            {
                _ = PlayDefaultStationAsync(audio);
            }
        }

        private static async Task PlayDefaultStationAsync(IAudioService audio)
        {
            IAzuraStationCatalog? catalog = Catalog;
            if (catalog is null) return;

            if (catalog.Stations.Count == 0)
                await catalog.LoadAsync();

            AzuraStation? station = catalog.Stations.FirstOrDefault();
            if (station is not null)
                await audio.PlayAsync(station);
        }

        public override void OnPlayFromMediaId(string? mediaId, Bundle? extras)
        {
            if (!int.TryParse(mediaId, out int id)) return;
            AzuraStation? station = Catalog?.Stations.FirstOrDefault(s => s.Id == id);
            if (station is not null)
                _ = Audio?.PlayAsync(station);
        }

        public override void OnPause() => _ = Audio?.PauseAsync();

        public override void OnStop() => _ = Audio?.StopAsync();
    }
}
