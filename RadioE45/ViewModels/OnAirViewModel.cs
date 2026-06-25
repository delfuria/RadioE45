using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Radio;
using RadioE45.Services;

namespace RadioE45.ViewModels;

public partial class OnAirViewModel : BaseViewModel
{
    private readonly IAudioService _audioService;
    private readonly INowPlayingService _nowPlayingService;
    private readonly IAzuraStationCatalog _catalog;
    private readonly IAppSettingsRepository _settingsRepo;
    private CancellationTokenSource? _pollingCts;

    [ObservableProperty]
    public partial AzuraStation? CurrentStation { get; set; }

    [ObservableProperty]
    public partial NowPlayingInfo NowPlaying { get; set; } = NowPlayingInfo.Empty;

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    public partial bool IsBuffering { get; set; }

    [ObservableProperty]
    public partial double Volume { get; set; }

    [ObservableProperty]
    public partial bool IsMuted { get; set; }

    private double _preMuteVolume = 1.0;

    [ObservableProperty]
    public partial string? ArtworkUrl { get; set; }

    [ObservableProperty]
    public partial double ArtworkHeight { get; set; } = 300;

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    partial void OnCurrentStationChanged(AzuraStation? value)
    {
        IsFavorite = value?.IsFavorite ?? false;
    }

    [ObservableProperty]
    public partial double TrackProgress { get; set; }

    [ObservableProperty]
    public partial string ElapsedTimeText { get; set; } = "0:00";

    [ObservableProperty]
    public partial string TotalTimeText { get; set; } = "0:00";

    private IDispatcherTimer? _progressTimer;
    private int _localElapsedSeconds;
    private int _trackDurationSeconds;
    private volatile bool _isShuttingDown;

    public OnAirViewModel(
        IAudioService audioService,
        INowPlayingService nowPlayingService,
        IAzuraStationCatalog catalog,
        IAppSettingsRepository settingsRepo,
        ILogger<OnAirViewModel> logger)
    {
        Logger = logger;
        _audioService = audioService;
        _nowPlayingService = nowPlayingService;
        _catalog = catalog;
        _settingsRepo = settingsRepo;

        Volume = Preferences.Default.Get("player_volume", 1.0);
        Title = "In Onda";

        _audioService.PlaybackStateChanged += OnPlaybackStateChanged;
        _audioService.ErrorOccurred += OnErrorOccurred;
        _audioService.StreamOpened += OnStreamOpened;
        _nowPlayingService.NowPlayingUpdated += OnNowPlayingUpdated;
        _catalog.StationsRefreshed += OnStationsRefreshed;
    }

    public async Task InitializeAsync()
    {
        if (CurrentStation is not null)
            return;

        await SafeExecuteAsync(async () =>
        {
            await _catalog.LoadAsync();
            AppSettings settings = await _settingsRepo.GetAsync();
            AzuraStation? station = null;

            if (settings.StartWithFavorite)
                station = _catalog.GetFavorite();

            station ??= _catalog.GetFirst();

            if (station is not null)
            {
                CurrentStation = station;
                await StartPlayAndNowPollingAsync(station);
            }
        }, "Inizializzazione");
    }

    public async Task RestartAsync()
    {
        if (CurrentStation is null)
            return;

        await StartPlayAndNowPollingAsync(CurrentStation);
    }

    private async void OnStationsRefreshed()
    {
        if (CurrentStation is not null) return;

        await SafeExecuteAsync(async () =>
        {
            AppSettings settings = await _settingsRepo.GetAsync();
            AzuraStation? station = settings.StartWithFavorite ? _catalog.GetFavorite() : null;
            station ??= _catalog.GetFirst();
            if (station is not null)
            {
                CurrentStation = station;
                await StartPlayAndNowPollingAsync(station);
            }
        }, "Ripristino dopo connessione");
    }

