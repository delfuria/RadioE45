using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Audio;

/// <summary>
/// Podcast episodes on every MAUI platform: the shared <see cref="PodcastPlaybackService"/>
/// driving the MediaElement that PodcastEpisodesPage hands over.
/// </summary>
public class PodcastPlayerService : PodcastPlaybackService, IMediaElementHost
{
    private MediaElementPodcastPlayer? _player;

    public PodcastPlayerService(
        IAudioService audioService,
        IPodcastProgressRepository progressRepository,
        IUiDispatcher dispatcher,
        ILogger<PodcastPlayerService> logger)
        : base(audioService, progressRepository, dispatcher, logger)
    {
    }

    public void Initialize(MediaElement mediaElement)
    {
        MediaElementPodcastPlayer? previous = _player;
        _player = new MediaElementPodcastPlayer(mediaElement);
        AttachPlayer(_player);
        previous?.Detach();
    }
}
