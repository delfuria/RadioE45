using RadioE45.Services.Localization;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Legal;

public sealed class TermsService : ITermsService
{
    /// <summary>Raise it when the terms text changes materially, to show the dialog again.</summary>
    public const int CurrentTermsVersion = 1;

    internal const string AcceptedVersionKey = "terms_accepted_version";

    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs;

    public TermsService(ISettingsStore settings, IDialogService dialogs)
    {
        _settings = settings;
        _dialogs = dialogs;
    }

    public async Task EnsureAcceptedAsync()
    {
        if (_settings.Get(AcceptedVersionKey, 0) >= CurrentTermsVersion)
            return;

        LocalizationResourceManager loc = LocalizationResourceManager.Instance;
        await _dialogs.AlertAsync(loc["Terms_Title"], loc["Terms_Body"], loc["Terms_Accept"]);

        _settings.Set(AcceptedVersionKey, CurrentTermsVersion);
    }

    public Task ShowAsync()
    {
        LocalizationResourceManager loc = LocalizationResourceManager.Instance;
        return _dialogs.AlertAsync(loc["Terms_Title"], loc["Terms_Body"], loc["Common_Ok"]);
    }
}
