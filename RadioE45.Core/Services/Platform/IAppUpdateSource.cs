using RadioE45.Models;

namespace RadioE45.Services.Platform;

/// <summary>
/// Where the latest published version of THIS app is read from: one source per distribution
/// channel (App Store, Microsoft Store, GitHub Releases). Never compare versions across channels.
/// </summary>
public interface IAppUpdateSource
{
    /// <summary>False when the channel has no way to check (e.g. Android, Mac Catalyst before publication).</summary>
    bool IsSupported { get; }

    /// <summary>
    /// The latest published version, or null when the channel has no record of the app.
    /// Throws on network or parsing failures: the caller treats them as "could not check".
    /// </summary>
    Task<AppUpdateInfo?> GetLatestAsync(CancellationToken cancellationToken);
}
