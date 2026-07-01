using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Core.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;
using RadioE45.Services.Audio;
using RadioE45.Services.CrashReporting;
using RadioE45.Services.Data;
using RadioE45.Services.Logging;
using RadioE45.Services.Radio;
using RadioE45.ViewModels;
using RadioE45.Views;
using Refit;
#if !MACCATALYST
using Sentry;
#endif
#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
#endif

namespace RadioE45;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false);

#if !MACCATALYST
        if (CrashReportingConfiguration.IsConfigured
            && CrashReportingSettings.IsEnabled())
        {
            builder.UseSentry(options =>
            {
                options.Dsn = CrashReportingConfiguration.Dsn;
                options.Debug = false;
                options.SendDefaultPii = false;
                options.AttachScreenshot = false;
                options.AutoSessionTracking = true;
                options.SetBeforeSend((@event, hint) =>
                {
                    string dbPath = DatabaseService.GetDatabasePath();
                    if (File.Exists(dbPath))
                    {
                        hint.AddAttachment(dbPath, AttachmentType.Default, "application/vnd.sqlite3");
                    }

                    return @event;
                });
            });
        }
#endif

        builder.ConfigureLifecycleEvents(events =>
        {
#if WINDOWS
            events.AddWindows(windows => windows.OnWindowCreated(window =>
            {
                IntPtr hWnd = WindowNative.GetWindowHandle(window);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

                // Window size is set in App.xaml.cs CreateWindow() using MAUI logical pixels,
                // which are DPI-aware — no resize needed here.
                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsResizable = false;
                    presenter.IsMaximizable = false;
                }
            }));
#endif
        });

#if WINDOWS
        // RealTimePlayback tells Windows not to build a large seek buffer —
        // correct behaviour for a live radio stream where seeking makes no sense.
        // MediaManager is internal; reach the native player via the private field on MauiMediaElement.
        MediaElementHandler.PropertyMapper.AppendToMapping("LiveStreamOptimization", (handler, view) =>
        {
            var pv = handler.PlatformView;
            if (pv is null) return;

            var field = pv.GetType().GetField("mediaPlayerElement",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field?.GetValue(pv) is Microsoft.UI.Xaml.Controls.MediaPlayerElement playerElement
                && playerElement.MediaPlayer is { } mp)
            {
                mp.RealTimePlayback = true;
            }
        });
#endif

        // HTTP client for AzuraCast (base URL set dynamically per-station)
        builder.Services.AddHttpClient("AzuraCast")
            .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(3));

        // Singletons — survive navigation
        builder.Services.AddSingleton<IStreamUrlProber, StreamUrlProber>();
        builder.Services.AddSingleton<IAudioService, AudioService>();
        builder.Services.AddSingleton<RemoteArtworkLoader>();
 #if IOS || MACCATALYST
        builder.Services.AddSingleton<IPlatformNowPlayingService, IosNowPlayingService>();
 #elif ANDROID
        builder.Services.AddSingleton<IPlatformNowPlayingService, AndroidNowPlayingService>();
 #else
        builder.Services.AddSingleton<IPlatformNowPlayingService, NullPlatformNowPlayingService>();
 #endif
 #if ANDROID
        builder.Services.AddSingleton<IAudioFocusManager, AudioFocusManager>();
 #else
        builder.Services.AddSingleton<IAudioFocusManager, NullAudioFocusManager>();
 #endif
        builder.Services.AddSingleton<INowPlayingService, NowPlayingService>();
        builder.Services.AddSingleton<IStationDetailService, StationDetailService>();
        builder.Services.AddTransient<IStationListService, StationListService>();
        builder.Services.AddSingleton<IAzuraStationCatalog, AzuraStationCatalog>();
        builder.Services.AddTransient<IScheduleService, ScheduleService>();
        builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
        builder.Services.AddSingleton<IRadioRepository, RadioRepository>();
        builder.Services.AddSingleton<IDbVersionRepository, DbVersionRepository>();
        builder.Services.AddSingleton<IAppSettingsRepository, AppSettingsRepository>();
        builder.Services.AddSingleton<ILogRepository, LogRepository>();

        // OnAirViewModel is Singleton so RadioListViewModel can reference it and share state
        builder.Services.AddSingleton<OnAirViewModel>();

        // Other ViewModels as Transient
        builder.Services.AddTransient<RadioListViewModel>();
        builder.Services.AddTransient<AddStationViewModel>();
        builder.Services.AddTransient<ScheduleViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Views as Transient
        builder.Services.AddTransient<OnAirPage>();
        builder.Services.AddTransient<AddStationPage>();
        builder.Services.AddTransient<RadioListPage>();
        builder.Services.AddTransient<SchedulePage>();
        builder.Services.AddTransient<SettingsPage>();

        DatabaseLoggerProvider dbLoggerProvider = new();
        builder.Services.AddSingleton(dbLoggerProvider);

#if WINDOWS
        builder.Logging.AddDebug();
#else
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new ConsoleLoggerProvider());
#endif
        builder.Logging.AddProvider(dbLoggerProvider);

#if DEBUG
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#else
        builder.Logging.SetMinimumLevel(LogLevel.Information);
#endif
        return builder.Build();
    }
}
