namespace RadioE45.Services.Platform;

/// <summary>
/// App navigation expressed as intents, so the same ViewModels drive MAUI Shell routes and the
/// Uno Frame/NavigationView. Implementations decide threading and route syntax.
/// </summary>
public interface INavigationService
{
    /// <summary>Back to the On Air root, clearing pages stacked above it.</summary>
    Task GoToOnAirAsync();

    /// <summary>Pops the current page.</summary>
    Task GoBackAsync();

    Task GoToAddStationAsync();

    Task GoToEditStationAsync(int stationId);

    Task GoToPodcastEpisodesAsync(string podcastId, string podcastTitle);

    /// <summary>Opens a page from the side menu, stacked above On Air, closing the menu first.</summary>
    Task OpenFromMenuAsync(MenuPage page);

    void CloseMenu();

    /// <summary>
    /// Pops the Podcast tab back to its list, whichever tab is active (its episodes page would
    /// show data of the previous station after a station switch).
    /// </summary>
    void ResetPodcastTabToRoot();

    /// <summary>Shows the station schedule (MAUI: bottom popup).</summary>
    Task ShowScheduleAsync();
}

public enum MenuPage
{
    Channels,
    Settings,
}
