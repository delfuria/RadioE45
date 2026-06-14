using Android.App;
using Android.Graphics;
using Microsoft.Extensions.Logging;

namespace RadioE45.Services.Audio;

public sealed class AndroidNowPlayingService : IPlatformNowPlayingService
{
    private readonly PlatformNowPlayingStateTracker _stateTracker = new();
    private readonly RemoteArtworkLoader _remoteArtworkLoader;
    private readonly ILogger<AndroidNowPlayingService> _logger;

    public AndroidNowPlayingService(RemoteArtworkLoader remoteArtworkLoader, ILogger<AndroidNowPlayingService> logger)
    {
        _remoteArtworkLoader = remoteArtworkLoader;
        _logger = logger;
    }

    public void UpdateMetadata(string artist, string title, string? artworkUrl, int? elapsedSeconds, int? durationSeconds, bool isPlaying)
    {
        PlatformNowPlayingUpdateResult update = _stateTracker.UpdateMetadata(artist, title, artworkUrl, elapsedSeconds, durationSeconds, isPlaying);

        if (update.ArtworkVersion > 0)
            AndroidNowPlayingStateStore.UpdateArtwork(null);

        AndroidNowPlayingStateStore.UpdateSnapshot(update.Snapshot);
        AndroidMediaNotificationService.RequestRefresh(Android.App.Application.Context);

        if (!string.IsNullOrWhiteSpace(update.ArtworkUrlToLoad))
            _ = LoadArtworkAsync(update.ArtworkUrlToLoad, update.ArtworkVersion);
    }

    public void UpdatePlaybackState(bool isPlaying)
    {
        PlatformNowPlayingSnapshot snapshot = _stateTracker.UpdatePlaybackState(isPlaying);
        AndroidNowPlayingStateStore.UpdateSnapshot(snapshot);
        AndroidMediaNotificationService.RequestRefresh(Android.App.Application.Context);
    }

    public void Clear()
    {
        _stateTracker.Clear();
        AndroidNowPlayingStateStore.Clear();
        AndroidMediaNotificationService.RequestStop(Android.App.Application.Context);
    }

    private async Task LoadArtworkAsync(string artworkUrl, int artworkVersion)
    {
        try
        {
            byte[] bytes = await _remoteArtworkLoader.LoadAsync(artworkUrl);
            Bitmap? artwork = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
            if (artwork is null)
            {
                _logger.LogWarning("Unable to decode now playing artwork from {ArtworkUrl}", artworkUrl);
                return;
            }

            if (!_stateTracker.IsArtworkCurrent(artworkUrl, artworkVersion))
                return;

            AndroidNowPlayingStateStore.UpdateArtwork(artwork);
            AndroidMediaNotificationService.RequestRefresh(Android.App.Application.Context);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Unable to download now playing artwork from {ArtworkUrl}", artworkUrl);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Timed out downloading now playing artwork from {ArtworkUrl}", artworkUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error loading now playing artwork from {ArtworkUrl}", artworkUrl);
        }
    }
}
