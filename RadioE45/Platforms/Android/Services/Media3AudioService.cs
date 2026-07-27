using Android.Content;
using AndroidX.Core.Content;
using AndroidX.Media3.Common;
using AndroidX.Media3.Session;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Radio;

namespace RadioE45.Services.Audio;

// Android implementation of IAudioService backed by a Media3 MediaController connected to
// RadioPlaybackService's MediaLibrarySession. The single ExoPlayer in that service is the only
// player; the phone UI drives it through this controller and mirrors its state, exactly like
// Android Auto / Bluetooth do. Replaces the old MediaElement-in-UI AudioService on Android.
public sealed class Media3AudioService : Java.Lang.Object, IAudioService, IPlayerListener
{
    private readonly IAzuraStationCatalog _catalog;
    private readonly ILogger<Media3AudioService> _logger;
    private readonly Context _context = Android.App.Application.Context;

    private Task<MediaController>? _connectTask;
    private MediaController? _controller;
    private AzuraStation? _currentStation;
    private bool _streamOpenedRaised;

    // Last metadata pushed to the session, to skip redundant ReplaceMediaItem calls (the now-playing
    // poll fires every ~10s and the progress path more often, but the song rarely changes).
    private string? _lastArtist;
    private string? _lastTitle;
    private string? _lastArtworkUrl;

    public bool IsPlaying { get; private set; }
    public bool IsBuffering { get; private set; }
    public AzuraStation? CurrentStation => _currentStation;

    public event EventHandler<bool>? PlaybackStateChanged;
    public event EventHandler<string?>? ErrorOccurred;
    public event EventHandler<AzuraStation>? StreamOpened;
    public event EventHandler<AzuraStation>? StationChanged;

