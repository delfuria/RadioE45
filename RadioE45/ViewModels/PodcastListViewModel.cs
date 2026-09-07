using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Localization;
using RadioE45.Services.Radio;

namespace RadioE45.ViewModels;

public partial class PodcastListViewModel : BaseViewModel
{
    private readonly IPodcastService _podcastService;
    private readonly OnAirViewModel _onAirViewModel;

    [ObservableProperty]
    public partial ObservableCollection<AzuraCastPodcast> Podcasts { get; set; } = [];

    public PodcastListViewModel(IPodcastService podcastService, OnAirViewModel onAirViewModel, ILogger<PodcastListViewModel> logger)
    {
        Logger = logger;
        _podcastService = podcastService;
        _onAirViewModel = onAirViewModel;
        Title = LocalizationResourceManager.Instance["Tab_Podcasts"];

        // La pagina è un tab (istanza unica, sempre viva) — segue i cambi di stazione anche
        // se non è quella attualmente visibile, così la lista è già pronta al prossimo OnAppearing.
        _onAirViewModel.PropertyChanged += OnOnAirPropertyChanged;
    }

    private void OnOnAirPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OnAirViewModel.CurrentStation))
            _ = LoadPodcastsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task LoadPodcastsAsync()
    {
        AzuraStation? station = _onAirViewModel.CurrentStation;
        if (station is null)
        {
            Podcasts = [];
            return;
        }

        await SafeExecuteAsync(async () =>
        {
            List<AzuraCastPodcast> items = await _podcastService.GetPodcastsAsync(station);
            foreach (AzuraCastPodcast podcast in items)
                podcast.SelectCommand = SelectPodcastCommand;

            Podcasts = new ObservableCollection<AzuraCastPodcast>(items);
        }, LocalizationResourceManager.Instance["Err_LoadPodcasts"]);
    }

    [RelayCommand]
    private async Task SelectPodcastAsync(AzuraCastPodcast podcast)
    {
        await Shell.Current.GoToAsync($"PodcastEpisodesPage?podcastId={Uri.EscapeDataString(podcast.Id)}&podcastTitle={Uri.EscapeDataString(podcast.Title)}");
    }
}
