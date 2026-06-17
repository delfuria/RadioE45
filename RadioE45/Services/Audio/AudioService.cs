using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using RadioE45.Models;

namespace RadioE45.Services.Audio;

public class AudioService : IAudioService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPlatformNowPlayingService _platformNowPlayingService;
    private readonly ILogger<AudioService> _logger;
    private MediaElement? _mediaElement;
    private AzuraStation? _currentStation;
    private bool _shouldBePlaying;
    private int _reconnectGuard;   // 0 = idle, 1 = busy — accesso tramite Interlocked
    private MediaElementState _currentState = MediaElementState.None;
    private DateTime _bufferingStartedAt = DateTime.MinValue;
    private const double BufferingTimeoutSeconds = 12.0;
    private System.Timers.Timer? _watchdog;
    private const double WatchdogIntervalMs = 10000;
    private bool _isShuttingDown;
    private bool _isConnectivitySubscribed;
    private CancellationTokenSource _reconnectCts = new();

    public bool IsPlaying { get; private set; }
    public bool IsBuffering { get; private set; }
    public AzuraStation? CurrentStation => _currentStation;

    public event EventHandler<bool>? PlaybackStateChanged;
    public event EventHandler<string?>? ErrorOccurred;
    public event EventHandler<AzuraStation>? StreamOpened;

    public AudioService(IHttpClientFactory httpClientFactory, IPlatformNowPlayingService platformNowPlayingService, ILogger<AudioService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _platformNowPlayingService = platformNowPlayingService;
        _logger = logger;
    }
    
    public void Initialize(MediaElement mediaElement)
    {
        _isShuttingDown = false;

        if (_mediaElement is not null)
        {
            _mediaElement.StateChanged -= OnStateChanged;
            _mediaElement.MediaFailed -= OnMediaFailed;
            _mediaElement.MediaEnded -= OnMediaEnded;
            // L'elemento precedente può ancora suonare (es. stream avviato dal widget
            // mentre l'app era in background). Va fermato prima di rimpiazzarlo,
            // altrimenti rimane vivo in memoria e produce un doppio stream.
            _mediaElement.Stop();
            _mediaElement.Source = null;
        }

        _mediaElement = mediaElement;
        _mediaElement.ShouldAutoPlay = false;
        _mediaElement.ShouldLoopPlayback = false;
        _mediaElement.ShouldShowPlaybackControls = false;

        _mediaElement.StateChanged += OnStateChanged;
        _mediaElement.MediaFailed += OnMediaFailed;
        _mediaElement.MediaEnded += OnMediaEnded;

        if (!_isConnectivitySubscribed)
        {
            Connectivity.ConnectivityChanged += OnConnectivityChanged;
            _isConnectivitySubscribed = true;
        }

        // Se lo stream era attivo (es. ripreso dal widget), riavvia sul nuovo elemento
        // senza aspettare il watchdog.
        if (_shouldBePlaying && _currentStation is not null)
        {
            RenewReconnectCts();
            Interlocked.Exchange(ref _reconnectGuard, 0);
            TryQueueReconnect();
        }
    }

    public async Task PlayAsync(AzuraStation station)
    {
        if (_mediaElement is null)
            return;

        _logger.LogDebug("Start radio streaming...");

        _currentStation = station;
        _shouldBePlaying = true;
        _bufferingStartedAt = DateTime.MinValue;
        RenewReconnectCts();
        Interlocked.Exchange(ref _reconnectGuard, 0);
        TryQueueReconnect();
    }

    // Per uno stream live non ha senso un "vero" pause: MediaElement.Pause() lascia la
    // connessione aperta e bufferizza, così alla ripresa si sentirebbe audio non più in diretta.
    // Per questo la pausa chiude la sorgente (come uno stop) ma mantiene stazione e metadati,
    // così la notifica/widget resta visibile in stato "in pausa" e Resume può riaprire la diretta.
    public async Task PauseAsync()
    {
        MediaElement? mediaElement = _mediaElement;
        if (mediaElement is null)
            return;

        _logger.LogDebug("Pause station streaming...");

        _shouldBePlaying = false;
        _reconnectCts.Cancel();
        Interlocked.Exchange(ref _reconnectGuard, 0);
        StopWatchdog();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            mediaElement.Stop();
            mediaElement.Source = null;
        });

        _platformNowPlayingService.UpdatePlaybackState(false);
    }

    // Riprende riaprendo lo stream da capo (come PlayAsync), in modo da tornare sul punto
    // attuale della diretta invece di proseguire da un buffer ormai non più sincronizzato.
    public Task ResumeAsync()
    {
        if (_mediaElement is null || _currentStation is null)
            return Task.CompletedTask;

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
        MediaElement? mediaElement = _mediaElement;
        if (mediaElement is null)
            return;

        _logger.LogDebug("Stop station streaming...");

        _shouldBePlaying = false;
        _reconnectCts.Cancel();
        Interlocked.Exchange(ref _reconnectGuard, 0);
        _currentStation = null;
        StopWatchdog();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            mediaElement.Stop();
            mediaElement.Source = null;
            mediaElement.MetadataTitle = string.Empty;
            mediaElement.MetadataArtist = string.Empty;
            mediaElement.MetadataArtworkUrl = string.Empty;
        });

        _platformNowPlayingService.Clear();
    }

    // Chiamato da OnTaskRemoved (swipe), che Android dispatcha sul main thread.
    // Se siamo già sul main thread, Stop() viene chiamato direttamente (nessun deadlock).
    // Se per qualunque ragione siamo su un thread diverso, lo posta e torna subito.
    public void StopImmediate()
    {
        _shouldBePlaying = false;
        _reconnectCts.Cancel();
        Interlocked.Exchange(ref _reconnectGuard, 0);
        _currentStation = null;
        StopWatchdog();

        _logger.LogDebug("Stop immediate station streaming...");

        MediaElement? mediaElement = _mediaElement;
        if (mediaElement is null)
            return;

        if (MainThread.IsMainThread)
        {
            mediaElement.Stop();
            mediaElement.Source = null;
            mediaElement.MetadataTitle = string.Empty;
            mediaElement.MetadataArtist = string.Empty;
            mediaElement.MetadataArtworkUrl = string.Empty;
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                mediaElement.Stop();
                mediaElement.Source = null;
                mediaElement.MetadataTitle = string.Empty;
                mediaElement.MetadataArtist = string.Empty;
                mediaElement.MetadataArtworkUrl = string.Empty;
            });
        }

        _platformNowPlayingService.Clear();
    }

    public void SetVolume(double volume)
    {
        MediaElement? mediaElement = _mediaElement;
        if (mediaElement is null)
            return;

        double clamped = Math.Clamp(volume, 0.0, 1.0);
        MainThread.BeginInvokeOnMainThread(() => mediaElement.Volume = clamped);
    }

    public void UpdateMetadata(string artist, string title, string? artworkUrl = null, int? elapsedSeconds = null, int? durationSeconds = null)
    {
        MediaElement? mediaElement = _mediaElement;
        if (mediaElement is null)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            mediaElement.MetadataTitle = title;
            mediaElement.MetadataArtist = artist;
            mediaElement.MetadataArtworkUrl = artworkUrl ?? string.Empty;
        });

        _platformNowPlayingService.UpdateMetadata(artist, title, artworkUrl, elapsedSeconds, durationSeconds, IsPlaying);
    }
    
    private async Task TryOpenStreamAsync(AzuraStation station, MediaElement mediaElement, CancellationToken ct = default)
    {
        if (_isShuttingDown)
            return;

        _logger.LogDebug("Try Open Stream...");

        if (_watchdog is null)
            StartWatchdog();

        // Priority: last known working URL → primary → fallback
        string[] candidates = new[] { station.OnAirStreamUrl, station.HlsUrl, station.StreamUrl, station.StreamUrlFallback }
            .Where(u => !string.IsNullOrEmpty(u))
            .Distinct()
            .ToArray()!;

        if (candidates.Length == 0)
            return;

        string? winner = await ProbeFirstReachableAsync(candidates, ct);

        if (winner is null)
        {
            _logger.LogWarning("All stream URLs unreachable");
            return;
        }

        station.OnAirStreamUrl = winner;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            mediaElement.ShouldAutoPlay = true;
            mediaElement.Source = MediaSource.FromUri(winner);
        });

        StreamOpened?.Invoke(this, station);
    }

    // Probes all candidate URLs in parallel and returns the first reachable one.
    // Remaining probes are cancelled as soon as a winner is found.
    private async Task<string?> ProbeFirstReachableAsync(string[] urls, CancellationToken ct)
    {
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        List<Task<string?>> tasks = urls.Select(url => ProbeUrlAsync(url, probeCts.Token)).ToList();

        while (tasks.Count > 0)
        {
            Task<string?> done = await Task.WhenAny(tasks);
            tasks.Remove(done);

            string? result = await done;
            if (result is not null)
            {
                probeCts.Cancel();
                return result;
            }
        }

        return null;
    }

    private async Task<string?> ProbeUrlAsync(string url, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            HttpClient client = _httpClientFactory.CreateClient("AzuraCast");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Stream probe OK: {Url}", url);
                return url;
            }

            _logger.LogWarning("Stream probe: HTTP {Status} for {Url}", (int)response.StatusCode, url);
            return null;
        }
        catch (OperationCanceledException)
        {
            if (!ct.IsCancellationRequested)
                _logger.LogWarning("Stream probe timed out: {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stream probe failed for {Url}", url);
            return null;
        }
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

    private void OnWatchdogElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_shouldBePlaying || _currentStation is null || _mediaElement is null)
            return;

        MediaElement mediaElement = _mediaElement;

        bool isStale = _currentState != MediaElementState.Playing &&
                       _currentState != MediaElementState.Buffering &&
                       _currentState != MediaElementState.Opening;

        bool isStuckBuffering = _currentState == MediaElementState.Buffering &&
                                _bufferingStartedAt != DateTime.MinValue &&
                                (DateTime.UtcNow - _bufferingStartedAt).TotalSeconds > BufferingTimeoutSeconds;

        if (isStale || isStuckBuffering || mediaElement.Source is null)
        {
            if (TryQueueReconnect())
            {
                _logger.LogInformation("Watchdog: state={State} stuckBuffering={StuckBuffering}, reconnecting", _currentState, isStuckBuffering);
            }
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet && _shouldBePlaying)
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
        // Se _currentStation o _mediaElement sono null, rilascia e torna.
        if (_currentStation is null || _mediaElement is null)
        {
            Interlocked.Exchange(ref _reconnectGuard, 0);
            return;
        }

        var ct = _reconnectCts.Token;
        _logger.LogDebug("Try reconnect...");

        await TryOpenStreamAsync(_currentStation, _mediaElement, ct);

        Interlocked.Exchange(ref _reconnectGuard, 0);
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        _logger.LogDebug("StateChanged: {Previous} → {Current}", e.PreviousState, e.NewState);
        _currentState = e.NewState;

        if (e.NewState == MediaElementState.Buffering && e.PreviousState != MediaElementState.Buffering)
            _bufferingStartedAt = DateTime.UtcNow;
        else if (e.NewState != MediaElementState.Buffering)
            _bufferingStartedAt = DateTime.MinValue;

        IsPlaying = e.NewState == MediaElementState.Playing;
        IsBuffering = e.NewState is MediaElementState.Buffering or MediaElementState.Opening;

        if (e.NewState == MediaElementState.Playing && _mediaElement is { ShouldAutoPlay: true })
            _mediaElement.ShouldAutoPlay = false;

        _platformNowPlayingService.UpdatePlaybackState(IsPlaying);
        PlaybackStateChanged?.Invoke(this, IsPlaying);
    }

    private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    {
        IsPlaying = false;
        IsBuffering = false;
        _platformNowPlayingService.UpdatePlaybackState(false);
        PlaybackStateChanged?.Invoke(this, false);

        if (_shouldBePlaying)
        {
            _logger.LogError("MediaFailed: {Error}", e.ErrorMessage);
            TryQueueReconnect();
        }
        else
        {
            ErrorOccurred?.Invoke(this, e.ErrorMessage);
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
        _reconnectCts.Cancel();
        _reconnectCts.Dispose();
        _reconnectCts = new CancellationTokenSource();
    }

    private bool TryQueueReconnect()
    {
        if (_isShuttingDown)
            return false;

        if (Interlocked.CompareExchange(ref _reconnectGuard, 1, 0) != 0)
            return false;

        MainThread.BeginInvokeOnMainThread(TryReconnectAsync);
        return true;
    }

    public void Shutdown()
    {
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;
        _shouldBePlaying = false;
        _reconnectCts.Cancel();
        _reconnectCts.Dispose();
        _currentStation = null;
        _bufferingStartedAt = DateTime.MinValue;
        IsPlaying = false;
        IsBuffering = false;
        _currentState = MediaElementState.None;
        Interlocked.Exchange(ref _reconnectGuard, 1);

        StopWatchdog();

        if (_isConnectivitySubscribed)
        {
            Connectivity.ConnectivityChanged -= OnConnectivityChanged;
            _isConnectivitySubscribed = false;
        }

        MediaElement? mediaElement = _mediaElement;
        _mediaElement = null;

        if (mediaElement is null)
            return;

        mediaElement.StateChanged -= OnStateChanged;
        mediaElement.MediaFailed -= OnMediaFailed;
        mediaElement.MediaEnded -= OnMediaEnded;

        if (MainThread.IsMainThread)
        {
            mediaElement.Stop();
            mediaElement.Source = null;
            mediaElement.MetadataTitle = string.Empty;
            mediaElement.MetadataArtist = string.Empty;
            mediaElement.MetadataArtworkUrl = string.Empty;
        }

        _platformNowPlayingService.Clear();
    }
}
