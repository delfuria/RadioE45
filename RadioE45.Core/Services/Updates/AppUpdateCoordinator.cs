using System.Globalization;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Updates;

public enum AppUpdateStatus
{
    /// <summary>The channel has no update check.</summary>
    Unsupported,

    /// <summary>Automatic check already done today.</summary>
    Skipped,

    UpToDate,

    Available,

    /// <summary>Network, parsing or version error. Silent for the automatic check.</summary>
    Failed,
}

public sealed record AppUpdateCheckResult(AppUpdateStatus Status, AppUpdateInfo? Update = null);

/// <summary>
/// Decides whether to check and whether a newer version exists. The automatic check runs on the
/// first launch of each local day (docs/APP-UPDATE-ROADMAP.md D5); the manual one always runs.
/// Showing the prompt is up to the caller.
/// </summary>
public sealed class AppUpdateCoordinator
{
    internal const string LastCheckDateKey = "update.lastCheckDate";

    private readonly IAppUpdateSource _source;
    private readonly IAppEnvironment _environment;
    private readonly ISettingsStore _settings;
    private readonly TimeProvider _time;
    private readonly ILogger<AppUpdateCoordinator> _logger;

    public AppUpdateCoordinator(IAppUpdateSource source, IAppEnvironment environment, ISettingsStore settings,
        TimeProvider time, ILogger<AppUpdateCoordinator> logger)
    {
        _source = source;
        _environment = environment;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    public bool IsSupported => _source.IsSupported;

    public async Task<AppUpdateCheckResult> CheckAsync(bool manual, CancellationToken cancellationToken = default)
    {
        if (!_source.IsSupported)
            return new AppUpdateCheckResult(AppUpdateStatus.Unsupported);

        string today = _time.GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (!manual && _settings.Get(LastCheckDateKey, "") == today)
            return new AppUpdateCheckResult(AppUpdateStatus.Skipped);

        if (!AppVersion.TryParse(_environment.VersionString, out AppVersion current))
        {
            _logger.LogWarning("Update check skipped: unreadable app version '{Version}'", _environment.VersionString);
            return new AppUpdateCheckResult(AppUpdateStatus.Failed);
        }

        AppUpdateInfo? latest;
        try
        {
            latest = await _source.GetLatestAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Not recorded as done: the next launch tries again.
            _logger.LogWarning(ex, "Update check failed");
            return new AppUpdateCheckResult(AppUpdateStatus.Failed);
        }

        _settings.Set(LastCheckDateKey, today);

        if (latest is null || latest.Action == AppUpdateAction.None || latest.Version <= current)
            return new AppUpdateCheckResult(AppUpdateStatus.UpToDate);

        return new AppUpdateCheckResult(AppUpdateStatus.Available, latest);
    }
}
