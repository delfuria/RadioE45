using AVFoundation;
using Foundation;
using MediaPlayer;
using Microsoft.Extensions.Logging;
using RadioE45.Services.Audio;
using RadioE45.ViewModels;
using UIKit;

namespace RadioE45;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        // Category only: the session is activated when a stream is opened (IosStreamPlayer.Open).
        // Activating it at launch would stop the car's FM radio as soon as CarPlay opens the app
        // (CarPlay Developer Guide: "only activate your audio session when you are ready to play audio").
        AVAudioSession audioSession = AVAudioSession.SharedInstance();
        audioSession.SetCategory(AVAudioSessionCategory.Playback, (AVAudioSessionCategoryOptions)0, out _);

        bool result = base.FinishedLaunching(application, launchOptions);

        ILoggerFactory? loggerFactory = IPlatformApplication.Current?.Services?.GetService<ILoggerFactory>();
        if (loggerFactory is not null)
            AudioSessionDiagnostics.Start(loggerFactory.CreateLogger("AudioSession"));

        // Set up lock-screen / CarPlay remote control handlers. Must run after base
        // so that the MAUI DI container (IPlatformApplication.Current) is ready.
        try
        {
            SetupRemoteCommandCenter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppDelegate] SetupRemoteCommandCenter failed: {ex}");
        }

        return result;
    }

    public override UISceneConfiguration GetConfiguration(
        UIApplication application,
        UISceneSession connectingSceneSession,
        UISceneConnectionOptions options)
    {
        System.Diagnostics.Debug.WriteLine($"[AppDelegate] GetConfiguration role={connectingSceneSession.Role.GetConstant()}");

        // GetConstant() comparison is more reliable than .Equals() for NSString-backed role values.
        if (connectingSceneSession.Role.GetConstant() == UIWindowSceneSessionRole.CarTemplateApplication.GetConstant())
        {
            System.Diagnostics.Debug.WriteLine("[AppDelegate] GetConfiguration → CarPlay scene");
            UISceneConfiguration config = new("CarPlay Configuration", connectingSceneSession.Role);
            config.DelegateType = typeof(CarPlaySceneDelegate);
            return config;
        }

        // Call base so MAUI can apply its own scene configuration, then swap the
        // delegate type for our thin SceneDelegate subclass of MauiUISceneDelegate.
        // Creating a brand-new UISceneConfiguration would bypass MAUI's internal setup.
        UISceneConfiguration mainConfig = base.GetConfiguration(application, connectingSceneSession, options);
        mainConfig.DelegateType = typeof(SceneDelegate);
        return mainConfig;
    }

    private static void SetupRemoteCommandCenter()
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