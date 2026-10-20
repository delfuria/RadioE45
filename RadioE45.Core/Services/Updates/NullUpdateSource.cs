using RadioE45.Models;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Updates;

/// <summary>Channel without an update check (Android, Mac Catalyst, web).</summary>
public sealed class NullUpdateSource : IAppUpdateSource
{
    public bool IsSupported => false;

    public Task<AppUpdateInfo?> GetLatestAsync(CancellationToken cancellationToken) =>
        Task.FromResult<AppUpdateInfo?>(null);
}
