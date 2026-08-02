using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
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

    // Back from a tab root (nothing pushed, no modal) used to fall through to the platform default,
    // which finishes the activity and — via OnDestroy below — tears down playback. That made "back"
    // behave like "close", unlike every other radio/music app where back only backgrounds the UI.
    // Home does the right thing already (backgrounding is untouched, see OnDestroy), so back should
    // match it: send the task behind instead of finishing, and only let the platform finish the
    // activity when there's actually somewhere for back to go (a pushed page or a modal).
    // OnBackPressed() is obsolete in favor of OnBackPressedDispatcher callbacks, but
    // MauiAppCompatActivity's own Shell back-navigation handling still lives behind this same
    // override, so calling base.OnBackPressed() here is what reaches it for the pushed-page case.
#pragma warning disable CS0612
    public override void OnBackPressed()
    {
        var navigation = Shell.Current?.Navigation;
        bool canNavigateBack = navigation is not null &&
            (navigation.NavigationStack.Count > 1 || navigation.ModalStack.Count > 0);

        if (!canNavigateBack)
        {
            MoveTaskToBack(true);
            return;
        }

        base.OnBackPressed();
    }
#pragma warning restore CS0612

    // The other half of "closing the UI closes playback" (see RadioPlaybackService.OnTaskRemoved,
    // which covers the swipe-from-recents case, and OnBackPressed above, which now keeps back from
    // reaching here at all): a real Finish() — swiped from recents, or some future explicit exit —
    // still takes the playback service down with it.
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