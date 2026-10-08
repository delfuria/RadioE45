using CommunityToolkit.Maui;
using RadioE45.ViewModels;

namespace RadioE45.Services.Platform;

public sealed class MauiNavigationService : INavigationService
{
    private readonly IPopupService _popupService;

    public MauiNavigationService(IPopupService popupService)
    {
        _popupService = popupService;
    }

    public Task GoToOnAirAsync() => GoToAsync("//OnAirPage");

    public Task GoBackAsync() => GoToAsync("..");

    public Task GoToAddStationAsync() => GoToAsync("AddStationPage");

    public Task GoToEditStationAsync(int stationId) => GoToAsync($"EditStationPage?id={stationId}");

    public Task GoToPodcastEpisodesAsync(string podcastId, string podcastTitle) =>
        GoToAsync($"PodcastEpisodesPage?podcastId={Uri.EscapeDataString(podcastId)}&podcastTitle={Uri.EscapeDataString(podcastTitle)}");

    // Canali e Impostazioni vengono sempre impilate sopra il tab OnAir: così il "//OnAirPage"
    // con cui quelle pagine tornano indietro svuota lo stack in modo pulito.
    public Task OpenFromMenuAsync(MenuPage page)
    {
        CloseMenu();
        string route = page switch
        {
            MenuPage.Channels => "RadioListPage",
            MenuPage.Settings => "SettingsPage",
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, null),
        };
        return GoToAsync($"//OnAirPage/{route}");
    }

    public void CloseMenu()
    {
        if (Shell.Current is Shell shell)
            shell.FlyoutIsPresented = false;
    }

    // Si opera direttamente sulla ShellSection del tab Podcast (per Route, non su Shell.Current
    // "corrente") così funziona indipendentemente dal tab attivo in quel momento — GoToAsync("..")
    // relativo sarebbe ambiguo/rischioso da qui.
    public void ResetPodcastTabToRoot()
    {
        Shell? shell = Shell.Current;
        if (shell is null)
            return;

        foreach (ShellItem item in shell.Items)
        {
            foreach (ShellSection section in item.Items)
            {
                foreach (ShellContent content in section.Items)
                {
                    if (content.Route != "PodcastListPage")
                        continue;

                    if (section.Navigation.NavigationStack.Count > 1)
                        _ = section.Navigation.PopToRootAsync(false);

                    return;
                }
            }
        }
    }

    public async Task ShowScheduleAsync()
    {
        if (Shell.Current is null)
            return;

        await _popupService.ShowPopupAsync<ScheduleViewModel>(Shell.Current, new PopupOptions
        {
            Shape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(20, 20, 0, 0),
                StrokeThickness = 0
            },
            Shadow = null
        });
    }

    private static Task GoToAsync(string route) =>
        MainThread.InvokeOnMainThreadAsync(() => Shell.Current?.GoToAsync(route) ?? Task.CompletedTask);
}
