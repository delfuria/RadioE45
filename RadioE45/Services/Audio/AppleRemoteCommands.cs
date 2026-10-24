#if IOS || MACCATALYST
using Foundation;
using MediaPlayer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadioE45.ViewModels;

namespace RadioE45.Services.Audio;

/// <summary>
/// Lock screen, Control Center, CarPlay and headset commands. Pause goes through
/// <see cref="IAudioService.PauseAsync"/>, which closes the live stream: a native AVPlayer pause
/// would keep buffering and resume behind the live edge.
/// </summary>
internal static class AppleRemoteCommands
{
    public static void Setup()
    {
        MPRemoteCommandCenter center = MPRemoteCommandCenter.Shared;

        center.PlayCommand.Enabled = true;
        center.PlayCommand.AddTarget(e =>
        {
            IAudioService? audio = IPlatformApplication.Current?.Services?.GetService<IAudioService>();
            if (audio?.CurrentStation is null) return MPRemoteCommandHandlerStatus.NoSuchContent;
            MainThread.BeginInvokeOnMainThread(async () => await audio.ResumeAsync());
            return MPRemoteCommandHandlerStatus.Success;
        });

        center.PauseCommand.Enabled = true;
        center.PauseCommand.AddTarget(e =>
        {
            IAudioService? audio = IPlatformApplication.Current?.Services?.GetService<IAudioService>();
            if (audio is null) return MPRemoteCommandHandlerStatus.NoSuchContent;
            MainThread.BeginInvokeOnMainThread(async () => await audio.PauseAsync());
            return MPRemoteCommandHandlerStatus.Success;
        });

        center.TogglePlayPauseCommand.Enabled = true;
        center.TogglePlayPauseCommand.AddTarget(e =>
        {
            IAudioService? audio = IPlatformApplication.Current?.Services?.GetService<IAudioService>();
            if (audio is null) return MPRemoteCommandHandlerStatus.NoSuchContent;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (audio.IsPlaying)
                    await audio.PauseAsync();
                else if (audio.CurrentStation is not null)
                    await audio.ResumeAsync();
            });
            return MPRemoteCommandHandlerStatus.Success;
        });

        // Next/previous track = next/previous station (CarPlay, lock screen, steering wheel, headphones),
        // the same OnAirViewModel commands as the phone UI and as Seek-to-Next/Previous on Android Auto.
        center.NextTrackCommand.Enabled = true;
        center.NextTrackCommand.AddTarget(e => SwitchStation(next: true));
        center.PreviousTrackCommand.Enabled = true;
        center.PreviousTrackCommand.AddTarget(e => SwitchStation(next: false));

        // Live radio stream — disable seeking
        center.ChangePlaybackPositionCommand.Enabled = false;
        center.SeekForwardCommand.Enabled = false;
        center.SeekBackwardCommand.Enabled = false;
        center.SkipForwardCommand.Enabled = false;
        center.SkipBackwardCommand.Enabled = false;
    }

    private static MPRemoteCommandHandlerStatus SwitchStation(bool next)
    {
        OnAirViewModel? onAir = IPlatformApplication.Current?.Services?.GetService<OnAirViewModel>();
        if (onAir is null) return MPRemoteCommandHandlerStatus.NoSuchContent;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (next)
                await onAir.NextStationCommand.ExecuteAsync(null);
            else
                await onAir.PreviousStationCommand.ExecuteAsync(null);
        });
        return MPRemoteCommandHandlerStatus.Success;
    }
}
#endif
