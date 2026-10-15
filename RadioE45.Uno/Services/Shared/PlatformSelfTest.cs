#if DEBUG && RADIOE45_DESKTOP
using RadioE45.Services.Platform;

namespace RadioE45.Services.Shared;

/// <summary>
/// DEBUG-only scripted check of the shared platform services, for runs without a mouse (e.g. the
/// Linux VM over SSH): <c>RADIOE45_SELFTEST=1 dotnet run --project RadioE45.Uno -f net10.0-desktop</c>.
/// Writes one "SELFTEST" line per check to stdout and exits. Dialogs and the URL launcher need a
/// person and are checked on the Debug page.
/// </summary>
public static class PlatformSelfTest
{
    private const string RunCountKey = "selftest_run_count";

    public static bool IsRequested => Environment.GetEnvironmentVariable("RADIOE45_SELFTEST") == "1";

    public static async Task RunAsync(IServiceProvider services)
    {
        var dispatcher = services.GetRequiredService<IUiDispatcher>();
        var settings = services.GetRequiredService<ISettingsStore>();
        var environment = services.GetRequiredService<IAppEnvironment>();
        var theme = services.GetRequiredService<IThemeService>();
        var navigation = services.GetRequiredService<INavigationService>();
        var host = services.GetRequiredService<UnoWindowHost>();
        var failures = 0;

        void Report(string check, bool ok, string detail)
        {
            if (!ok) failures++;
            Console.WriteLine($"SELFTEST {(ok ? "OK  " : "FAIL")} {check}: {detail}");
        }

        Report("environment", environment.VersionString.Length > 0,
            $"{environment.VersionString} build {environment.BuildString} {environment.CommitId} {environment.PlatformName}");

        // Persists across runs: the second run must print previous + 1.
        var runs = settings.Get(RunCountKey, 0) + 1;
        settings.Set(RunCountKey, runs);
        Report("settings", settings.Get(RunCountKey, 0) == runs && settings.ContainsKey(RunCountKey),
            $"run #{runs} (persisted in LocalSettings)");
        settings.Set("selftest_double", 0.25);
        Report("settings.double", Math.Abs(settings.Get("selftest_double", 1.0) - 0.25) < 1e-9, "0.25 round-trip");
        settings.Remove("selftest_double");
        Report("settings.remove", !settings.ContainsKey("selftest_double"), "key removed");

        Report("dispatcher.ui", dispatcher.IsUiThread, "IsUiThread on the UI thread");
        var (background, inside) = await Task.Run(async () =>
        {
            var before = dispatcher.IsUiThread;
            var onUi = false;
            await dispatcher.InvokeAsync(() => onUi = dispatcher.IsUiThread);
            return (before, onUi);
        });
        Report("dispatcher.invoke", !background && inside, $"background IsUiThread={background}, inside InvokeAsync={inside}");

        var ticks = 0;
        var timer = dispatcher.CreateTimer(TimeSpan.FromMilliseconds(100), () => ticks++);
        timer?.Start();
        await Task.Delay(550);
        timer?.Stop();
        var afterStop = ticks;
        await Task.Delay(250);
        Report("timer", ticks >= 3 && ticks == afterStop, $"{afterStop} ticks in 550 ms, none after Stop");

        theme.Apply("Light");
        var light = host.Root?.ActualTheme;
        theme.Apply("Dark");
        var dark = host.Root?.ActualTheme;
        Report("theme", light == ElementTheme.Light && dark == ElementTheme.Dark, $"Light→{light}, Dark→{dark}");
        theme.Apply(settings.Get(UnoThemeService.PreferenceKey, UnoThemeService.DefaultPreference));

        await navigation.GoToOnAirAsync();
        await navigation.GoBackAsync();
        await navigation.ShowScheduleAsync(); // not ported yet: must only log a warning
        Report("navigation", true, "GoToOnAir, GoBack, ShowSchedule (not ported) without exceptions");

        Console.WriteLine($"SELFTEST DONE failures={failures}");
    }
}
#endif
