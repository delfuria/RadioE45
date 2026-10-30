using CommunityToolkit.Maui;
using RadioE45.ViewModels;
using RadioE45.Views.Tv;

namespace RadioE45.Services.Platform;

public sealed class MauiNavigationService : INavigationService
{
    private readonly IPopupService _popupService;
    private readonly TvHostAccessor _tv;

    public MauiNavigationService(IPopupService popupService, TvHostAccessor tv)
    {
        _popupService = popupService;
        _tv = tv;
    }

    // On TV there is no Shell: the TV root page switches its own section (docs/ANDROID-TV-ROADMAP.md §4).
    public Task GoToOnAirAsync()
    {
        if (_tv.Current is { } tv)
        {
            tv.ShowSection(TvSection.OnAir);
            return Task.CompletedTask;
        }

        return GoToAsync("//OnAirPage");
    }

    public Task GoBackAsync()
    {
        if (_tv.Current is { } tv)
        {
            tv.GoBack();
            return Task.CompletedTask;
        }

        return GoToAsync("..");
    }

    public Task GoToAddStationAsync()
    {
        if (_tv.Current is { } tv)
        {
            tv.ShowSection(TvSection.AddStation);
            return Task.CompletedTask;
        }

        return GoToAsync("AddStationPage");
    }

    public Task GoToEditStationAsync(int stationId)
    {
        if (_tv.Current is { } tv)
        {
            tv.ShowEditStation(stationId);
            return Task.CompletedTask;
        }

        return GoToAsync($"EditStationPage?id={stationId}");
    }

    public Task GoToPodcastEpisodesAsync(string podcastId, string podcastTitle)
    {
        if (_tv.Current is { } tv)
        {
            tv.ShowPodcastEpisodes(podcastId, podcastTitle);
            return Task.CompletedTask;
        }

        return GoToAsync($"PodcastEpisodesPage?podcastId={Uri.EscapeDataString(podcastId)}&podcastTitle={Uri.EscapeDataString(podcastTitle)}");
    }

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
        if (_tv.Current is { } tv)
        {
            tv.ShowSection(TvSection.Schedule);
            return;
        }

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

    // Not on TV: the remote has no use for the technical details of the stream (onair.stream-info n/a).
    public async Task ShowStreamInfoAsync()
    {
        if (_tv.Current is not null || Shell.Current is null)
            return;

        await _popupService.ShowPopupAsync<StreamInfoViewModel>(Shell.Current, new PopupOptions
        {
            Shape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(20),
                StrokeThickness = 0
            }
        });
    }

    private static Task GoToAsync(string route) =>
        MainThread.InvokeOnMainThreadAsync(() => Shell.Current?.GoToAsync(route) ?? Task.CompletedTask);
}
