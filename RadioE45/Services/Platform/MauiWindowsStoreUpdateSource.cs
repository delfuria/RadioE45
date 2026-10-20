#if WINDOWS
using RadioE45.Models;
using Windows.Services.Store;

namespace RadioE45.Services.Platform;

/// <summary>
/// Microsoft Store package of the app: the Store reports whether a newer package exists (no UI is
/// shown, so no window handle is needed). Only for the MSIX installed from the Store; the Inno
/// Setup installer follows GitHub Releases instead. The Store lists the app at
/// https://apps.microsoft.com/detail/9pj0lkl6mnvq.
/// </summary>
public sealed class MauiWindowsStoreUpdateSource : IAppUpdateSource
{
    private const string StorePageUrl = "ms-windows-store://pdp/?productid=9pj0lkl6mnvq";

    public bool IsSupported => true;

    public async Task<AppUpdateInfo?> GetLatestAsync(CancellationToken cancellationToken)
    {
        StoreContext context = StoreContext.GetDefault();
        IReadOnlyList<StorePackageUpdate> updates =
            await context.GetAppAndOptionalStorePackageUpdatesAsync().AsTask(cancellationToken);

        AppVersion? newest = null;
        foreach (StorePackageUpdate update in updates)
        {
            // The Store gives X.Y.0.0 (revision pinned to 0, see RadioE45.csproj): Major.Minor only.
            var candidate = new AppVersion(update.Package.Id.Version.Major, update.Package.Id.Version.Minor);
            if (newest is null || candidate > newest.Value)
                newest = candidate;
        }

        return newest is { } version
            ? new AppUpdateInfo(version, AppUpdateAction.OpenUrl, new Uri(StorePageUrl))
            : null;
    }
}
#endif
