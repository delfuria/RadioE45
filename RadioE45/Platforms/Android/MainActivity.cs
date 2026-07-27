using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

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