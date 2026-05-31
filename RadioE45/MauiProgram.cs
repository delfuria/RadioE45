using SkiaSharp.Views.Maui.Controls.Hosting;
#if WINDOWS
using Microsoft.Maui.LifecycleEvents;
#endif

namespace RadioE45;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkitMediaElement()
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if WINDOWS
        builder.ConfigureLifecycleEvents(lifecycle =>
        {
            lifecycle.AddWindows(windows =>
            {
                windows.OnWindowCreated(window =>
                {
                    var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "appicon.ico");
                    if (System.IO.File.Exists(iconPath))
                    {
                        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
                        appWindow.SetIcon(iconPath);
                    }
                });
            });
        });
#endif

#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.Services.AddSingleton<MainViewModel>();

        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<RadioPage>();


        return builder.Build();
    }
}