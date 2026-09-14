using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Localization;
using RadioE45.Services.Radio;

namespace RadioE45.ViewModels;

[QueryProperty(nameof(PodcastId), "podcastId")]
[QueryProperty(nameof(PodcastTitle), "podcastTitle")]
public partial class PodcastEpisodesViewModel : BaseViewModel
{
    private readonly IPodcastService _podcastService;
    private readonly IPodcastPlayerService _podcastPlayerService;
    private readonly IPodcastProgressRepository _progressRepository;
    private readonly OnAirViewModel _onAirViewModel;
    private int? _loadedStationId;
    // Elenco completo scaricato dal feed: Episodes è la vista filtrata che ne deriva.
    private List<AzuraCastPodcastEpisode> _allEpisodes = [];

    [ObservableProperty]
    public partial string PodcastId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PodcastTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<AzuraCastPodcastEpisode> Episodes { get; set; } = [];

    [ObservableProperty]
    public partial List<PodcastFilterOption<int>> SeasonOptions { get; set; } = [];

    [ObservableProperty]
    public partial List<PodcastFilterOption<int>> EpisodeNumberOptions { get; set; } = [];

    [ObservableProperty]
    public partial List<PodcastFilterOption<PodcastEpisodeProgressState>> StatusOptions { get; set; } = [];

    [ObservableProperty]
    public partial PodcastFilterOption<int>? SelectedSeason { get; set; }

    [ObservableProperty]
    public partial PodcastFilterOption<int>? SelectedEpisodeNumber { get; set; }

    [ObservableProperty]
    public partial PodcastFilterOption<PodcastEpisodeProgressState>? SelectedStatus { get; set; }

    // Stagione/episodio sono spesso assenti dal feed (vedi commento su SeasonEpisodeText):
    // il relativo Picker si mostra solo se almeno un episodio valorizza il campo.
    [ObservableProperty]
    public partial bool IsSeasonFilterVisible { get; set; }

    [ObservableProperty]
    public partial bool IsEpisodeFilterVisible { get; set; }

    // Distingue "il podcast non ha episodi" da "i filtri escludono tutti gli episodi" — non
    // cambia con la selezione dei filtri, solo quando cambia l'elenco scaricato dal feed.
    [ObservableProperty]
    public partial string EmptyEpisodesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial AzuraCastPodcastEpisode? CurrentEpisode { get; set; }

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    public partial double PlaybackProgress { get; set; }

    [ObservableProperty]
    public partial string ElapsedTimeText { get; set; } = "0:00";

    [ObservableProperty]
    public partial string TotalTimeText { get; set; } = "0:00";

    partial void OnPodcastTitleChanged(string value) => Title = value;

    public PodcastEpisodesViewModel(
        IPodcastService podcastService,
        IPodcastPlayerService podcastPlayerService,
        IPodcastProgressRepository progressRepository,
        OnAirViewModel onAirViewModel,
        ILogger<PodcastEpisodesViewModel> logger)
    {
        Logger = logger;
        _podcastService = podcastService;
        _podcastPlayerService = podcastPlayerService;
        _progressRepository = progressRepository;
        _onAirViewModel = onAirViewModel;

        _podcastPlayerService.PlaybackStateChanged += OnPlaybackStateChanged;
        _podcastPlayerService.PositionChanged += OnPositionChanged;
        _podcastPlayerService.EpisodeCompleted += OnEpisodeCompleted;
        _podcastPlayerService.PlaybackFailed += OnPlaybackFailed;
    }

    // La pagina resta viva nello stack Shell del tab anche quando non è quella visibile: se la
    // stazione cambia (frecce avanti/indietro su OnAir) mentre siamo su questa schermata, gli
    // episodi mostrati appartengono ormai alla stazione sbagliata. Controllato da OnAppearing:
    // se stale, la pagina torna alla lista podcast invece di ricaricare dati non pertinenti.
    public bool IsStaleForCurrentStation() =>
        _loadedStationId is not null && _loadedStationId != _onAirViewModel.CurrentStation?.Id;

