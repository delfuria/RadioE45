using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Audio;

/// <summary>
/// Live radio engine shared by the MAUI (iOS, Mac Catalyst, Windows) and Uno heads: stream URL
/// selection and fallback, reconnection (watchdog, connectivity, media failures) and the live
/// pause rule. The actual playback goes through an <see cref="IStreamPlayer"/> attached by the
/// head. Android does not use it: there the Media3 MediaLibraryService owns playback.
/// </summary>
public class LiveStreamAudioService : IAudioService
{
    private readonly IStreamUrlProber _streamUrlProber;
    private readonly IPlatformNowPlayingService _platformNowPlayingService;
    private readonly IAudioFocusManager _audioFocusManager;
    private readonly IAppSettingsRepository _settingsRepo;
    private readonly INetworkMonitor _networkMonitor;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger _logger;
    private IStreamPlayer? _player;
    private AzuraStation? _currentStation;
    private bool _shouldBePlaying;
    private int _reconnectGuard;   // 0 = idle, 1 = busy — accesso tramite Interlocked
    private StreamPlayerState _currentState = StreamPlayerState.None;
    private DateTime _bufferingStartedAt = DateTime.MinValue;
    private const double BufferingTimeoutSeconds = 12.0;
    private System.Timers.Timer? _watchdog;
    private const double WatchdogIntervalMs = 10000;
    private bool _isShuttingDown;
    private bool _isConnectivitySubscribed;
    private CancellationTokenSource _reconnectCts = new();
    private readonly object _ctsLock = new();

    public bool IsPlaying { get; private set; }
    public bool IsBuffering { get; private set; }
    public AzuraStation? CurrentStation => _currentStation;
    public bool IsHlsActive { get; private set; }

    public event EventHandler<bool>? PlaybackStateChanged;
    public event EventHandler<string?>? ErrorOccurred;
    public event EventHandler<AzuraStation>? StreamOpened;
    // Station changes outside Android always originate in the app UI, so this fires in lock-step
    // with PlayAsync; it exists to satisfy IAudioService uniformly (Android raises it for car-driven changes).
    public event EventHandler<AzuraStation>? StationChanged;

