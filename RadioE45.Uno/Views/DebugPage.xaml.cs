using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;
using RadioE45.Services.Radio;
using RadioE45.Services.Shared;

namespace RadioE45.Views;

public sealed partial class DebugPage : Page
{
    private const string OpenCountKey = "debug_page_open_count";

    private readonly IUiDispatcher _dispatcher = App.Services.GetRequiredService<IUiDispatcher>();
    private readonly IDialogService _dialogs = App.Services.GetRequiredService<IDialogService>();
    private readonly ISettingsStore _settings = App.Services.GetRequiredService<ISettingsStore>();
    private readonly IAppEnvironment _environment = App.Services.GetRequiredService<IAppEnvironment>();
    private readonly IUrlLauncher _urlLauncher = App.Services.GetRequiredService<IUrlLauncher>();
    private readonly IThemeService _theme = App.Services.GetRequiredService<IThemeService>();
    private readonly IAudioService _audio = App.Services.GetRequiredService<IAudioService>();
    private readonly INetworkMonitor _network = App.Services.GetRequiredService<INetworkMonitor>();
    private readonly IAppPaths _paths = App.Services.GetRequiredService<IAppPaths>();
    private readonly IPodcastPlayerService _podcast = App.Services.GetRequiredService<IPodcastPlayerService>();
    private IUiTimer? _timer;
    private int _ticks;

