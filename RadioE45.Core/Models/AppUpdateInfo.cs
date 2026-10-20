namespace RadioE45.Models;

public enum AppUpdateAction
{
    /// <summary>Nothing to offer (the source knows no newer version).</summary>
    None,

    /// <summary>Open <see cref="AppUpdateInfo.Url"/> (store page or release page).</summary>
    OpenUrl,

    /// <summary>Update inside the app. Reserved: no source uses it yet.</summary>
    InAppUpdate,
}

/// <summary>The latest version a source knows for its distribution channel, and how to get it.</summary>
public sealed record AppUpdateInfo(AppVersion Version, AppUpdateAction Action, Uri? Url);