    [RelayCommand]
    private async Task LoadEpisodesAsync()
    {
        AzuraStation? station = _onAirViewModel.CurrentStation;
        if (station is null || string.IsNullOrEmpty(PodcastId))
        {
            _allEpisodes = [];
            Episodes = [];
            EmptyEpisodesText = LocalizationResourceManager.Instance["Podcast_NoEpisodes"];
            return;
        }

        _loadedStationId = station.Id;

        await SafeExecuteAsync(async () =>
        {
            AzuraCastPodcast podcast = new() { Id = PodcastId, Title = PodcastTitle };
            List<AzuraCastPodcastEpisode> items = await _podcastService.GetEpisodesAsync(station, podcast);

            List<PodcastEpisodeProgress> progress = await _progressRepository.GetForPodcastAsync(station.StationId, PodcastId);
            Dictionary<string, PodcastEpisodeProgress> progressByEpisodeId = progress.ToDictionary(p => p.EpisodeId);

            foreach (AzuraCastPodcastEpisode episode in items)
            {
                episode.PlayCommand = PlayEpisodeCommand;

                if (progressByEpisodeId.TryGetValue(episode.Id, out PodcastEpisodeProgress? saved))
                {
                    episode.ResumePositionSeconds = saved.PositionSeconds;
                    episode.ProgressState = saved.IsCompleted
                        ? PodcastEpisodeProgressState.Completed
                        : saved.PositionSeconds > 3
                            ? PodcastEpisodeProgressState.InProgress
                            : PodcastEpisodeProgressState.NotStarted;
                }
            }

            _allEpisodes = items;
            EmptyEpisodesText = LocalizationResourceManager.Instance[
                items.Count == 0 ? "Podcast_NoEpisodes" : "Podcast_NoEpisodesFiltered"];
            BuildFilterOptions(items);
            ApplyFilter();
        }, LocalizationResourceManager.Instance["Err_LoadEpisodes"]);
    }

    private void BuildFilterOptions(List<AzuraCastPodcastEpisode> items)
    {
        List<int> seasons = items
            .Where(e => e.SeasonNumber.HasValue)
            .Select(e => e.SeasonNumber!.Value)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        List<int> episodeNumbers = items
            .Where(e => e.EpisodeNumber.HasValue)
            .Select(e => e.EpisodeNumber!.Value)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        IsSeasonFilterVisible = seasons.Count > 0;
        IsEpisodeFilterVisible = episodeNumbers.Count > 0;

        SeasonOptions = new List<PodcastFilterOption<int>>
        {
            new(LocalizationResourceManager.Instance["Podcast_FilterAllSeasons"], null),
        }.Concat(seasons.Select(s => new PodcastFilterOption<int>(s.ToString(), s))).ToList();

        EpisodeNumberOptions = new List<PodcastFilterOption<int>>
        {
            new(LocalizationResourceManager.Instance["Podcast_FilterAllEpisodes"], null),
        }.Concat(episodeNumbers.Select(n => new PodcastFilterOption<int>(n.ToString(), n))).ToList();

        StatusOptions =
        [
            new(LocalizationResourceManager.Instance["Podcast_FilterAllStatuses"], null),
            new(LocalizationResourceManager.Instance["Podcast_StatusNotStarted"], PodcastEpisodeProgressState.NotStarted),
            new(LocalizationResourceManager.Instance["Podcast_StatusInProgress"], PodcastEpisodeProgressState.InProgress),
            new(LocalizationResourceManager.Instance["Podcast_StatusCompleted"], PodcastEpisodeProgressState.Completed),
        ];

        // Selezionare "Tutti/Tutte" fa scattare ApplyFilter per ciascuna delle tre proprietà;
        // ridondante con la ApplyFilter() finale di LoadEpisodesAsync ma innocuo (liste corte).
        SelectedSeason = SeasonOptions[0];
        SelectedEpisodeNumber = EpisodeNumberOptions[0];
        SelectedStatus = StatusOptions[0];
    }

    partial void OnSelectedSeasonChanged(PodcastFilterOption<int>? value) => ApplyFilter();

    partial void OnSelectedEpisodeNumberChanged(PodcastFilterOption<int>? value) => ApplyFilter();

    partial void OnSelectedStatusChanged(PodcastFilterOption<PodcastEpisodeProgressState>? value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<AzuraCastPodcastEpisode> query = _allEpisodes;

        if (SelectedSeason?.Value is { } season)
            query = query.Where(e => e.SeasonNumber == season);

        if (SelectedEpisodeNumber?.Value is { } episodeNumber)
            query = query.Where(e => e.EpisodeNumber == episodeNumber);

        if (SelectedStatus?.Value is { } status)
            query = query.Where(e => e.ProgressState == status);

        Episodes = new ObservableCollection<AzuraCastPodcastEpisode>(query);
    }

