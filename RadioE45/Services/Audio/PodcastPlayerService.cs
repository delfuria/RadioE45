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
    private readonly IUiDispatcher _uiDispatcher;
    private MediaElementPodcastPlayer? _player;
    private Func<Task<MediaElement>>? _playerFactory;
    private MediaElement? _mediaElement;

    public PodcastPlayerService(
        IAudioService audioService,
        IPodcastProgressRepository progressRepository,
        IUiDispatcher dispatcher,
        ILogger<PodcastPlayerService> logger)
        : base(audioService, progressRepository, dispatcher, logger)
    {
        _uiDispatcher = dispatcher;
    }

    public void SetPlayerFactory(Func<Task<MediaElement>>? factory) => _playerFactory = factory;

    // The MediaElement only exists while an episode is being played. Its native manager
    // deactivates the shared AVAudioSession when it is disposed (iOS), which would silence the
    // live radio every time the episode page is left without playing anything.
    protected override async Task EnsurePlayerAsync()
    {
        if (_mediaElement?.Handler is not null || _playerFactory is null)
            return;

        Func<Task<MediaElement>> factory = _playerFactory;
        MediaElement? element = null;
        await _uiDispatcher.InvokeAsync(async () => element = await factory());
        if (element is not null)
            Initialize(element);
    }

    /// <summary>Attaches a MediaElement that already lives in the visual tree (Android TV view).</summary>
    public void Initialize(MediaElement mediaElement)
    {
        MediaElementPodcastPlayer? previous = _player;
        _mediaElement = mediaElement;
        _player = new MediaElementPodcastPlayer(mediaElement);
        AttachPlayer(_player);
        previous?.Detach();
    }
}
