using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;
using RadioE45.Services.Legal;
using RadioE45.Services.Localization;
using RadioE45.Services.Platform;
using RadioE45.Services.Updates;

namespace RadioE45.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    /// <summary>Copyright notices and license texts of the third-party components: too long for a phone screen, so they open in the browser.</summary>
    private static readonly Uri LicensesUri = new("https://github.com/delfuria/RadioE45/blob/main/THIRD-PARTY-NOTICES.txt");

    private readonly IAppSettingsRepository _settingsRepo;
    private readonly IDatabaseService _databaseService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ISettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly ICrashReportingService _crashReporting;
    private readonly IAppEnvironment _environment;
    private readonly ITermsService _terms;
    private readonly IUrlLauncher _urlLauncher;
    private readonly AppUpdateCoordinator _updateCoordinator;
    private readonly AppUpdatePrompter _updatePrompter;
    private AppUpdateInfo? _availableUpdate;
    private AppSettings? _currentSettings;
    private bool _hasChanges;

#if DEBUG
    public bool IsDebugBuild => true;
#else
    public bool IsDebugBuild => false;
#endif

    [ObservableProperty]
    public partial string ThemePreference { get; set; } = "Dark";

    [ObservableProperty]
    public partial string AppVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial float SeedVersion { get; set; }

    [ObservableProperty]
    public partial bool MustUpdate { get; set; }

    [ObservableProperty]
    public partial bool StartWithFavorite { get; set; }

    [ObservableProperty]
    public partial bool CrashReportingEnabled { get; set; }

    [ObservableProperty]
    public partial int PlaybackLatencyOffsetSeconds { get; set; }

    [ObservableProperty]
    public partial bool PreferHlsStream { get; set; }

    [ObservableProperty]
    public partial int RequestPageSize { get; set; }

    [ObservableProperty]
    public partial string UpdateStatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsUpdateAvailable { get; set; }

    /// <summary>False on channels without an update check (Android, Mac Catalyst, web): the section is hidden.</summary>
    public bool IsUpdateCheckSupported => _updateCoordinator.IsSupported;

    public bool IsCrashReportingAvailable => _crashReporting.IsAvailable;

    public SettingsViewModel(
        IAppSettingsRepository settingsRepo,
        IDatabaseService databaseService,
        INavigationService navigation,
        IDialogService dialogs,
        ISettingsStore settingsStore,
        IThemeService themeService,
        ICrashReportingService crashReporting,
        IAppEnvironment environment,
        ITermsService terms,
        IUrlLauncher urlLauncher,
        AppUpdateCoordinator updateCoordinator,
        AppUpdatePrompter updatePrompter,
        ILogger<SettingsViewModel> logger)
    {
        Logger = logger;
        _settingsRepo = settingsRepo;
        _databaseService = databaseService;
        _navigation = navigation;
        _dialogs = dialogs;
        _settingsStore = settingsStore;
        _themeService = themeService;
        _crashReporting = crashReporting;
        _environment = environment;
        _terms = terms;
        _urlLauncher = urlLauncher;
        _updateCoordinator = updateCoordinator;
        _updatePrompter = updatePrompter;
        Title = "Impostazioni";
        AppVersion = $"{environment.VersionString} ({environment.BuildString}, {environment.CommitId})";
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        _currentSettings = await _settingsRepo.GetAsync();
        // Il tema applicato all'avvio è quello dello store: è la fonte di verità (il valore nel DB
        // resta una copia scritta insieme a questo); con lo store vuoto vale il DB.
        ThemePreference = _settingsStore.Get("theme_preference", _currentSettings.ThemePreference);
        SeedVersion = (float) _currentSettings.SeedVersion;
        MustUpdate = _currentSettings.MustUpdate;
        StartWithFavorite = _currentSettings.StartWithFavorite;
        CrashReportingEnabled = _currentSettings.CrashReportingEnabled;
        PlaybackLatencyOffsetSeconds = _currentSettings.PlaybackLatencyOffsetSeconds;
        PreferHlsStream = _currentSettings.PreferHlsStream;
        RequestPageSize = _currentSettings.RequestPageSize;
        _hasChanges = false;
        SaveSettingsCommand.NotifyCanExecuteChanged();
    }

    partial void OnThemePreferenceChanged(string value)
    {
        _themeService.Apply(value);
        MarkChanged();
    }
    
    partial void OnStartWithFavoriteChanged(bool value) => MarkChanged();

    partial void OnCrashReportingEnabledChanged(bool value) => MarkChanged();

    partial void OnPlaybackLatencyOffsetSecondsChanged(int value) => MarkChanged();

    partial void OnPreferHlsStreamChanged(bool value) => MarkChanged();

    partial void OnRequestPageSizeChanged(int value) => MarkChanged();

    private void MarkChanged()
    {
        _hasChanges = true;
        SaveSettingsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private Task ShowTermsAsync() => _terms.ShowAsync();

    [RelayCommand]
    private async Task OpenLicensesAsync()
    {
        try
        {
            await _urlLauncher.OpenAsync(LicensesUri);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Unable to open {Uri}", LicensesUri);
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (!_updateCoordinator.IsSupported)
            return;

        LocalizationResourceManager loc = LocalizationResourceManager.Instance;
        UpdateStatusText = loc["Update_Status_Checking"];
        IsUpdateAvailable = false;

        AppUpdateCheckResult result = await _updateCoordinator.CheckAsync(manual: true);
        _availableUpdate = result.Update;
        IsUpdateAvailable = result.Status == AppUpdateStatus.Available;
        UpdateStatusText = result.Status switch
        {
            AppUpdateStatus.Available => loc.Format("Update_Status_Available", result.Update!.Version),
            AppUpdateStatus.UpToDate => loc["Update_Status_UpToDate"],
            _ => loc["Update_Status_Failed"],
        };
    }

    [RelayCommand]
    private Task UpdateNowAsync() =>
        _availableUpdate is null ? Task.CompletedTask : _updatePrompter.OpenAsync(_availableUpdate);

    [RelayCommand]
    private async Task ResetDatabaseAsync()
    {
        await SafeExecuteAsync(async () =>
        {
            await _databaseService.ResetToDefaultsAsync();
            await _dialogs.ShowToastAsync(LocalizationResourceManager.Instance["Settings_Msg_DbReset"], TimeSpan.FromSeconds(3));
        }, LocalizationResourceManager.Instance["Err_ResetDatabase"]);
    }

    [RelayCommand]
    private async Task SendCrashReportTestAsync()
    {
        if (!_crashReporting.IsEnabled())
        {
            await _dialogs.AlertAsync(
                LocalizationResourceManager.Instance["Settings_Alert_CrashInactive_Title"],
                LocalizationResourceManager.Instance["Settings_Alert_CrashInactive_Message"],
                LocalizationResourceManager.Instance["Common_Ok"]);
            return;
        }

        Exception testException = new InvalidOperationException(
            $"Test crash report da impostazioni ({_environment.PlatformName}, v{_environment.VersionString})");

        _crashReporting.CaptureTestException(testException);
        await _dialogs.ShowToastAsync(LocalizationResourceManager.Instance["Settings_Msg_TestCrashSent"], TimeSpan.FromSeconds(2));
    }

    private bool CanSaveSettings() => _hasChanges;

    [RelayCommand(CanExecute = nameof(CanSaveSettings))]
    private async Task SaveSettings()
    {
        _currentSettings ??= new AppSettings();
        bool crashReportingChanged = _currentSettings.CrashReportingEnabled != CrashReportingEnabled;

        _currentSettings.ThemePreference = ThemePreference;
        _currentSettings.StartWithFavorite = StartWithFavorite;
        _currentSettings.PlaybackLatencyOffsetSeconds = PlaybackLatencyOffsetSeconds;
        _currentSettings.PreferHlsStream = PreferHlsStream;
        _currentSettings.RequestPageSize = RequestPageSize;
        _currentSettings.CrashReportingEnabled = CrashReportingEnabled;
        _currentSettings.CrashReportingConsentRequested = true;
        await _settingsRepo.SaveAsync(_currentSettings);
        _settingsStore.Set("theme_preference", ThemePreference);
        _crashReporting.SaveConsent(CrashReportingEnabled, consentRequested: true);
        _hasChanges = false;
        SaveSettingsCommand.NotifyCanExecuteChanged();

        await _dialogs.ShowToastAsync(LocalizationResourceManager.Instance["Settings_Msg_SettingsSaved"], TimeSpan.FromSeconds(2));
        if (crashReportingChanged)
        {
            await _dialogs.AlertAsync(
                LocalizationResourceManager.Instance["Settings_Alert_Restart_Title"],
                LocalizationResourceManager.Instance["Settings_Alert_Restart_Message"],
                LocalizationResourceManager.Instance["Common_Ok"]);
        }

        await _navigation.GoToOnAirAsync();
    }
}
