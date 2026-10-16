using RadioE45.Services.Platform;
using RadioE45.Views;

namespace RadioE45.Services.Shared;

/// <summary>
/// <see cref="INavigationService"/> on the Uno shell: a NavigationView whose tabs (On Air,
/// requests, podcasts) drive a Frame (docs/LINUX-UNO-ROADMAP.md §8, D6: hand-written, no
/// Uno.Extensions.Navigation). Same for desktop and web.
/// </summary>
public sealed class UnoNavigationService : INavigationService
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

    public Task GoToAddStationAsync()
    {
        _shell?.Push(typeof(AddStationPage));
        return Task.CompletedTask;
    }

    public Task GoToEditStationAsync(int stationId)
    {
        _shell?.Push(typeof(EditStationPage), stationId);
        return Task.CompletedTask;
    }

    // Stacked above the Podcast tab (MAUI: "PodcastEpisodesPage?podcastId=…&podcastTitle=…").
    public Task GoToPodcastEpisodesAsync(string podcastId, string podcastTitle)
    {
        _shell?.Push(typeof(PodcastEpisodesPage), new PodcastEpisodesParameter(podcastId, podcastTitle));
        return Task.CompletedTask;
    }

    // Like MAUI, menu pages are always stacked above the On Air tab.
    public Task OpenFromMenuAsync(MenuPage page)
    {
        CloseMenu();
        var pageType = page switch
        {
            MenuPage.Settings => typeof(SettingsPage),
            _ => typeof(RadioListPage),
        };
        _shell?.Push(pageType, clearHistory: true);
        return Task.CompletedTask;
    }

    /// <summary>Opens the side menu (Controls/MenuButton; MAUI: Shell.FlyoutIsPresented).</summary>
    public void OpenMenu() => _shell?.OpenMenu();

    public void CloseMenu() => _shell?.CloseMenu();

    // Nothing to do: the tabs share one Frame and a tab change clears its history, so the
    // episodes of the previous station are never kept above the Podcast tab (in MAUI each tab
    // keeps its own stack, which is emptied here when the station changes).
    public void ResetPodcastTabToRoot()
    {
    }

    // MAUI: SchedulePopup through IPopupService; here a sheet of the shell. Like the MAUI popup
    // service, it returns once the sheet is shown.
    public Task ShowScheduleAsync()
    {
        _shell?.ShowSheet(new ScheduleDialog());
        return Task.CompletedTask;
    }

    public void CloseSchedule() => _shell?.CloseSheet();
}
