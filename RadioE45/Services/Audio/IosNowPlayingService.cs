#if IOS || MACCATALYST
using CoreGraphics;
using Foundation;
using MediaPlayer;
using Microsoft.Extensions.Logging;
using UIKit;

namespace RadioE45.Services.Audio;

public sealed class IosNowPlayingService : IPlatformNowPlayingService
{
    private readonly PlatformNowPlayingStateTracker _stateTracker = new();
    private readonly RemoteArtworkLoader _remoteArtworkLoader;
    private readonly ILogger<IosNowPlayingService> _logger;
    private MPMediaItemArtwork? _artwork;

    public IosNowPlayingService(RemoteArtworkLoader remoteArtworkLoader, ILogger<IosNowPlayingService> logger)
    {
        _remoteArtworkLoader = remoteArtworkLoader;
        _logger = logger;
    }

    public void UpdateMetadata(string artist, string title, string? artworkUrl, int? elapsedSeconds, int? durationSeconds, bool isPlaying)
    {
        PlatformNowPlayingUpdateResult update = _stateTracker.UpdateMetadata(artist, title, artworkUrl, elapsedSeconds, durationSeconds, isPlaying);

        if (update.ArtworkVersion > 0)
            _artwork = null;

        Publish(update.Snapshot, _artwork);

        if (!string.IsNullOrWhiteSpace(update.ArtworkUrlToLoad))
            _ = LoadArtworkAsync(update.ArtworkUrlToLoad, update.ArtworkVersion);
    }

    public void UpdatePlaybackState(bool isPlaying)
    {
        PlatformNowPlayingSnapshot snapshot = _stateTracker.UpdatePlaybackState(isPlaying);
        Publish(snapshot, _artwork);
    }

    public void Clear()
    {
        _stateTracker.Clear();
        _artwork = null;

        MainThread.BeginInvokeOnMainThread(() => MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = null!);
    }

    private async Task LoadArtworkAsync(string artworkUrl, int artworkVersion)
    {
        try
        {
            byte[] bytes = await _remoteArtworkLoader.LoadAsync(artworkUrl);
            using NSData data = NSData.FromArray(bytes);
            UIImage? image = UIImage.LoadFromData(data);
            if (image is null)
            {
                _logger.LogWarning("Unable to decode now playing artwork from {ArtworkUrl}", artworkUrl);
                return;
            }

            MPMediaItemArtwork artwork = new(image.Size, _ => image);
            if (!_stateTracker.IsArtworkCurrent(artworkUrl, artworkVersion))
                return;

            _artwork = artwork;
            PlatformNowPlayingSnapshot snapshot = _stateTracker.GetSnapshot();
            Publish(snapshot, artwork);
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

    private static void Publish(PlatformNowPlayingSnapshot snapshot, MPMediaItemArtwork? artwork)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (string.IsNullOrWhiteSpace(snapshot.Title) && string.IsNullOrWhiteSpace(snapshot.Artist))
            {
                MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = null!;
                return;
            }

            MPNowPlayingInfo nowPlaying = new()
            {
                Title = snapshot.Title,
                Artist = snapshot.Artist,
                PlaybackRate = snapshot.IsPlaying ? 1.0 : 0.0
            };

            if (snapshot.ElapsedSeconds.HasValue)
                nowPlaying.ElapsedPlaybackTime = snapshot.ElapsedSeconds.Value;

            if (snapshot.DurationSeconds.HasValue)
                nowPlaying.PlaybackDuration = snapshot.DurationSeconds.Value;

            if (artwork is not null)
                nowPlaying.Artwork = artwork;

            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = nowPlaying;
        });
    }
}
#endif
