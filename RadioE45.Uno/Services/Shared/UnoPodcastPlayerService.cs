using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Shared;

/// <summary>
/// Podcast episodes on the Uno heads: the shared <see cref="PodcastPlaybackService"/> driving a
/// <see cref="MediaPlayerPodcastPlayer"/>, created here (no page-hosted element, unlike MAUI).
/// </summary>
public sealed class UnoPodcastPlayerService : PodcastPlaybackService
{
    public UnoPodcastPlayerService(
        IAudioService audioService,
        IPodcastProgressRepository progressRepository,
        IUiDispatcher dispatcher,
        ILogger<UnoPodcastPlayerService> logger)
        : base(audioService, progressRepository, dispatcher, logger)
    {
        AttachPlayer(new MediaPlayerPodcastPlayer(logger));
    }
}
