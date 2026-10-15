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
        AttachPlayer(new MediaPlayerStreamPlayer(networkMonitor, logger));
    }
}
