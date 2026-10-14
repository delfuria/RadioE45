using RadioE45.Services.Platform;
using RadioE45.Views;

namespace RadioE45.Services.Shared;

/// <summary>
/// <see cref="INavigationService"/> on the Uno shell: a NavigationView whose tabs (On Air,
/// requests, podcasts) drive a Frame (docs/LINUX-UNO-ROADMAP.md §8, D6: hand-written, no
/// Uno.Extensions.Navigation). Same for desktop and web.
/// Pages not ported yet (S8.x) are logged and ignored, so the shared ViewModels can already
/// call every intent.
/// </summary>
public sealed class UnoNavigationService(ILogger<UnoNavigationService> logger) : INavigationService
{
    private ShellPage? _shell;

    /// <summary>Called once by the shell when it is created.</summary>
    public void Attach(ShellPage shell) => _shell = shell;

    public Task GoToOnAirAsync()
    {
        _shell?.SelectTab(ShellTab.OnAir, clearHistory: true);
        return Task.CompletedTask;
    }

    public Task GoBackAsync()
    {
        _shell?.GoBack();
        return Task.CompletedTask;
    }

    public Task GoToAddStationAsync() => NotPorted(nameof(GoToAddStationAsync));

    public Task GoToEditStationAsync(int stationId) => NotPorted(nameof(GoToEditStationAsync));

    public Task GoToPodcastEpisodesAsync(string podcastId, string podcastTitle) =>
        NotPorted(nameof(GoToPodcastEpisodesAsync));

    public Task OpenFromMenuAsync(MenuPage page) => NotPorted($"{nameof(OpenFromMenuAsync)}({page})");

    public void CloseMenu()
    {
        // The side menu is ported in S8.3.
    }

    public void ResetPodcastTabToRoot()
    {
        // PodcastEpisodesPage is ported in S8.6; until then the Podcast tab has no stack.
    }

    public Task ShowScheduleAsync() => NotPorted(nameof(ShowScheduleAsync));

    private Task NotPorted(string intent)
    {
        logger.LogWarning("Navigation {Intent} not available yet in the Uno app (page not ported)", intent);
        return Task.CompletedTask;
    }
}
