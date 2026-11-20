#if ANDROID
using Android.Gms.Extensions;
using Xamarin.Google.Android.Play.Core.AppUpdate;
using PlayUpdateInfo = Xamarin.Google.Android.Play.Core.AppUpdate.AppUpdateInfo;
using Xamarin.Google.Android.Play.Core.AppUpdate.Install.Model;
using RadioE45.Models;
using AppUpdateInfo = RadioE45.Models.AppUpdateInfo;

namespace RadioE45.Services.Platform;

/// <summary>
/// Google Play build: the Play In-App Updates API says whether Play offers a newer versionCode
/// (it answers only for an app installed from Play; a sideloaded APK reports no update). The
/// prompt then opens the store page; the update itself is left to Play.
/// </summary>
public sealed class MauiPlayUpdateSource : IAppUpdateSource
{
    public const string StorePageUrl = "https://play.google.com/store/apps/details?id=com.radioe45.app";

    public bool IsSupported => true;

    public async Task<AppUpdateInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        using IAppUpdateManager manager = AppUpdateManagerFactory.Create(Android.App.Application.Context);
        using PlayUpdateInfo info = (PlayUpdateInfo)await manager.GetAppUpdateInfo().AsAsync<PlayUpdateInfo>();

        if (info.UpdateAvailability() != UpdateAvailability.UpdateAvailable)
            return null;

        if (!AppVersion.TryParse(AppInfo.VersionString, out AppVersion current))
            return null;

        return new AppUpdateInfo(
            AndroidVersionCode.ToAppVersion(info.AvailableVersionCode(), current.Major),
            AppUpdateAction.OpenUrl,
            new Uri(StorePageUrl));
    }
}
#endif
