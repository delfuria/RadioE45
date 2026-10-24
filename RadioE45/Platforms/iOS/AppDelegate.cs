using AVFoundation;
using Foundation;
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
            AppleRemoteCommands.Setup();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppDelegate] AppleRemoteCommands.Setup failed: {ex}");
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
}
