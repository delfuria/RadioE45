using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;
using RadioE45.Services.Legal;
using RadioE45.Services.Localization;
using RadioE45.Services.Platform;

namespace RadioE45.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IAppSettingsRepository _settingsRepo;
    private readonly IDatabaseService _databaseService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ISettingsStore _settingsStore;
    private readonly IThemeService _themeService;
    private readonly ICrashReportingService _crashReporting;
    private readonly IAppEnvironment _environment;
    private readonly ITermsService _terms;
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
        Title = "Impostazioni";
        AppVersion = $"{environment.VersionString} ({environment.BuildString}, {environment.CommitId})";
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        _currentSettings = await _settingsRepo.GetAsync();
        ThemePreference = _currentSettings.ThemePreference;
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
