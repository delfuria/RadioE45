using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.CrashReporting;
using RadioE45.Services;
using RadioE45.Services.Data;
using RadioE45.Services.Localization;
#if !MACCATALYST
using Sentry;
#endif

namespace RadioE45.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly IAppSettingsRepository _settingsRepo;
    private readonly IDatabaseService _databaseService;
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

#if MACCATALYST
    public bool IsCrashReportingAvailable => false;
#else
    public bool IsCrashReportingAvailable => CrashReportingConfiguration.IsConfigured;
#endif

    public SettingsViewModel(IAppSettingsRepository settingsRepo, IDatabaseService databaseService, ILogger<SettingsViewModel> logger)
    {
        Logger = logger;
        _settingsRepo = settingsRepo;
        _databaseService = databaseService;
        Title = "Impostazioni";
        AppVersion = AppInfo.VersionString;
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
        _hasChanges = false;
        SaveSettingsCommand.NotifyCanExecuteChanged();
    }

    partial void OnThemePreferenceChanged(string value)
    {
        ThemeService.Apply(value);
        MarkChanged();
    }
    
    partial void OnStartWithFavoriteChanged(bool value) => MarkChanged();

    partial void OnCrashReportingEnabledChanged(bool value) => MarkChanged();

    private void MarkChanged()
    {
        _hasChanges = true;
        SaveSettingsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task ResetDatabaseAsync()
    {
        await SafeExecuteAsync(async () =>
        {
            await _databaseService.ResetToDefaultsAsync();

#if ANDROID || IOS
            await Snackbar.Make(LocalizationResourceManager.Instance["Settings_Msg_DbReset"], duration: TimeSpan.FromSeconds(3)).Show();
#endif
        }, LocalizationResourceManager.Instance["Err_ResetDatabase"]);
    }

    [RelayCommand]
    private async Task SendCrashReportTestAsync()
    {
        if (!CrashReportingSettings.IsEnabled())
        {
            await Shell.Current.DisplayAlertAsync(
                LocalizationResourceManager.Instance["Settings_Alert_CrashInactive_Title"],
                LocalizationResourceManager.Instance["Settings_Alert_CrashInactive_Message"],
                LocalizationResourceManager.Instance["Common_Ok"]);
            return;
        }

        Exception testException = new InvalidOperationException(
            $"Test crash report da impostazioni ({DeviceInfo.Current.Platform}, v{AppInfo.VersionString})");

#if !MACCATALYST
        SentrySdk.CaptureException(testException);
#endif

#if ANDROID || IOS
        await Snackbar.Make(LocalizationResourceManager.Instance["Settings_Msg_TestCrashSent"], duration: TimeSpan.FromSeconds(2)).Show();
#endif
    }

    private bool CanSaveSettings() => _hasChanges;

    [RelayCommand(CanExecute = nameof(CanSaveSettings))]
    private async Task SaveSettings()
    {
        _currentSettings ??= new AppSettings();
        bool crashReportingChanged = _currentSettings.CrashReportingEnabled != CrashReportingEnabled;

        _currentSettings.ThemePreference = ThemePreference;
        _currentSettings.StartWithFavorite = StartWithFavorite;
        CrashReportingSettings.ApplyTo(_currentSettings, CrashReportingEnabled, consentRequested: true);
        await _settingsRepo.SaveAsync(_currentSettings);
        Preferences.Default.Set("theme_preference", ThemePreference);
        CrashReportingSettings.SaveToPreferences(CrashReportingEnabled, consentRequested: true);
        _hasChanges = false;
        SaveSettingsCommand.NotifyCanExecuteChanged();

#if ANDROID || IOS
        await Snackbar.Make(LocalizationResourceManager.Instance["Settings_Msg_SettingsSaved"], duration: TimeSpan.FromSeconds(2)).Show();
#endif
        if (crashReportingChanged)
        {
            await Shell.Current.DisplayAlertAsync(
                LocalizationResourceManager.Instance["Settings_Alert_Restart_Title"],
                LocalizationResourceManager.Instance["Settings_Alert_Restart_Message"],
                LocalizationResourceManager.Instance["Common_Ok"]);
        }

        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("//OnAirPage"));
    }
}
