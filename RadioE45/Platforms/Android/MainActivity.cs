using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using Microsoft.Extensions.DependencyInjection;
using RadioE45.Services.Audio;

namespace RadioE45;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int PostNotificationsRequestCode = 1001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        RequestPostNotificationsPermission();
    }

    // The other half of "closing the UI closes playback" (see RadioPlaybackService.OnTaskRemoved,
    // which covers the swipe-from-recents case): leaving the app for good — back out of the root
    // page, or Finish() — also takes the playback service down.
    //
    // IsFinishing separates a real exit from a rotation or a process restart; on top of that,
    // StopService is a no-op for as long as something is still bound, so a session in use by
    // Android Auto or Bluetooth survives an activity that goes away underneath it.
    protected override void OnDestroy()
    {
        if (IsFinishing)
        {
            try
            {
                // Release our own MediaController first. It binds the service, and a bound service is
                // not destroyed by StopSelf/StopService — without this the process stays resident with
                // the ExoPlayer instance still allocated, which is exactly what closing is meant to
                // avoid. Dropping the binding lets the service reach OnDestroy and free the player.
                IPlatformApplication.Current?.Services?.GetService<IAudioService>()?.Shutdown();
                StopService(new Intent(this, typeof(RadioPlaybackService)));
            }
            catch (Exception)
            {
                // Teardown path: the service may already be gone. Never take the activity down with us.
            }
        }

        base.OnDestroy();
    }

    // Android 13+ gates the media notification / lock-screen controls behind POST_NOTIFICATIONS.
    // Without it the Media3 foreground service still plays, but the user loses on-phone controls,
    // so ask once on launch. (Android Auto / Bluetooth are unaffected either way.)
    private void RequestPostNotificationsPermission()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            return;

        if (CheckSelfPermission(Android.Manifest.Permission.PostNotifications) == Permission.Granted)
            return;

        ActivityCompat.RequestPermissions(this, new[] { Android.Manifest.Permission.PostNotifications }, PostNotificationsRequestCode);
    }
}