    [RelayCommand]
    private async Task PlayEpisodeAsync(AzuraCastPodcastEpisode episode)
    {
        AzuraStation? station = _onAirViewModel.CurrentStation;
        if (station is null)
            return;

        if (CurrentEpisode?.Id == episode.Id)
        {
            await PlayPauseAsync();
            return;
        }

        CurrentEpisode = episode;

        // Aggiorna subito l'indicatore (verde -> giallo): non aspettare il prossimo OnAppearing.
        if (episode.ProgressState == PodcastEpisodeProgressState.NotStarted)
        {
            episode.ProgressState = PodcastEpisodeProgressState.InProgress;
            // Se è attivo un filtro per stato, l'episodio potrebbe non farne più parte
            // (es. filtro "Da ascoltare"): ri-applica per rifletterlo subito in lista.
            if (SelectedStatus?.Value is not null)
                ApplyFilter();
        }

        int startPositionSeconds = await _podcastPlayerService.GetResumePositionSecondsAsync(station, episode);
        TotalTimeText = FormatTime(episode.Duration);
        UpdatePlaybackPosition(TimeSpan.FromSeconds(startPositionSeconds));

        await _podcastPlayerService.PlayAsync(station, episode, startPositionSeconds);
    }

    [RelayCommand]
    private async Task PlayPauseAsync()
    {
        if (CurrentEpisode is null)
            return;

        if (IsPlaying)
            await _podcastPlayerService.PauseAsync();
        else
            await _podcastPlayerService.ResumeAsync();
    }

    [RelayCommand]
    private async Task SeekAsync(double fraction)
    {
        // Usa la durata dell'episodio da feed, non quella del player nativo
        // (_podcastPlayerService.Duration): per uno stream appena aperto quest'ultima resta a zero
        // per i primi secondi, facendo fallire in silenzio il seek subito dopo l'avvio (stesso
        // motivo per cui UpdatePlaybackPosition non la usa, vedi commento lì).
        if (CurrentEpisode is null || CurrentEpisode.Duration <= TimeSpan.Zero)
            return;

        TimeSpan target = TimeSpan.FromSeconds(CurrentEpisode.Duration.TotalSeconds * fraction);
        await _podcastPlayerService.SeekAsync(target);
    }

    public async Task StopPlaybackAsync()
    {
        if (CurrentEpisode is null)
            return;

        await _podcastPlayerService.StopAsync();
        CurrentEpisode = null;
        IsPlaying = false;
        PlaybackProgress = 0;
    }

    private void OnPlaybackStateChanged(object? sender, bool isPlaying)
    {
        MainThread.BeginInvokeOnMainThread(() => IsPlaying = isPlaying);
    }

    private void OnPositionChanged(object? sender, TimeSpan position)
    {
        MainThread.BeginInvokeOnMainThread(() => UpdatePlaybackPosition(position));
    }

    // Unico punto che valorizza label ed elapsed e progress bar: entrambi derivano dalla STESSA
    // coppia (position, durata episodio da feed). La durata del player nativo
    // (_podcastPlayerService.Duration) non va usata qui — per stream HTTP può restare a zero per
    // un po' o oscillare durante il download, e usarla avrebbe fatto disallineare la barra
    // rispetto alla label (che dipende solo da position) ogni volta che la fonte cambiava.
    private void UpdatePlaybackPosition(TimeSpan position)
    {
        TimeSpan duration = CurrentEpisode?.Duration ?? TimeSpan.Zero;

        ElapsedTimeText = FormatTime(position);
        PlaybackProgress = duration > TimeSpan.Zero
            ? Math.Clamp(position.TotalSeconds / duration.TotalSeconds, 0.0, 1.0)
            : 0.0;
    }

    private void OnEpisodeCompleted(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Aggiorna subito l'indicatore (giallo -> rosso) sull'episodio appena terminato,
            // prima di sganciarlo da CurrentEpisode.
            if (CurrentEpisode is { } episode)
            {
                episode.ProgressState = PodcastEpisodeProgressState.Completed;
                if (SelectedStatus?.Value is not null)
                    ApplyFilter();
            }

            CurrentEpisode = null;
            IsPlaying = false;
            PlaybackProgress = 0;
        });
    }

    private void OnPlaybackFailed(object? sender, string? message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ErrorMessage = string.IsNullOrWhiteSpace(message)
                ? LocalizationResourceManager.Instance["Err_PodcastPlayback"]
                : message;
            CurrentEpisode = null;
            IsPlaying = false;
            PlaybackProgress = 0;
        });
    }

    private static string FormatTime(TimeSpan ts) =>
        ts.Hours > 0 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");

    public void Cleanup()
    {
        _podcastPlayerService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _podcastPlayerService.PositionChanged -= OnPositionChanged;
        _podcastPlayerService.EpisodeCompleted -= OnEpisodeCompleted;
        _podcastPlayerService.PlaybackFailed -= OnPlaybackFailed;
    }
}