    public Media3AudioService(IAzuraStationCatalog catalog, ILogger<Media3AudioService> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    // The MediaElement argument is UI-specific and unused on Android — the player lives in the
    // service now. Kick off the controller connection so the session is ready before first play.
    public void Initialize(MediaElement mediaElement) => _ = ConnectAsync();

    public async Task PlayAsync(AzuraStation station)
    {
        _currentStation = station;
        _streamOpenedRaised = false;
        _lastArtist = _lastTitle = _lastArtworkUrl = null;

        await WithControllerAsync(c =>
        {
            // SetMediaItem triggers OnSetMediaItems on the session, which expands to the full
            // station queue positioned on this station (so next/prev works in the car).
            c.SetMediaItem(StationItem(station, metadata: null));
            c.Prepare();
            c.Play();
        });
    }

    public Task PauseAsync() => WithControllerAsync(c => c.Pause());

    public Task ResumeAsync() => WithControllerAsync(c =>
    {
        if (c.PlaybackState == BasePlayer.InterfaceConsts.StateIdle)
            c.Prepare();
        c.Play();
    });

    public async Task StopAsync()
    {
        _currentStation = null;
        await WithControllerAsync(c =>
        {
            c.Stop();
            c.ClearMediaItems();
        });
    }

    public void StopImmediate()
    {
        _currentStation = null;
        MediaController? c = _controller;
        if (c is null)
            return;

        void Stop()
        {
            c.Stop();
            c.ClearMediaItems();
        }

        if (MainThread.IsMainThread)
            Stop();
        else
            MainThread.BeginInvokeOnMainThread(Stop);
    }

    public void SetVolume(double volume)
    {
        float clamped = (float)Math.Clamp(volume, 0.0, 1.0);
        _ = WithControllerAsync(c => c.Volume = clamped);
    }

    public void UpdateMetadata(string artist, string title, string? artworkUrl = null, int? elapsedSeconds = null, int? durationSeconds = null)
    {
        AzuraStation? station = _currentStation;
        if (station is null)
            return;

        // The song rarely changes between polls — skip the round-trip to the session if nothing moved.
        if (artist == _lastArtist && title == _lastTitle && artworkUrl == _lastArtworkUrl)
            return;

        _lastArtist = artist;
        _lastTitle = title;
        _lastArtworkUrl = artworkUrl;

        _ = WithControllerAsync(c =>
        {
            int index = c.CurrentMediaItemIndex;
            if (index < 0 || c.MediaItemCount == 0)
                return;

            MediaMetadata.Builder metadata = new MediaMetadata.Builder()!
                .SetTitle(title)!
                .SetArtist(artist)!
                .SetSubtitle(artist)!
                .SetIsPlayable(Java.Lang.Boolean.True)!
                .SetMediaType((Java.Lang.Integer)MediaMetadata.MediaTypeRadioStation)!;

            if (!string.IsNullOrEmpty(artworkUrl))
                metadata.SetArtworkUri(Android.Net.Uri.Parse(artworkUrl));

            // Same media id / stream → ReplaceMediaItem updates metadata without restarting playback.
            // The session's OnAddMediaItems re-resolves the (stripped) URI while preserving this metadata.
            MediaItem item = new MediaItem.Builder()!
                .SetMediaId(station.Id.ToString())!
                .SetMediaMetadata(metadata.Build())!
                .Build()!;

            c.ReplaceMediaItem(index, item);
        });
    }

    public void Shutdown()
    {
        _currentStation = null;
        MediaController? c = _controller;
        _controller = null;
        _connectTask = null;

        if (c is null)
            return;

        void Release()
        {
            c.RemoveListener(this);
            c.Release();
        }

        if (MainThread.IsMainThread)
            Release();
        else
            MainThread.BeginInvokeOnMainThread(Release);
    }

    // ── IPlayerListener ───────────────────────────────────────────────────────

    public void OnIsPlayingChanged(bool isPlaying)
    {
        IsPlaying = isPlaying;

        if (isPlaying && !_streamOpenedRaised && _currentStation is not null)
        {
            _streamOpenedRaised = true;
            StreamOpened?.Invoke(this, _currentStation);
        }

        PlaybackStateChanged?.Invoke(this, isPlaying);
    }

    public void OnPlaybackStateChanged(int playbackState)
    {
        IsBuffering = playbackState == BasePlayer.InterfaceConsts.StateBuffering;
        // Nudge the UI so the buffering spinner tracks the session even when IsPlaying hasn't flipped.
        PlaybackStateChanged?.Invoke(this, IsPlaying);
    }

    public void OnPlayerError(PlaybackException? error)
    {
        IsPlaying = false;
        IsBuffering = false;
        ErrorOccurred?.Invoke(this, error?.Message);
    }

    // The car / steering wheel can switch stations via next/prev; keep our notion of the current
    // station in sync so metadata updates target the right item.
    public void OnMediaItemTransition(MediaItem? mediaItem, int reason)
    {
        _streamOpenedRaised = false;
        if (mediaItem?.MediaId is not { } id || !int.TryParse(id, out int stationId))
            return;

        AzuraStation? station = _catalog.Stations.FirstOrDefault(s => s.Id == stationId);
        if (station is null || station.Id == _currentStation?.Id)
            return;

        _currentStation = station;
        // Reset the metadata cache so the first now-playing push for the new station always lands.
        _lastArtist = _lastTitle = _lastArtworkUrl = null;
        // Tell the UI the car/steering-wheel switched stations so it can re-point its polling + display.
        StationChanged?.Invoke(this, station);
    }

    // ── Controller connection / dispatch ──────────────────────────────────────

    private Task<MediaController> ConnectAsync() => _connectTask ??= BuildControllerAsync();

    private async Task<MediaController> BuildControllerAsync()
    {
        MediaController controller = await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ComponentName component = new(_context, Java.Lang.Class.FromType(typeof(RadioPlaybackService)));
            SessionToken token = new(_context, component);

            var future = new MediaController.Builder(_context, token).BuildAsync()!;
            TaskCompletionSource<MediaController> tcs = new();

            future.AddListener(new Java.Lang.Runnable(() =>
            {
                try
                {
                    MediaController c = (MediaController)future.Get()!;
                    c.AddListener(this);
                    tcs.TrySetResult(c);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Media3AudioService: failed to connect MediaController");
                    tcs.TrySetException(ex);
                }
            }), ContextCompat.GetMainExecutor(_context)!);

            return tcs.Task;
        });

        _controller = controller;
        return controller;
    }

    private async Task WithControllerAsync(Action<MediaController> action)
    {
        try
        {
            MediaController controller = await ConnectAsync();
            await MainThread.InvokeOnMainThreadAsync(() => action(controller));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Media3AudioService: controller command failed");
        }
    }

    private MediaItem StationItem(AzuraStation station, MediaMetadata? metadata)
    {
        MediaMetadata resolved = metadata ?? BuildStationMetadata(station);
        return new MediaItem.Builder()!
            .SetMediaId(station.Id.ToString())!
            .SetMediaMetadata(resolved)!
            .Build()!;
    }

    private static MediaMetadata BuildStationMetadata(AzuraStation station)
    {
        MediaMetadata.Builder metadata = new MediaMetadata.Builder()!
            .SetTitle(station.Name)!
            .SetArtist(station.Description)!
            .SetSubtitle(station.Description)!
            .SetIsPlayable(Java.Lang.Boolean.True)!
            .SetMediaType((Java.Lang.Integer)MediaMetadata.MediaTypeRadioStation)!;

        if (!string.IsNullOrEmpty(station.LogoUrl))
            metadata.SetArtworkUri(Android.Net.Uri.Parse(station.LogoUrl));

        return metadata.Build()!;
    }
}
