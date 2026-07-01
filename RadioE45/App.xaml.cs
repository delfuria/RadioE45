using RadioE45.Models;
using RadioE45.Services.CrashReporting;
using RadioE45.Services;
using RadioE45.Services.Data;
using RadioE45.Services.Logging;
using RadioE45.Services.Radio;

namespace RadioE45;

public partial class App : Application
{
    internal const double PortraitWidth = 500;
    internal const double PortraitHeight = 950;
    internal const double LandscapeWidth = 900;
    internal const double LandscapeHeight = 500;
    private readonly IAppSettingsRepository _settingsRepo;
    private readonly IRadioRepository _radioRepository;

    public App(IAppSettingsRepository settingsRepo, ILogRepository logRepo, DatabaseLoggerProvider dbLoggerProvider, IAzuraStationCatalog stationCatalog, IRadioRepository radioRepository)
    {
        InitializeComponent();
        _settingsRepo = settingsRepo;
        _radioRepository = radioRepository;

        var pref = Preferences.Default.Get("theme_preference", "Dark");
        ThemeService.Apply(pref);

        _ = InitializeDbLoggingAsync(settingsRepo, logRepo, dbLoggerProvider);
        _ = stationCatalog.LoadAsync();

        RequestedThemeChanged += (_, _) =>
        {
            if (Preferences.Default.Get("theme_preference", "Dark") == "System")
                ThemeService.Apply("System");
        };
    }

    private static async Task InitializeDbLoggingAsync(
        IAppSettingsRepository settingsRepo,
        ILogRepository logRepo,
        DatabaseLoggerProvider dbLoggerProvider)
    {
        AppSettings settings = await settingsRepo.GetAsync();
        if (!settings.CrashReportingEnabled)
            return;

        await logRepo.TrimToLastAsync(1000);
        dbLoggerProvider.Enable(logRepo);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window = new(new AppShell(_radioRepository));

        DevicePlatform platform = DeviceInfo.Current.Platform;
        if (platform == DevicePlatform.WinUI || platform == DevicePlatform.MacCatalyst)
        {
            Enum.TryParse(Preferences.Default.Get("desktop_orientation", "Portrait"), out DesktopOrientationMode orientation);
            bool isLandscape = orientation == DesktopOrientationMode.Landscape;
            double w = isLandscape ? LandscapeWidth : PortraitWidth;
            double h = isLandscape ? LandscapeHeight : PortraitHeight;
            window.Width = w;
            window.Height = h;
            window.MinimumWidth = w;
            window.MaximumWidth = w;
            window.MinimumHeight = h;
            window.MaximumHeight = h;
        }

        if (CrashReportingConfiguration.IsConfigured
            && !CrashReportingSettings.HasRequestedConsent())
        {
            window.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(750), () => _ = RequestCrashReportingConsentAsync());
        }

        return window;
    }

    private async Task RequestCrashReportingConsentAsync()
    {
        Page? hostPage = await GetPromptHostPageAsync();
        if (hostPage is null)
            return;
        /*
        //TODO: Enable it when in production
        bool enabled = await hostPage.DisplayAlertAsync(
            "Segnalazione crash",
            "Vuoi inviare automaticamente allo sviluppatore i dati dei crash dell'app per aiutare la diagnosi dei problemi?",
            "Consenti",
            "Non inviare");

        await SaveCrashReportingPreferenceAsync(enabled, consentRequested: true);

        if (enabled)
        {
            await hostPage.DisplayAlertAsync(
                "Riavvio richiesto",
                "La raccolta dei crash sara' attiva dal prossimo avvio dell'app.",
                "OK");
        }
        */
    }

    private async Task SaveCrashReportingPreferenceAsync(bool enabled, bool consentRequested)
    {
        AppSettings settings = await _settingsRepo.GetAsync();
        CrashReportingSettings.ApplyTo(settings, enabled, consentRequested);
        await _settingsRepo.SaveAsync(settings);
        CrashReportingSettings.SaveToPreferences(enabled, consentRequested);
    }

    private static async Task<Page?> GetPromptHostPageAsync()
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Page? page = Shell.Current ?? Current?.Windows.FirstOrDefault()?.Page;
            if (page is not null)
                return page;
            await Task.Delay(250);
        }
        return null;
    }
}
