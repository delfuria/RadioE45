using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Audio;

/// <summary>
/// Live radio on iOS, Mac Catalyst and Windows: the shared <see cref="LiveStreamAudioService"/>
/// driving the MediaElement that OnAirPage hands over. On iOS it owns native players instead (IosStreamPlayer),
/// so playback does not depend on any page (CarPlay can start the app with no main window).
/// (Android uses Media3AudioService.)
/// </summary>
public class AudioService : LiveStreamAudioService, IMediaElementHost
{
#if !IOS
    private MediaElementStreamPlayer? _player;
#else
    private readonly Foundation.NSObject _routeChangeObserver;
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
        AttachPlayer(new IosStreamPlayer(logger));

        // CarPlay cable unplugged, headphones removed, car switched off: iOS moves the audio to the
        // speaker and interrupts it. Apple's rule is to stay paused; without this the live engine's
        // watchdog saw "Paused" as a stalled stream and restarted it on the speaker a few seconds later.
        _routeChangeObserver = AVFoundation.AVAudioSession.Notifications.ObserveRouteChange((_, e) =>
        {
            if (e.Reason == AVFoundation.AVAudioSessionRouteChangeReason.OldDeviceUnavailable && CurrentStation is not null)
            {
                logger.LogInformation("Audio output device removed, pausing");
                dispatcher.Post(async () => await PauseAsync());
            }
        });
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
