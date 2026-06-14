using AVFoundation;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
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

        return base.FinishedLaunching(application, launchOptions);
    }

    public override void WillTerminate(UIApplication application)
    {
        IPlatformApplication.Current?.Services.GetService<OnAirViewModel>()?.ShutdownAsync().GetAwaiter().GetResult();
        base.WillTerminate(application);
    }
}