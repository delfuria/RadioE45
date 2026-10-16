#if DEBUG && RADIOE45_DESKTOP
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;
using RadioE45.Services.Radio;

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

    public static bool IsRequested => Environment.GetEnvironmentVariable("RADIOE45_SELFTEST") is "1" or "network";

    private static bool IsNetworkRun => Environment.GetEnvironmentVariable("RADIOE45_SELFTEST") == "network";

    /// <summary>
    /// <c>RADIOE45_SELFTEST=network</c> (optional <c>RADIOE45_SELFTEST_MINUTES</c>, default 8):
    /// plays the E45 stream and prints playing/buffering/network every 5 s, for long network drop
    /// tests (the network is cut from outside, e.g. <c>prlctl set … --device-set net0 --disconnect</c>).
    /// </summary>
    private static async Task RunNetworkAsync(IServiceProvider services)
    {
        var minutes = int.TryParse(Environment.GetEnvironmentVariable("RADIOE45_SELFTEST_MINUTES"), out var m) ? m : 8;
        if (!await services.GetRequiredService<IRadioRepository>().HasStationsAsync())
            await services.GetRequiredService<IDatabaseService>().SeedStationsAsync();
        var catalog = services.GetRequiredService<IAzuraStationCatalog>();
        await catalog.ReloadAsync();
        var station = catalog.Stations.FirstOrDefault(s => s.Name.Contains("E45", StringComparison.OrdinalIgnoreCase))
                      ?? catalog.Stations.First();

        var audio = services.GetRequiredService<IAudioService>();
        var network = services.GetRequiredService<INetworkMonitor>();
        network.ConnectivityChanged += (_, online) => Console.WriteLine($"NETTEST {DateTime.Now:HH:mm:ss} event ConnectivityChanged={online}");
        audio.PlaybackStateChanged += (_, playing) => Console.WriteLine($"NETTEST {DateTime.Now:HH:mm:ss} event PlaybackStateChanged={playing}");
        audio.ErrorOccurred += (_, error) => Console.WriteLine($"NETTEST {DateTime.Now:HH:mm:ss} event ErrorOccurred={error}");

        await audio.PlayAsync(station);
        var end = DateTime.UtcNow.AddMinutes(minutes);
        while (DateTime.UtcNow < end)
        {
            Console.WriteLine($"NETTEST {DateTime.Now:HH:mm:ss} playing={audio.IsPlaying} buffering={audio.IsBuffering} " +
                              $"internet={network.HasInternet} hls={audio.IsHlsActive}");
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        await audio.StopAsync();
        Console.WriteLine("NETTEST DONE");
    }

    private static async Task<bool> WaitAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                return false;
            await Task.Delay(100);
        }

        return true;
    }

    public static async Task RunAsync(IServiceProvider services)
    {
        if (IsNetworkRun)
        {
            await RunNetworkAsync(services);
            return;
        }

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
        // Generous deadline: right after launch the UI thread can be busy for a while (cold start).
        var ticked = await WaitAsync(() => ticks >= 3, TimeSpan.FromSeconds(2));
        timer?.Stop();
        var afterStop = ticks;
        await Task.Delay(250);
        Report("timer", ticked && ticks == afterStop, $"{afterStop} ticks at 100 ms, none after Stop");

        theme.Apply("Light");
        var light = host.Root?.ActualTheme;
        theme.Apply("Dark");
        var dark = host.Root?.ActualTheme;
        Report("theme", light == ElementTheme.Light && dark == ElementTheme.Dark, $"Light→{light}, Dark→{dark}");
        theme.Apply(settings.Get(UnoThemeService.PreferenceKey, UnoThemeService.DefaultPreference));

        await navigation.GoToOnAirAsync();
        await navigation.GoBackAsync();
        await navigation.ShowScheduleAsync();
        services.GetRequiredService<UnoNavigationService>().CloseSchedule();
        Report("navigation", true, "GoToOnAir, GoBack, ShowSchedule + close without exceptions");

        // S7a: paths, database, station catalog, network monitor and live audio.
        var paths = services.GetRequiredService<IAppPaths>();
        Report("paths", Directory.Exists(paths.AppDataDirectory), paths.AppDataDirectory);
        var network = services.GetRequiredService<INetworkMonitor>();
        Report("network", network.HasInternet, $"HasInternet={network.HasInternet}");

        AzuraStation? station = null;
        try
        {
            if (!await services.GetRequiredService<IRadioRepository>().HasStationsAsync())
                await services.GetRequiredService<IDatabaseService>().SeedStationsAsync();
            var catalog = services.GetRequiredService<IAzuraStationCatalog>();
            await catalog.ReloadAsync();
            station = catalog.Stations.FirstOrDefault(s => s.Name.Contains("E45", StringComparison.OrdinalIgnoreCase))
                      ?? catalog.Stations.FirstOrDefault();
            Report("database+catalog", station is not null, $"{catalog.Stations.Count} stations, using '{station?.Name}'");
        }
        catch (Exception ex)
        {
            Report("database+catalog", false, $"{ex.GetType().Name}: {ex.Message}");
        }

        if (station is not null)
        {
            var audio = services.GetRequiredService<IAudioService>();
            var started = DateTime.UtcNow;
            await audio.PlayAsync(station);
            var playing = await WaitAsync(() => audio.IsPlaying, TimeSpan.FromSeconds(20));
            Report("audio.play", playing,
                $"playing after {(DateTime.UtcNow - started).TotalSeconds:F1} s, HLS={audio.IsHlsActive}");
            await Task.Delay(TimeSpan.FromSeconds(5));
            Report("audio.steady", audio.IsPlaying, "still playing after 5 s");

            await audio.PauseAsync();
            var paused = await WaitAsync(() => !audio.IsPlaying, TimeSpan.FromSeconds(3));
            Report("audio.pause", paused && audio.CurrentStation is not null, "live pause closes the stream, station kept");

            await audio.ResumeAsync();
            Report("audio.resume", await WaitAsync(() => audio.IsPlaying, TimeSpan.FromSeconds(20)), "reopened the live stream");

            await audio.StopAsync();
            Report("audio.stop", await WaitAsync(() => !audio.IsPlaying, TimeSpan.FromSeconds(3)), $"station after stop: {audio.CurrentStation?.Name ?? "none"}");
        }

        await PodcastAsync(services, Report);

        Console.WriteLine($"SELFTEST DONE failures={failures}");
    }

    // S7b: first episode with media of the first station that has podcasts.
    private static async Task PodcastAsync(IServiceProvider services, Action<string, bool, string> report)
    {
        var catalog = services.GetRequiredService<IAzuraStationCatalog>();
        var podcasts = services.GetRequiredService<IPodcastService>();
        AzuraStation? station = null;
        AzuraCastPodcastEpisode? episode = null;
        foreach (var candidate in catalog.Stations)
        {
            try
            {
                foreach (var podcast in await podcasts.GetPodcastsAsync(candidate))
                {
                    episode = (await podcasts.GetEpisodesAsync(candidate, podcast)).FirstOrDefault(e => e.MediaUrl.Length > 0);
                    if (episode is not null)
                    {
                        station = candidate;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SELFTEST info podcasts of '{candidate.Name}': {ex.GetType().Name} {ex.Message}");
            }

            if (episode is not null)
                break;
        }

        if (station is null || episode is null)
        {
            Console.WriteLine("SELFTEST SKIP podcast: no station in the catalog has an episode with media");
            return;
        }

        // Real use: the user switches from the live radio to an episode (PlayAsync stops the radio).
        var radio = services.GetRequiredService<IAudioService>();
        var radioStation = catalog.Stations.FirstOrDefault(s => s.Name.Contains("E45", StringComparison.OrdinalIgnoreCase));
        if (radioStation is not null)
        {
            await radio.PlayAsync(radioStation);
            report("podcast.radio-before", await WaitAsync(() => radio.IsPlaying, TimeSpan.FromSeconds(20)), "radio playing before the episode");
        }

        var player = services.GetRequiredService<IPodcastPlayerService>();
        await player.PlayAsync(station, episode, 0);
        report("podcast.radio-stopped", !radio.IsPlaying, "starting the episode stopped the radio");
        var playing = await WaitAsync(() => player.IsPlaying, TimeSpan.FromSeconds(20));
        report("podcast.play", playing, $"'{episode.Title}' on '{station.Name}', duration {player.Duration:mm\\:ss}");
        if (!playing)
            return;

        var start = player.Position;
        await Task.Delay(TimeSpan.FromSeconds(4));
        report("podcast.position", player.Position > start + TimeSpan.FromSeconds(2), $"{start:mm\\:ss} → {player.Position:mm\\:ss}");

        await player.PauseAsync();
        var paused = await WaitAsync(() => !player.IsPlaying, TimeSpan.FromSeconds(3));
        var pausedAt = player.Position;
        await Task.Delay(TimeSpan.FromSeconds(2));
        report("podcast.pause", paused && (player.Position - pausedAt).Duration() < TimeSpan.FromSeconds(1),
            $"real pause at {pausedAt:mm\\:ss}, position held");

        // Half way (the demo episodes can be shorter than a minute), at least 8 s so that the
        // saved position counts as resumable (> 3 s).
        var target = TimeSpan.FromSeconds(Math.Max(8, Math.Min(60, player.Duration.TotalSeconds / 2)));
        await player.SeekAsync(target);
        await player.ResumeAsync();
        var resumed = await WaitAsync(() => player.IsPlaying, TimeSpan.FromSeconds(10));
        await Task.Delay(TimeSpan.FromSeconds(1));
        report("podcast.seek+resume", resumed && player.Position >= target,
            $"resumed at {player.Position:mm\\:ss} after seek to {target:mm\\:ss}");

        await player.StopAsync(); // saves the position
        var saved = await player.GetResumePositionSecondsAsync(station, episode);
        report("podcast.saved-position", saved >= (int)target.TotalSeconds, $"resume position {saved} s");

        // Start again from the saved position: muted until the seek, then audible.
        await player.PlayAsync(station, episode, saved);
        var again = await WaitAsync(() => player.IsPlaying && player.Position >= TimeSpan.FromSeconds(saved - 2), TimeSpan.FromSeconds(20));
        report("podcast.resume-from-saved", again, $"playing={player.IsPlaying} position {player.Position:mm\\:ss} (saved {saved} s)");
        await player.StopAsync();

        // End of episode: start 5 s before the end, wait for EpisodeCompleted; the episode is then
        // saved as completed, so it would start over from 0.
        var completed = new TaskCompletionSource();
        void OnCompleted(object? sender, EventArgs e) => completed.TrySetResult();
        player.EpisodeCompleted += OnCompleted;
        var duration = player.Duration > TimeSpan.Zero ? player.Duration : TimeSpan.FromSeconds(27);
        await player.PlayAsync(station, episode, (int)Math.Max(4, duration.TotalSeconds - 5));
        var ended = await Task.WhenAny(completed.Task, Task.Delay(TimeSpan.FromSeconds(30))) == completed.Task;
        player.EpisodeCompleted -= OnCompleted;
        var resumeAfterEnd = await player.GetResumePositionSecondsAsync(station, episode);
        report("podcast.end", ended && resumeAfterEnd == 0 && player.CurrentEpisode is null,
            $"EpisodeCompleted={ended}, resume position after the end {resumeAfterEnd} s");
    }
}
#endif