    public LiveStreamAudioService(
        IStreamUrlProber streamUrlProber,
        IPlatformNowPlayingService platformNowPlayingService,
        IAudioFocusManager audioFocusManager,
        IAppSettingsRepository settingsRepo,
        INetworkMonitor networkMonitor,
        IUiDispatcher dispatcher,
        ILogger logger)
    {
        _streamUrlProber = streamUrlProber;
        _platformNowPlayingService = platformNowPlayingService;
        _audioFocusManager = audioFocusManager;
        _settingsRepo = settingsRepo;
        _networkMonitor = networkMonitor;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Attaches the player that will render the stream. MAUI calls it once per OnAirPage
    /// instance (the MediaElement lives in the page); Uno once at startup. Until a player is
    /// attached, Play/Pause/Resume/Stop are no-ops.
    /// </summary>
    public void AttachPlayer(IStreamPlayer player)
    {
        _isShuttingDown = false;

        if (_player is not null)
        {
            _player.StateChanged -= OnStateChanged;
            _player.Failed -= OnMediaFailed;
            _player.Ended -= OnMediaEnded;
            // Il player precedente può ancora suonare (es. stream avviato dal widget
            // mentre l'app era in background). Va fermato prima di rimpiazzarlo,
            // altrimenti rimane vivo in memoria e produce un doppio stream.
            _player.Close();
        }

        _player = player;
        _player.StateChanged += OnStateChanged;
        _player.Failed += OnMediaFailed;
        _player.Ended += OnMediaEnded;

        if (!_isConnectivitySubscribed)
        {
            _networkMonitor.ConnectivityChanged += OnConnectivityChanged;
            _isConnectivitySubscribed = true;
        }

        // Se lo stream era attivo (es. ripreso dal widget), riavvia sul nuovo player
        // senza aspettare il watchdog.
        if (_shouldBePlaying && _currentStation is not null)
        {
            RenewReconnectCts();
            Interlocked.Exchange(ref _reconnectGuard, 0);
            TryQueueReconnect();
        }
    }

    public Task PlayAsync(AzuraStation station)
    {
        if (_player is null)
            return Task.CompletedTask;

        if (!_audioFocusManager.RequestFocus())
        {
            _logger.LogWarning("Audio focus denied — playback blocked");
            return Task.CompletedTask;
        }

        _logger.LogDebug("Start radio streaming...");

        _currentStation = station;
        _shouldBePlaying = true;
        _bufferingStartedAt = DateTime.MinValue;
        StationChanged?.Invoke(this, station);
        RenewReconnectCts();
        Interlocked.Exchange(ref _reconnectGuard, 0);
        TryQueueReconnect();
        return Task.CompletedTask;
    }

    // Per uno stream live non ha senso un "vero" pause: il Pause() del player lascia la
    // connessione aperta e bufferizza, così alla ripresa si sentirebbe audio non più in diretta.
    // Per questo la pausa chiude la sorgente (come uno stop) ma mantiene stazione e metadati,
    // così la notifica/widget resta visibile in stato "in pausa" e Resume può riaprire la diretta.
    public async Task PauseAsync()
    {
        IStreamPlayer? player = _player;
        if (player is null)
            return;

        _logger.LogDebug("Pause station streaming...");

        _shouldBePlaying = false;
        CancelReconnect();
        Interlocked.Exchange(ref _reconnectGuard, 0);
        StopWatchdog();

        await _dispatcher.InvokeAsync(player.Close);

        _platformNowPlayingService.UpdatePlaybackState(false);
    }

    // Riprende riaprendo lo stream da capo (come PlayAsync), in modo da tornare sul punto
    // attuale della diretta invece di proseguire da un buffer ormai non più sincronizzato.
    public Task ResumeAsync()
    {
        if (_player is null || _currentStation is null)
            return Task.CompletedTask;

        if (!_audioFocusManager.RequestFocus())
        {
            _logger.LogWarning("Audio focus denied — resume blocked");
            return Task.CompletedTask;
        }

        _logger.LogDebug("Resume station streaming...");

        _shouldBePlaying = true;
        _bufferingStartedAt = DateTime.MinValue;
        RenewReconnectCts();
        Interlocked.Exchange(ref _reconnectGuard, 0);
        TryQueueReconnect();

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        IStreamPlayer? player = _player;
        if (player is null)
            return;

        _logger.LogDebug("Stop station streaming...");

        _shouldBePlaying = false;
        CancelReconnect();
        Interlocked.Exchange(ref _reconnectGuard, 0);
        _currentStation = null;
        StopWatchdog();

        await _dispatcher.InvokeAsync(() =>
        {
            player.Close();
            player.ClearMetadata();
        });

        _audioFocusManager.AbandonFocus();
        _platformNowPlayingService.Clear();
    }

    // Chiamato da OnTaskRemoved (swipe), che Android dispatcha sul main thread.
    // Se siamo già sul main thread, Close() viene chiamato direttamente (nessun deadlock).
    // Se per qualunque ragione siamo su un thread diverso, lo posta e torna subito.
    public void StopImmediate()
    {
        _shouldBePlaying = false;
        CancelReconnect();
        Interlocked.Exchange(ref _reconnectGuard, 0);
        _currentStation = null;
        StopWatchdog();
        _audioFocusManager.AbandonFocus();

        _logger.LogDebug("Stop immediate station streaming...");

        IStreamPlayer? player = _player;
        if (player is null)
            return;

        if (_dispatcher.IsUiThread)
        {
            player.Close();
            player.ClearMetadata();
        }
        else
        {
            _dispatcher.Post(() =>
            {
                player.Close();
                player.ClearMetadata();
            });
        }

        _platformNowPlayingService.Clear();
    }

    public void SetVolume(double volume)
    {
        IStreamPlayer? player = _player;
        if (player is null)
            return;

        double clamped = Math.Clamp(volume, 0.0, 1.0);
        _audioFocusManager.NotifyVolumeChanged(clamped);
        _dispatcher.Post(() => player.SetVolume(clamped));
    }

    public void UpdateMetadata(string artist, string title, string? artworkUrl = null, int? elapsedSeconds = null, int? durationSeconds = null)
    {
        IStreamPlayer? player = _player;
        if (player is null)
            return;

        _dispatcher.Post(() => player.SetMetadata(artist, title, artworkUrl));

        _platformNowPlayingService.UpdateMetadata(artist, title, artworkUrl, elapsedSeconds, durationSeconds, IsPlaying);
    }

    /// <summary>
    /// Stream URLs to try, in order. Default is the direct Icecast/MP3 stream first — even a tuned
    /// AzuraCast HLS config has a structural floor of a few seconds behind the live edge (standard
    /// HLS forbids starting closer than ~3 segments from the end), which the direct stream doesn't
    /// have. See AppSettings.PreferHlsStream. The last URL that worked always comes first.
    /// </summary>
    public static string[] GetCandidateUrls(AzuraStation station, bool preferHls) =>
        (preferHls
            ? new[] { station.OnAirStreamUrl, station.HlsUrl, station.StreamUrl, station.StreamUrlFallback }
            : new[] { station.OnAirStreamUrl, station.StreamUrl, station.StreamUrlFallback, station.HlsUrl })
        .Where(u => !string.IsNullOrEmpty(u))
        .Distinct()
        .ToArray()!;

    private async Task TryOpenStreamAsync(AzuraStation station, IStreamPlayer player, CancellationToken ct = default)
    {
        if (_isShuttingDown)
            return;

        _logger.LogDebug("Try Open Stream...");

        if (_watchdog is null)
            StartWatchdog();

        bool preferHls = (await _settingsRepo.GetAsync()).PreferHlsStream;
        string[] candidates = GetCandidateUrls(station, preferHls);

        if (candidates.Length == 0)
            return;

        string? winner = await _streamUrlProber.ProbeFirstReachableAsync(candidates, ct);

        if (winner is null)
        {
            _logger.LogWarning("All stream URLs unreachable");
            return;
        }

        station.OnAirStreamUrl = winner;
        IsHlsActive = !string.IsNullOrEmpty(station.HlsUrl) && winner == station.HlsUrl;

        await _dispatcher.InvokeAsync(() => player.Open(winner));

        StreamOpened?.Invoke(this, station);
    }

    private void StartWatchdog()
    {
        StopWatchdog();
        _watchdog = new System.Timers.Timer(WatchdogIntervalMs);
        _watchdog.Elapsed += OnWatchdogElapsed;
        _watchdog.AutoReset = true;
        _watchdog.Start();
    }

    private void StopWatchdog()
    {
        if (_watchdog is null)
            return;

        _watchdog.Stop();
        _watchdog.Elapsed -= OnWatchdogElapsed;
        _watchdog.Dispose();
        _watchdog = null;
    }

    private void OnWatchdogElapsed(object? sender, System.Timers.ElapsedEventArgs e) => CheckPlaybackHealth();

    /// <summary>Watchdog tick: reconnects when playback should be running but is stale or stuck.</summary>
    internal void CheckPlaybackHealth()
    {
        if (!_shouldBePlaying || _currentStation is null || _player is null)
            return;

        IStreamPlayer player = _player;

        bool isStale = _currentState != StreamPlayerState.Playing &&
                       _currentState != StreamPlayerState.Buffering &&
                       _currentState != StreamPlayerState.Opening;

        bool isStuckBuffering = _currentState == StreamPlayerState.Buffering &&
                                _bufferingStartedAt != DateTime.MinValue &&
                                (DateTime.UtcNow - _bufferingStartedAt).TotalSeconds > BufferingTimeoutSeconds;

        if (isStale || isStuckBuffering || !player.HasSource)
        {
            if (TryQueueReconnect())
            {
                _logger.LogInformation("Watchdog: state={State} stuckBuffering={StuckBuffering}, reconnecting", _currentState, isStuckBuffering);
            }
        }
    }

    private void OnConnectivityChanged(object? sender, bool hasInternet)
    {
        if (hasInternet && _shouldBePlaying)
        {
            if (TryQueueReconnect())
            {
                _logger.LogInformation("Connectivity restored, reconnecting");
            }
        }
    }

    private async void TryReconnectAsync()
    {
        // La guardia è già acquisita dal chiamante tramite CompareExchange.
        // Se _currentStation o _player sono null, rilascia e torna.
        if (_currentStation is null || _player is null)
        {
            Interlocked.Exchange(ref _reconnectGuard, 0);
            return;
        }

        var ct = CurrentReconnectToken();
        _logger.LogDebug("Try reconnect...");

        await TryOpenStreamAsync(_currentStation, _player, ct);

        Interlocked.Exchange(ref _reconnectGuard, 0);
    }

    private void OnStateChanged(object? sender, StreamPlayerStateChangedEventArgs e)
    {
        _logger.LogDebug("StateChanged: {Previous} → {Current}", e.PreviousState, e.NewState);
        _currentState = e.NewState;

        if (e.NewState == StreamPlayerState.Buffering && e.PreviousState != StreamPlayerState.Buffering)
            _bufferingStartedAt = DateTime.UtcNow;
        else if (e.NewState != StreamPlayerState.Buffering)
            _bufferingStartedAt = DateTime.MinValue;

        IsPlaying = e.NewState == StreamPlayerState.Playing;
        IsBuffering = e.NewState is StreamPlayerState.Buffering or StreamPlayerState.Opening;

        _platformNowPlayingService.UpdatePlaybackState(IsPlaying);
        PlaybackStateChanged?.Invoke(this, IsPlaying);
    }

    private void OnMediaFailed(object? sender, string? errorMessage)
    {
        IsPlaying = false;
        IsBuffering = false;
        _platformNowPlayingService.UpdatePlaybackState(false);
        PlaybackStateChanged?.Invoke(this, false);

        if (_shouldBePlaying)
        {
            _logger.LogError("MediaFailed: {Error}", errorMessage);
            TryQueueReconnect();
        }
        else
        {
            ErrorOccurred?.Invoke(this, errorMessage);
        }
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        _logger.LogDebug("MediaEnded");
        if (_shouldBePlaying)
        {
            TryQueueReconnect();
        }
    }

    private void RenewReconnectCts()
    {
        lock (_ctsLock)
        {
            _reconnectCts.Cancel();
            _reconnectCts.Dispose();
            _reconnectCts = new CancellationTokenSource();
        }
    }

    private void CancelReconnect()
    {
        lock (_ctsLock) { _reconnectCts.Cancel(); }
    }

    private CancellationToken CurrentReconnectToken()
    {
        lock (_ctsLock) { return _reconnectCts.Token; }
    }

    private bool TryQueueReconnect()
    {
        if (_isShuttingDown)
            return false;

        if (Interlocked.CompareExchange(ref _reconnectGuard, 1, 0) != 0)
            return false;

        _dispatcher.Post(TryReconnectAsync);
        return true;
    }

    public void Shutdown()
    {
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;
        _shouldBePlaying = false;
        lock (_ctsLock)
        {
            _reconnectCts.Cancel();
            _reconnectCts.Dispose();
        }
        _currentStation = null;
        _bufferingStartedAt = DateTime.MinValue;
        IsPlaying = false;
        IsBuffering = false;
        _currentState = StreamPlayerState.None;
        Interlocked.Exchange(ref _reconnectGuard, 1);

        StopWatchdog();

        if (_isConnectivitySubscribed)
        {
            _networkMonitor.ConnectivityChanged -= OnConnectivityChanged;
            _isConnectivitySubscribed = false;
        }

        IStreamPlayer? player = _player;
        _player = null;

        if (player is null)
            return;

        player.StateChanged -= OnStateChanged;
        player.Failed -= OnMediaFailed;
        player.Ended -= OnMediaEnded;

        if (_dispatcher.IsUiThread)
        {
            player.Close();
            player.ClearMetadata();
        }

        _audioFocusManager.AbandonFocus();
        _platformNowPlayingService.Clear();
    }
}