    [RelayCommand]
    private async Task PlayPauseAsync()
    {
        if (CurrentStation is null)
            return;

        if (IsPlaying)
            await _audioService.PauseAsync();
        else
        {
            if (_audioService.CurrentStation is null)
            {
                await StartPlayAndNowPollingAsync(CurrentStation);
            }
            else
            {
                await ResumePlayAndRefreshPollingAsync();
            }
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        await _audioService.StopAsync();
        StopProgressTimer();
        _localElapsedSeconds = 0;
        _trackDurationSeconds = 0;
        UpdateProgressDisplay();
    }

    public async Task ClearStationAsync()
    {
        await StopAsync();
        CurrentStation = null;
        NowPlaying = NowPlayingInfo.Empty;
        ArtworkUrl = null;
    }

    [RelayCommand]
    private async Task RefreshNowPlayingAsync()
    {
        if (CurrentStation is null)
        {
            ErrorMessage = "Nessuna stazione selezionata.";
            return;
        }

        ErrorMessage = null;
        await SafeExecuteAsync(async () =>
        {
            NowPlayingInfo info = await _nowPlayingService.FetchOnceAsync(CurrentStation);
            ApplyNowPlayingInfo(info);
        }, "Aggiornamento now playing");
    }

    [RelayCommand]
    private void SetVolume(double volume)
    {
        Volume = volume;
        _audioService.SetVolume(volume);
        Preferences.Default.Set("player_volume", volume);
        if (volume > 0 && IsMuted)
            IsMuted = false;
    }

    [RelayCommand]
    private void ToggleMute()
    {
        if (IsMuted)
        {
            IsMuted = false;
            double restore = _preMuteVolume > 0 ? _preMuteVolume : 1.0;
            Volume = restore;
            _audioService.SetVolume(restore);
            Preferences.Default.Set("player_volume", restore);
        }
        else
        {
            _preMuteVolume = Volume > 0 ? Volume : 1.0;
            IsMuted = true;
            Volume = 0;
            _audioService.SetVolume(0);
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (CurrentStation is null) return;
        bool newValue = !CurrentStation.IsFavorite;
        await _catalog.SetFavoriteAsync(CurrentStation.Id, newValue);
        IsFavorite = newValue;
    }

    [RelayCommand]
    private async Task SelectStationAsync(AzuraStation station)
    {
        if (CurrentStation?.Id == station.Id)
        {
            if (!IsPlaying)
                await StartPlayAndNowPollingAsync(station);

            return;
        }

        await _audioService.StopAsync();
        await StopNowPlayingPollingAsync();

        CurrentStation = station;
        NowPlaying = NowPlayingInfo.Empty;
        ArtworkUrl = null;
        StopProgressTimer();
        _localElapsedSeconds = 0;
        _trackDurationSeconds = 0;
        UpdateProgressDisplay();
        if (station is not null)
            await StartPlayAndNowPollingAsync(station);
    }

    private async Task ResumePlayAndRefreshPollingAsync()
    {
        await _audioService.ResumeAsync();
        await RefreshNowPlayingAsync();
    }

    private async Task StartPlayAndNowPollingAsync(AzuraStation station)
    {
        ApplyStationMetadata(station);
        await _audioService.PlayAsync(station);
        await StartNowPlayingPollingAsync(station);
    }

    private async Task StartNowPlayingPollingAsync(AzuraStation station)
    {
        await StopNowPlayingPollingAsync();
        _pollingCts = new CancellationTokenSource();
        await _nowPlayingService.StartPollingAsync(station, _pollingCts.Token);
    }

    private async Task StopNowPlayingPollingAsync()
    {
        if (_pollingCts is not null)
        {
            await _pollingCts.CancelAsync();
            _pollingCts.Dispose();
            _pollingCts = null;
        }
        await _nowPlayingService.StopPollingAsync();
    }

    private async void OnStreamOpened(object? sender, AzuraStation station)
    {
        NowPlayingInfo info = await _nowPlayingService.FetchOnceAsync(station);
        RunOnMainThreadIfActive(() => ApplyNowPlayingInfo(info));
    }

    private void OnPlaybackStateChanged(object? sender, bool isPlaying)
    {
        if (isPlaying)
            _nowPlayingService.ResumePolling();
        else
            _nowPlayingService.PausePolling();

        RunOnMainThreadIfActive(() =>
        {
            IsPlaying = isPlaying;
            IsBuffering = _audioService.IsBuffering;
        });
    }

    private void OnErrorOccurred(object? sender, string? message)
    {
        RunOnMainThreadIfActive(() =>
        {
            ErrorMessage = message;
        });
    }

    private void OnNowPlayingUpdated(object? sender, NowPlayingInfo info)
    {
        RunOnMainThreadIfActive(() =>
        {
            ApplyNowPlayingInfo(info);
        });
    }

    private void ApplyNowPlayingInfo(NowPlayingInfo info)
    {
        ErrorMessage = "";
        NowPlaying = info;
        ArtworkUrl = info.ArtworkUrl;
        _audioService.UpdateMetadata(info.Artist, info.Title, info.ArtworkUrl, info.TrackElapsedSeconds, info.TrackDurationSeconds);

        if (info.TrackElapsedSeconds >= _localElapsedSeconds || (_localElapsedSeconds - info.TrackElapsedSeconds) > 15)
            _localElapsedSeconds = info.TrackElapsedSeconds;
        _trackDurationSeconds = info.TrackDurationSeconds;
        UpdateProgressDisplay();

        if (_trackDurationSeconds > 0)
            EnsureProgressTimerRunning();
        else
            StopProgressTimer();
    }

    private void ApplyStationMetadata(AzuraStation station)
    {
        string artist = string.IsNullOrWhiteSpace(station.Description) ? station.Name : station.Description;
        string title = string.IsNullOrWhiteSpace(station.Name) ? "In attesa..." : station.Name;
        string? artworkUrl = string.IsNullOrWhiteSpace(station.LogoUrl) ? null : station.LogoUrl;

        _audioService.UpdateMetadata(artist, title, artworkUrl);
    }

    private void EnsureProgressTimerRunning()
    {
        if (_isShuttingDown || _progressTimer is not null)
            return;

        IDispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        _progressTimer = dispatcher.CreateTimer();
        _progressTimer.Interval = TimeSpan.FromSeconds(1);
        _progressTimer.Tick += OnProgressTimerTick;
        _progressTimer.Start();
    }

    private void StopProgressTimer()
    {
        if (_progressTimer is null)
            return;

        _progressTimer.Stop();
        _progressTimer.Tick -= OnProgressTimerTick;
        _progressTimer = null;
    }

    private void OnProgressTimerTick(object? sender, EventArgs e)
    {
        if (_trackDurationSeconds <= 0)
            return;

        _localElapsedSeconds = Math.Min(_localElapsedSeconds + 1, _trackDurationSeconds);
        UpdateProgressDisplay();
    }

    private void UpdateProgressDisplay()
    {
        ElapsedTimeText = FormatTime(_localElapsedSeconds);
        TotalTimeText = FormatTime(_trackDurationSeconds);
        TrackProgress = _trackDurationSeconds > 0
            ? Math.Clamp((double)_localElapsedSeconds / _trackDurationSeconds, 0.0, 1.0)
            : 0.0;
    }

    private static string FormatTime(int totalSeconds)
    {
        TimeSpan ts = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return ts.Hours > 0 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
    }

    private void RunOnMainThreadIfActive(Action action)
    {
        if (_isShuttingDown)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_isShuttingDown)
                return;

            action();
        });
    }

    public async Task ShutdownAsync()
    {
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;
        _audioService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _audioService.ErrorOccurred -= OnErrorOccurred;
        _audioService.StreamOpened -= OnStreamOpened;
        _nowPlayingService.NowPlayingUpdated -= OnNowPlayingUpdated;

        StopProgressTimer();
        await StopNowPlayingPollingAsync();
        _audioService.Shutdown();
    }
}
