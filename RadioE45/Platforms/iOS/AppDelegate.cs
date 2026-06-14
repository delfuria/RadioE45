using AVFoundation;
using Foundation;
using MediaPlayer;
using RadioE45.Services.Audio;
using UIKit;

namespace RadioE45;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary? launchOptions)
    {
        AVAudioSession audioSession = AVAudioSession.SharedInstance();
        audioSession.SetCategory(AVAudioSessionCategory.Playback, (AVAudioSessionCategoryOptions)0, out _);
        audioSession.SetActive(true, out _);

        bool result = base.FinishedLaunching(application, launchOptions);

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

        // Live radio stream — disable seek and track navigation
        center.ChangePlaybackPositionCommand.Enabled = false;
        center.SeekForwardCommand.Enabled = false;
        center.SeekBackwardCommand.Enabled = false;
        center.SkipForwardCommand.Enabled = false;
        center.SkipBackwardCommand.Enabled = false;
        center.NextTrackCommand.Enabled = false;
        center.PreviousTrackCommand.Enabled = false;
    }
}