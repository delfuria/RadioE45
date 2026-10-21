using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Audio;

/// <summary>
/// Live radio on iOS, Mac Catalyst and Windows: the shared <see cref="LiveStreamAudioService"/>
/// driving the MediaElement that OnAirPage hands over. On iOS it owns a native AVPlayer instead,
/// so playback does not depend on any page (CarPlay can start the app with no main window).
/// (Android uses Media3AudioService.)
/// </summary>
public class AudioService : LiveStreamAudioService, IMediaElementHost
{
#if !IOS
    private MediaElementStreamPlayer? _player;
#endif

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
#if IOS
        AttachPlayer(new AvStreamPlayer());
#endif
    }

    public void Initialize(MediaElement mediaElement)
    {
#if !IOS
        MediaElementStreamPlayer? previous = _player;
        _player = new MediaElementStreamPlayer(mediaElement);
        AttachPlayer(_player);
        previous?.Detach();
#endif
    }
}
