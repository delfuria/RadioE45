using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Audio;

/// <summary>
/// Live radio on iOS, Mac Catalyst and Windows: the shared <see cref="LiveStreamAudioService"/>
/// driving the MediaElement that OnAirPage hands over. (Android uses Media3AudioService.)
/// </summary>
public class AudioService : LiveStreamAudioService, IMediaElementHost
{
    private MediaElementStreamPlayer? _player;

    public AudioService(
        IStreamUrlProber streamUrlProber,
        IPlatformNowPlayingService platformNowPlayingService,
        IAudioFocusManager audioFocusManager,
        IAppSettingsRepository settingsRepo,
        INetworkMonitor networkMonitor,
        IUiDispatcher dispatcher,
        ILogger<AudioService> logger)
        : base(streamUrlProber, platformNowPlayingService, audioFocusManager, settingsRepo, networkMonitor, dispatcher, logger)
    {
    }

    public void Initialize(MediaElement mediaElement)
    {
        MediaElementStreamPlayer? previous = _player;
        _player = new MediaElementStreamPlayer(mediaElement);
        AttachPlayer(_player);
        previous?.Detach();
    }
}
