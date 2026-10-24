using AVFoundation;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
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
        AVAudioSession audioSession = AVAudioSession.SharedInstance();
        audioSession.SetCategory(AVAudioSessionCategory.Playback, AVAudioSessionCategoryOptions.MixWithOthers, out _);
        audioSession.SetActive(true, out _);

        bool result = base.FinishedLaunching(application, launchOptions);

        // Control Center / media keys: pause must close the live stream, not pause AVPlayer (see AppleRemoteCommands).
        // After base so the MAUI service provider is ready.
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

    public override void WillTerminate(UIApplication application)
    {
        IPlatformApplication.Current?.Services.GetService<OnAirViewModel>()?.ShutdownAsync().GetAwaiter().GetResult();
        base.WillTerminate(application);
    }
}