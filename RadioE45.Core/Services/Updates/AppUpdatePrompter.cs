using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Localization;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Updates;

/// <summary>
/// The user-facing side of the update check: the startup prompt and the "update" action, shared
/// by every head. The startup check never throws and never reports errors to the user.
/// </summary>
public sealed class AppUpdatePrompter
{
    private readonly AppUpdateCoordinator _coordinator;
    private readonly IDialogService _dialogs;
    private readonly IUrlLauncher _urlLauncher;
    private readonly ILogger<AppUpdatePrompter> _logger;

    public AppUpdatePrompter(AppUpdateCoordinator coordinator, IDialogService dialogs, IUrlLauncher urlLauncher,
        ILogger<AppUpdatePrompter> logger)
    {
        _coordinator = coordinator;
        _dialogs = dialogs;
        _urlLauncher = urlLauncher;
        _logger = logger;
    }

    /// <summary>Automatic check (first launch of the day): asks the user only when a newer version exists.</summary>
    public async Task CheckAtStartupAsync()
    {
        try
        {
            AppUpdateCheckResult result = await _coordinator.CheckAsync(manual: false);
            if (result.Status == AppUpdateStatus.Available && result.Update is { } update)
                await PromptAsync(update);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Startup update check failed");
        }
    }

    public async Task PromptAsync(AppUpdateInfo update)
    {
        LocalizationResourceManager loc = LocalizationResourceManager.Instance;
        bool accepted = await _dialogs.ConfirmAsync(
            loc["Update_Title"],
            loc.Format("Update_Message", update.Version),
            loc["Update_Accept"],
            loc["Update_Later"]);

        if (accepted)
            await OpenAsync(update);
    }

    /// <summary>Takes the user to the store or release page.</summary>
    public async Task OpenAsync(AppUpdateInfo update)
    {
        if (update.Action != AppUpdateAction.OpenUrl || update.Url is null)
            return;

        try
        {
            await _urlLauncher.OpenAsync(update.Url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open the update page {Url}", update.Url);
        }
    }
}
