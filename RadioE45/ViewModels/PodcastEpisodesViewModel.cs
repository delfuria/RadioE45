using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Localization;
using RadioE45.Services.Radio;

namespace RadioE45.ViewModels;

[QueryProperty(nameof(PodcastId), "podcastId")]
[QueryProperty(nameof(PodcastTitle), "podcastTitle")]
public partial class PodcastEpisodesViewModel : BaseViewModel
{
    private readonly IPodcastService _podcastService;
    private readonly IPodcastPlayerService _podcastPlayerService;
    private readonly OnAirViewModel _onAirViewModel;
    private int? _loadedStationId;

    [ObservableProperty]
    public partial string PodcastId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PodcastTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<AzuraCastPodcastEpisode> Episodes { get; set; } = [];

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
        OnAirViewModel onAirViewModel,
        ILogger<PodcastEpisodesViewModel> logger)
    {
        Logger = logger;
        _podcastService = podcastService;
        _podcastPlayerService = podcastPlayerService;
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
            Episodes = [];
            return;
        }

        _loadedStationId = station.Id;

        await SafeExecuteAsync(async () =>
        {
            AzuraCastPodcast podcast = new() { Id = PodcastId, Title = PodcastTitle };
            List<AzuraCastPodcastEpisode> items = await _podcastService.GetEpisodesAsync(station, podcast);
            foreach (AzuraCastPodcastEpisode episode in items)
                episode.PlayCommand = PlayEpisodeCommand;

            Episodes = new ObservableCollection<AzuraCastPodcastEpisode>(items);
        }, LocalizationResourceManager.Instance["Err_LoadEpisodes"]);
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
        if (CurrentEpisode is null || _podcastPlayerService.Duration <= TimeSpan.Zero)
            return;

        TimeSpan target = TimeSpan.FromSeconds(_podcastPlayerService.Duration.TotalSeconds * fraction);
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
