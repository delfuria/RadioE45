using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Shared;

/// <summary>
/// Live radio on the Uno heads: the shared <see cref="LiveStreamAudioService"/> (candidate URLs,
/// HLS/Icecast fallback, reconnection, watchdog, live pause = close) driving a
/// <see cref="MediaPlayerStreamPlayer"/>. Unlike MAUI the player needs no page: it is created here.
/// </summary>
public sealed class UnoAudioService : LiveStreamAudioService
{
    public UnoAudioService(
        IStreamUrlProber streamUrlProber,
        IPlatformNowPlayingService platformNowPlayingService,
        IAudioFocusManager audioFocusManager,
        IAppSettingsRepository settingsRepo,
        INetworkMonitor networkMonitor,
        IUiDispatcher dispatcher,
        ILogger<UnoAudioService> logger)
        : base(streamUrlProber, platformNowPlayingService, audioFocusManager, settingsRepo, networkMonitor, dispatcher, logger)
    {
        var player = new MediaPlayerStreamPlayer(networkMonitor, logger);
#if RADIOE45_BROWSER
        player.AutoplayBlocked += (_, _) => dispatcher.Post(OnAutoplayBlocked);
#endif
        AttachPlayer(player);
    }

    // Web: the browser refused to start the radio without a gesture (Safari always at page load,
    // Chrome without the site sound permission). Not a failure to reconnect from: the radio waits
    // and starts at the first click or key press in the page, the moment the browser allows it
    // (also when that click is on Play: a second resume is harmless).
#if RADIOE45_BROWSER
    private void OnAutoplayBlocked()
    {
        _ = PauseAsync();
        Browser.BrowserUserActivation.OnFirstGesture(() =>
        {
            if (CurrentStation is not null && !IsPlaying)
                _ = ResumeAsync();
        });
    }
#endif
}