    public DebugPage()
    {
        InitializeComponent();

        EnvironmentText.Text =
            $"Versione {_environment.VersionString} (build {_environment.BuildString}, {_environment.CommitId}) · " +
            $"{_environment.PlatformName} · volume di sistema: {_environment.UsesSystemVolume} · " +
            $"Local Network privacy: {_environment.HasLocalNetworkPrivacy}";

        // Persisted counter: after a restart it must continue from the previous value.
        var count = _settings.Get(OpenCountKey, 0) + 1;
        _settings.Set(OpenCountKey, count);
        OpenCountText.Text = $"Pagina aperta {count} volte (ISettingsStore, persiste tra i riavvii)";

        ThemeBox.SelectedItem = _settings.Get(UnoThemeService.PreferenceKey, UnoThemeService.DefaultPreference);

        PathsText.Text = $"IAppPaths: {_paths.AppDataDirectory}";
        ShowNetwork(_network.HasInternet);
        ShowAudio();

        Loaded += (_, _) =>
        {
            _network.ConnectivityChanged += OnConnectivityChanged;
            _audio.PlaybackStateChanged += OnPlaybackStateChanged;
            _audio.ErrorOccurred += OnAudioError;
            _podcast.PositionChanged += OnPodcastPosition;
            _podcast.PlaybackStateChanged += OnPodcastState;
            _podcast.EpisodeCompleted += OnPodcastCompleted;
            _podcast.PlaybackFailed += OnPodcastFailed;
            _timer = _dispatcher.CreateTimer(TimeSpan.FromSeconds(1), () => TimerText.Text = $"Timer UI: {++_ticks} s");
            _timer?.Start();
        };
        Unloaded += (_, _) =>
        {
            _network.ConnectivityChanged -= OnConnectivityChanged;
            _audio.PlaybackStateChanged -= OnPlaybackStateChanged;
            _audio.ErrorOccurred -= OnAudioError;
            _podcast.PositionChanged -= OnPodcastPosition;
            _podcast.PlaybackStateChanged -= OnPodcastState;
            _podcast.EpisodeCompleted -= OnPodcastCompleted;
            _podcast.PlaybackFailed -= OnPodcastFailed;
            _timer?.Stop();
            _timer = null;
        };
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeBox.SelectedItem is not string preference)
            return;
        _settings.Set(UnoThemeService.PreferenceKey, preference);
        _theme.Apply(preference);
    }

    private async void OnAlertClick(object sender, RoutedEventArgs e) =>
        await _dialogs.AlertAsync("Alert", "Messaggio di prova di IDialogService.AlertAsync.", "OK");

    private async void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        var accepted = await _dialogs.ConfirmAsync("Conferma", "Confermi?", "Sì", "No");
        ResultText.Text = $"ConfirmAsync → {accepted}";
    }

    private async void OnOpenLinkClick(object sender, RoutedEventArgs e) =>
        await _urlLauncher.OpenAsync(new Uri("https://www.azuracast.com/"));

    private async void OnBackgroundDispatchClick(object sender, RoutedEventArgs e)
    {
        var result = await Task.Run(async () =>
        {
            var before = _dispatcher.IsUiThread;
            var inside = false;
            await _dispatcher.InvokeAsync(() => inside = _dispatcher.IsUiThread);
            return (before, inside);
        });
        ResultText.Text = $"Thread di fondo: IsUiThread={result.before}; dentro InvokeAsync: IsUiThread={result.inside}";
    }

    private async void OnLoadStationsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            // Same path as the empty-list seed prompt of RadioListViewModel.
            if (!await App.Services.GetRequiredService<IRadioRepository>().HasStationsAsync())
                await App.Services.GetRequiredService<IDatabaseService>().SeedStationsAsync();

            var catalog = App.Services.GetRequiredService<IAzuraStationCatalog>();
            await catalog.ReloadAsync();
            StationBox.ItemsSource = catalog.Stations;
            StationBox.SelectedIndex = catalog.Stations.Count > 0 ? 0 : -1;
            AudioText.Text = $"{catalog.Stations.Count} stazioni nel catalogo";
        }
        catch (Exception ex)
        {
            AudioText.Text = $"Caricamento stazioni fallito: {ex.GetType().Name}: {ex.Message}";
        }
    }

    // Web: these run from a real click, as the browser requires for play() (roadmap §5).
    private async void OnPlayClick(object sender, RoutedEventArgs e)
    {
        if (StationBox.SelectedItem is AzuraStation station)
            await _audio.PlayAsync(station);
        ShowAudio();
    }

    private async void OnPauseClick(object sender, RoutedEventArgs e) { await _audio.PauseAsync(); ShowAudio(); }

    private async void OnResumeClick(object sender, RoutedEventArgs e) { await _audio.ResumeAsync(); ShowAudio(); }

    private async void OnStopClick(object sender, RoutedEventArgs e) { await _audio.StopAsync(); ShowAudio(); }

    private void OnVolumeChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) =>
        _audio?.SetVolume(e.NewValue / 100.0);

    private void OnPlaybackStateChanged(object? sender, bool isPlaying) => _dispatcher.Post(ShowAudio);

    private void OnAudioError(object? sender, string? message) =>
        _dispatcher.Post(() => AudioText.Text = $"Errore audio: {message}");

    private void OnConnectivityChanged(object? sender, bool hasInternet) => _dispatcher.Post(() => ShowNetwork(hasInternet));

    private void ShowNetwork(bool hasInternet) =>
        NetworkText.Text = $"INetworkMonitor: {(hasInternet ? "online" : "OFFLINE")} (aggiornato {DateTime.Now:HH:mm:ss})";

    private void ShowAudio() =>
        AudioText.Text = $"Stazione: {_audio.CurrentStation?.Name ?? "-"} · playing={_audio.IsPlaying} · " +
                         $"buffering={_audio.IsBuffering} · HLS={_audio.IsHlsActive} ({DateTime.Now:HH:mm:ss})";

    private (AzuraStation Station, AzuraCastPodcastEpisode Episode)? _episode;

    // First episode with media of the first station (in the loaded catalog) that has podcasts.
    // Separate from Play: on the web play() must follow the click within a few seconds (§5),
    // and fetching the lists takes longer.
    private async void OnPodcastFindClick(object sender, RoutedEventArgs e)
    {
        var catalog = App.Services.GetRequiredService<IAzuraStationCatalog>();
        var podcasts = App.Services.GetRequiredService<IPodcastService>();
        string? error = null;
        foreach (var station in catalog.Stations)
        {
            try
            {
                foreach (var podcast in await podcasts.GetPodcastsAsync(station))
                {
                    var episode = (await podcasts.GetEpisodesAsync(station, podcast)).FirstOrDefault(x => x.MediaUrl.Length > 0);
                    if (episode is null)
                        continue;
                    _episode = (station, episode);
                    PodcastText.Text = $"Episodio: '{episode.Title}' ({station.Name})";
                    return;
                }
            }
            catch (Exception ex)
            {
                error = $"{station.Name}: {ex.GetType().Name}: {ex.Message}";
            }
        }

        PodcastText.Text = error ?? "Nessun episodio trovato (premere prima \"Carica stazioni\")";
    }

    // Starts from the saved position, like PodcastEpisodesViewModel.
    private async void OnPodcastPlayClick(object sender, RoutedEventArgs e)
    {
        if (_episode is not { } selected)
        {
            PodcastText.Text = "Premere prima \"Trova episodio\"";
            return;
        }

        var start = await _podcast.GetResumePositionSecondsAsync(selected.Station, selected.Episode);
        PodcastText.Text = $"Avvio da {start} s";
        await _podcast.PlayAsync(selected.Station, selected.Episode, start);
    }

    private async void OnPodcastPauseClick(object sender, RoutedEventArgs e) => await _podcast.PauseAsync();

    private async void OnPodcastResumeClick(object sender, RoutedEventArgs e) => await _podcast.ResumeAsync();

    private async void OnPodcastSeekClick(object sender, RoutedEventArgs e) =>
        await _podcast.SeekAsync(_podcast.Position + TimeSpan.FromSeconds(10));

    private async void OnPodcastStopClick(object sender, RoutedEventArgs e) => await _podcast.StopAsync();

    private void OnPodcastPosition(object? sender, TimeSpan position) => _dispatcher.Post(ShowPodcast);

    private void OnPodcastState(object? sender, bool playing) => _dispatcher.Post(ShowPodcast);

    private void OnPodcastCompleted(object? sender, EventArgs e) =>
        _dispatcher.Post(() => PodcastText.Text = "Episodio completato (EpisodeCompleted)");

    private void OnPodcastFailed(object? sender, string? message) =>
        _dispatcher.Post(() => PodcastText.Text = $"Errore podcast: {message ?? "avvio non riuscito"}");

    private void ShowPodcast() =>
        PodcastText.Text = $"{_podcast.CurrentEpisode?.Title ?? "-"} · playing={_podcast.IsPlaying} · " +
                           $"{_podcast.Position:mm\\:ss} / {_podcast.Duration:mm\\:ss}";
}
