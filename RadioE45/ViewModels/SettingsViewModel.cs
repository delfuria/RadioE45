using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.CrashReporting;
using RadioE45.Services;
using RadioE45.Services.Data;
#if !MACCATALYST
using Sentry;
#endif

namespace RadioE45.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly OnAirViewModel _onAirViewModel;
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
    public partial double Volume { get; set; } = 1.0;

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
    public partial DesktopOrientationMode DesktopOrientation { get; set; } = DesktopOrientationMode.Portrait;

    public IReadOnlyList<string> OrientationLabels { get; } = ["Verticale", "Orizzontale"];

    public int DesktopOrientationIndex
    {
        get => (int)DesktopOrientation;
        set => DesktopOrientation = (DesktopOrientationMode)value;
    }

#if MACCATALYST
    public bool IsCrashReportingAvailable => false;
#else
    public bool IsCrashReportingAvailable => CrashReportingConfiguration.IsConfigured;
#endif

    public SettingsViewModel(OnAirViewModel onAirViewModel, IAppSettingsRepository settingsRepo, IDatabaseService databaseService, ILogger<SettingsViewModel> logger)
    {
        Logger = logger;
        _onAirViewModel = onAirViewModel;
        _settingsRepo = settingsRepo;
        _databaseService = databaseService;
        Title = "Impostazioni";
        AppVersion = AppInfo.VersionString;
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        _currentSettings = await _settingsRepo.GetAsync();
        // Volume: Preferences.Default è la fonte aggiornata in tempo reale da SetVolumeCommand
        Volume = Preferences.Default.Get("player_volume", _currentSettings.Volume);
        ThemePreference = _currentSettings.ThemePreference;
        SeedVersion = (float) _currentSettings.SeedVersion;
        MustUpdate = _currentSettings.MustUpdate;
        StartWithFavorite = _currentSettings.StartWithFavorite;
        CrashReportingEnabled = _currentSettings.CrashReportingEnabled;
        Enum.TryParse(_currentSettings.DesktopOrientation, out DesktopOrientationMode orientation);
        DesktopOrientation = orientation;
        _hasChanges = false;
        SaveSettingsCommand.NotifyCanExecuteChanged();
    }

    partial void OnVolumeChanged(double value)
    {
        _onAirViewModel.SetVolumeCommand.Execute(value);
        MarkChanged();
    }

    partial void OnThemePreferenceChanged(string value)
    {
        ThemeService.Apply(value);
        MarkChanged();
    }
    
    partial void OnStartWithFavoriteChanged(bool value) => MarkChanged();

    partial void OnCrashReportingEnabledChanged(bool value) => MarkChanged();

    partial void OnDesktopOrientationChanged(DesktopOrientationMode value)
    {
        OnPropertyChanged(nameof(DesktopOrientationIndex));
        MarkChanged();
    }

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
            await Snackbar.Make("Database ripristinato ai valori di default", duration: TimeSpan.FromSeconds(3)).Show();
#endif
        }, "Reset database");
    }

    [RelayCommand]
    private async Task SendCrashReportTestAsync()
    {
        if (!CrashReportingSettings.IsEnabled())
        {
            await Shell.Current.DisplayAlertAsync(
                "Crash report non attivo",
                "Attiva il crash reporting, salva le impostazioni e riavvia l'app prima di inviare un test.",
                "OK");
            return;
        }

        Exception testException = new InvalidOperationException(
            $"Test crash report da impostazioni ({DeviceInfo.Current.Platform}, v{AppInfo.VersionString})");

#if !MACCATALYST
        SentrySdk.CaptureException(testException);
#endif

#if ANDROID || IOS
        await Snackbar.Make("Crash report di test inviato", duration: TimeSpan.FromSeconds(2)).Show();
#endif
    }

    private static void ApplyDesktopOrientation(DesktopOrientationMode orientation)
    {
        DevicePlatform p = DeviceInfo.Current.Platform;
        if (p != DevicePlatform.WinUI && p != DevicePlatform.MacCatalyst) return;
        if (Application.Current?.Windows.FirstOrDefault() is not { } win) return;

        bool landscape = orientation == DesktopOrientationMode.Landscape;
        double w = landscape ? App.LandscapeWidth : App.PortraitWidth;
        double h = landscape ? App.LandscapeHeight : App.PortraitHeight;
        win.MinimumWidth  = w;
        win.MaximumWidth  = w;
        win.MinimumHeight = h;
        win.MaximumHeight = h;
        win.Width  = w;
        win.Height = h;
    }

    private bool CanSaveSettings() => _hasChanges;

    [RelayCommand(CanExecute = nameof(CanSaveSettings))]
    private async Task SaveSettings()
    {
        _currentSettings ??= new AppSettings();
        bool crashReportingChanged = _currentSettings.CrashReportingEnabled != CrashReportingEnabled;
        bool orientationChanged = _currentSettings.DesktopOrientation != DesktopOrientation.ToString();

        _currentSettings.Volume = Volume;
        _currentSettings.ThemePreference = ThemePreference;
        _currentSettings.StartWithFavorite = StartWithFavorite;
        _currentSettings.DesktopOrientation = DesktopOrientation.ToString();
        CrashReportingSettings.ApplyTo(_currentSettings, CrashReportingEnabled, consentRequested: true);
        await _settingsRepo.SaveAsync(_currentSettings);
        Preferences.Default.Set("player_volume", Volume);
        Preferences.Default.Set("theme_preference", ThemePreference);
        Preferences.Default.Set("desktop_orientation", DesktopOrientation.ToString());
        CrashReportingSettings.SaveToPreferences(CrashReportingEnabled, consentRequested: true);
        _hasChanges = false;
        SaveSettingsCommand.NotifyCanExecuteChanged();

#if ANDROID || IOS
        await Snackbar.Make("Impostazioni salvate", duration: TimeSpan.FromSeconds(2)).Show();
#endif
        if (crashReportingChanged)
        {
            await Shell.Current.DisplayAlertAsync(
                "Riavvio richiesto",
                "La modifica all'invio dei crash verra' applicata al prossimo avvio dell'app.",
                "OK");
        }
        if (orientationChanged)
            ApplyDesktopOrientation(DesktopOrientation);

        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync("//OnAirPage"));
    }
}
