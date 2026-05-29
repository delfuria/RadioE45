using SkiaSharp.Views.Maui.Controls.Hosting;

namespace RadioE45;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
#if MACCATALYST
            .UseMauiCommunityToolkitMediaElement()
#endif
            .UseMauiCommunityToolkit()
            .UseSkiaSharp()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
#if DEBUG
        builder.Logging.AddDebug();
#endif
        builder.Services.AddSingleton<MainViewModel>();

        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<RadioPage>();


        return builder.Build();
    }
}