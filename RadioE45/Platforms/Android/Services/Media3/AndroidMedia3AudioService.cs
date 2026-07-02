using Android.App;
using Android.Content;
using AndroidX.Media3.Common;
using AndroidX.Media3.Session;
using CommunityToolkit.Maui.Views;
using Google.Common.Util.Concurrent;
using Java.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using RadioE45.Models;

namespace RadioE45.Services.Audio;

// Fase 3 (Media3/Android Auto) — vedi docs/carplay/Phase3-Media3-ActionPlan.md §3.5.
// Bridge lato app verso RadioLibraryService (§3.4): implementa IAudioService, lo stesso
// contratto usato da OnAirViewModel su tutte le piattaforme, parlando con l'unica
// MediaLibrarySession tramite un MediaController connesso in-process — l'approccio
// raccomandato da Google per un client che vuole controllare un MediaSessionService in
// esecuzione nello stesso processo (l'alternativa "holder statico" sul Player, proposta
// in Fase 2B per il vecchio MediaSession, è stata scartata qui a favore di questo).
// La risoluzione mediaId → URL reale resta lato server (RadioLibrarySessionCallback,
// §3.3): questa classe invia solo MediaId logici, non URL.
internal sealed class AndroidMedia3AudioService : IAudioService
{
    private readonly ILogger<AndroidMedia3AudioService> _logger;
    private readonly object _connectLock = new();

    private Task<MediaController>? _controllerConnectTask;
    private MediaController? _controller;
    private PlayerListener? _playerListener;
    private MediaItem? _currentPlayableItem;

    private AzuraStation? _currentStation;
    private bool _shouldBePlaying;
    private int _reconnectGuard;
    private DateTime _bufferingStartedAt = DateTime.MinValue;
    private System.Timers.Timer? _watchdog;

    // Stessi valori di AudioService.cs (§3.2/§6 del piano: "stesso rigore", non reinventati).
    private const double BufferingTimeoutSeconds = 12.0;
    private const double WatchdogIntervalMs = 10000;

    public bool IsPlaying { get; private set; }
    public bool IsBuffering { get; private set; }
    public AzuraStation? CurrentStation => _currentStation;

    public event EventHandler<bool>? PlaybackStateChanged;
    public event EventHandler<string?>? ErrorOccurred;
    public event EventHandler<AzuraStation>? StreamOpened;

    public AndroidMedia3AudioService(ILogger<AndroidMedia3AudioService> logger)
    {
        _logger = logger;
    }

    // No-op su Android (vedi §4.2 del piano): la riproduzione non dipende più da un
    // MediaElement. Il parametro resta solo per compatibilità con OnAirPage.xaml.cs,
    // condiviso fra tutte le piattaforme.
    public void Initialize(MediaElement mediaElement)
    {
    }

    public async Task PlayAsync(AzuraStation station)
    {
        _currentStation = station;
        _shouldBePlaying = true;
        _bufferingStartedAt = DateTime.MinValue;

        EnsureServiceStarted();

        MediaController controller;
        try
        {
            controller = await EnsureControllerAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AndroidMedia3AudioService: connessione al MediaController fallita");
            ErrorOccurred?.Invoke(this, ex.Message);
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            MediaItem item = new MediaItem.Builder().SetMediaId(station.Id.ToString())!.Build()!;
            controller.SetMediaItem(item);
            controller.Prepare();
            controller.Play();
        });

        // Ottimistico, stesso momento in cui il vecchio AudioService.TryOpenStreamAsync
        // sollevava StreamOpened: la vera risoluzione mediaId → URL avviene lato server in
        // modo asincrono (RadioLibrarySessionCallback.OnSetMediaItems, §3.3) — attendere la
        // conferma richiederebbe un round-trip che il vecchio codice non aveva.
        StreamOpened?.Invoke(this, station);
    }

    // Per uno stream live non ha senso un "vero" pause (bufferizzerebbe restando indietro
    // rispetto alla diretta): si ferma la sorgente mantenendo però stazione e MediaItem,
    // stessa scelta di AudioService.PauseAsync.
    public async Task PauseAsync()
    {
        if (_currentStation is null || _controller is null)
            return;

        _shouldBePlaying = false;
        StopWatchdog();

        MediaController controller = _controller;
        await MainThread.InvokeOnMainThreadAsync(controller.Stop);
    }

    // Riprende riaprendo lo stream da capo (nuova risoluzione URL lato server), stessa
    // scelta di AudioService.ResumeAsync — non "continua" da un buffer ormai non più
    // sincronizzato con la diretta.
    public Task ResumeAsync()
    {
        return _currentStation is null ? Task.CompletedTask : PlayAsync(_currentStation);
    }

    public async Task StopAsync()
    {
        _shouldBePlaying = false;
        _currentStation = null;
        StopWatchdog();

        if (_controller is not { } controller)
            return;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            controller.Stop();
            controller.ClearMediaItems();
        });
    }

    // Chiamato da un eventuale OnTaskRemoved lato app (oggi, con RadioLibraryService già
    // attivo, il cleanup su swipe è gestito direttamente dal service — §3.4 — non più
    // tramite questo bridge; il metodo resta per conformità all'interfaccia e per
    // eventuali altri chiamanti sincroni futuri). Non deve bloccare il thread chiamante.
    public void StopImmediate()
    {
        _shouldBePlaying = false;
        _currentStation = null;
        StopWatchdog();

        if (_controller is not { } controller)
            return;

        void StopNow()
        {
            controller.Stop();
            controller.ClearMediaItems();
        }

        if (MainThread.IsMainThread)
            StopNow();
        else
            MainThread.BeginInvokeOnMainThread(StopNow);
    }

    public void SetVolume(double volume)
    {
        if (_controller is not { } controller)
            return;

        float clamped = (float)Math.Clamp(volume, 0.0, 1.0);
        MainThread.BeginInvokeOnMainThread(() => controller.Volume = clamped);
    }

    // Aggiorna solo i metadati del brano in onda (titolo/artista/artwork), preservando
    // l'URI già risolto: BuildUpon() mantiene invariato tutto il resto del MediaItem.
    // Non verificato su device (aggiunto da questa fase, §7): da confermare che
    // ReplaceMediaItem non causi un ribuffering percepibile su uno stream live.
    // elapsedSeconds/durationSeconds non sono usati: per Media3 la posizione di
    // riproduzione la riporta il Player stesso, non ha senso iniettarla dall'esterno.
    public void UpdateMetadata(string artist, string title, string? artworkUrl = null, int? elapsedSeconds = null, int? durationSeconds = null)
    {
        if (_controller is not { } controller || _currentPlayableItem is not { } current)
            return;

        MediaMetadata.Builder metadataBuilder = (current.MediaMetadata?.BuildUpon() ?? new MediaMetadata.Builder())
            .SetTitle(title)!
            .SetArtist(artist)!;

        if (!string.IsNullOrEmpty(artworkUrl))
            metadataBuilder = metadataBuilder.SetArtworkUri(Android.Net.Uri.Parse(artworkUrl))!;

        MediaItem updated = current.BuildUpon()!.SetMediaMetadata(metadataBuilder.Build())!.Build()!;

        MainThread.BeginInvokeOnMainThread(() => controller.ReplaceMediaItem(0, updated));
    }

    public void Shutdown()
    {
        _shouldBePlaying = false;
        _currentStation = null;
        IsPlaying = false;
        IsBuffering = false;
        StopWatchdog();

        MediaController? controller = _controller;
        PlayerListener? listener = _playerListener;
        _controller = null;
        _playerListener = null;
        _controllerConnectTask = null;
        _currentPlayableItem = null;

        if (controller is null)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (listener is not null)
                controller.RemoveListener(listener);
            controller.Release();
        });
    }

    // Fix #2 (Fase 1.A), riportato qui come richiesto da §3.5 del piano. A differenza del
    // vecchio codice, un fallimento qui non è fatale: il bind del MediaController che
    // segue può comunque avviare il service (Media3 promuove da sé il service a
    // foreground quando il Player ha uno stato riproducibile — vedi nota "bug #1" in
    // RadioLibraryService, §3.4), quindi si logga e si prosegue.
    private void EnsureServiceStarted()
    {
        Context context = Android.App.Application.Context;
        Intent intent = new(context, typeof(RadioLibraryService));

        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                context.StartForegroundService(intent);
            else
                context.StartService(intent);
        }
        catch (Exception ex) when (
            ex is Java.Lang.IllegalStateException ||
            ex is Android.App.ForegroundServiceStartNotAllowedException)
        {
            _logger.LogWarning(ex, "AndroidMedia3AudioService: avvio del foreground service bloccato dal sistema");
        }
    }

    private Task<MediaController> EnsureControllerAsync()
    {
        lock (_connectLock)
        {
            if (_controllerConnectTask is null || _controllerConnectTask.IsFaulted)
                _controllerConnectTask = ConnectControllerAsync();
            return _controllerConnectTask;
        }
    }

    private async Task<MediaController> ConnectControllerAsync()
    {
        IListenableFuture future = await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Context context = Android.App.Application.Context;
            ComponentName component = new(context, Java.Lang.Class.FromType(typeof(RadioLibraryService)));
            SessionToken token = new(context, component);
            return new MediaController.Builder(context, token).BuildAsync()!;
        });

        MediaController controller = await AwaitFutureAsync<MediaController>(future);

        _playerListener = new PlayerListener(this);
        MainThread.BeginInvokeOnMainThread(() => controller.AddListener(_playerListener));

        _controller = controller;
        return controller;
    }

    // Guava's Futures.getDone/addCallback non sono esposti in questo binding (stesso
    // problema affrontato in RadioLibrarySessionCallback, §3.3, ma lì al contrario:
    // producevamo un IListenableFuture, qui lo consumiamo). Si adatta manualmente con
    // IListenableFuture.AddListener(IRunnable, IExecutor) — l'API Java "grezza" —
    // usando un executor che esegue subito sul thread che completa la future.
    private static Task<T> AwaitFutureAsync<T>(IListenableFuture future) where T : Java.Lang.Object
    {
        TaskCompletionSource<T> tcs = new();

        future.AddListener(new ActionRunnable(() =>
        {
            try
            {
                Java.Lang.Object? result = ((Java.Util.Concurrent.IFuture)future).Get() as Java.Lang.Object;
                tcs.TrySetResult(result!.JavaCast<T>());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }), DirectExecutor.Instance);

        return tcs.Task;
    }

    // Scoperto testando su emulatore (2026-07-02): OnIsPlayingChanged scatta una sola
    // volta (al primo Play, isPlaying=true) e MAI più — anche quando OnPlaybackStateChanged
    // conferma correttamente la transizione a Idle dopo Stop/Pause (log: state=3 poi
    // state=1, ma nessun OnIsPlayingChanged intermedio). È una lacuna nota della dispatch
    // dei singoli callback granulari di Media3/ExoPlayer 1.8 per alcune transizioni
    // innescate da comandi client (stop()); la guida ufficiale raccomanda infatti di
    // preferire OnEvents, invocato dopo OGNI batch di cambiamenti, e leggere lo stato
    // reale dal Player invece di fidarsi dei singoli callback. Da qui in poi IsPlaying
    // viene ricalcolato qui, non più in un handler dedicato a OnIsPlayingChanged.
    private void HandlePlayerEvents(IPlayer? player)
    {
        if (player is null)
            return;

        // IsBuffering va aggiornato PRIMA di lanciare PlaybackStateChanged: l'evento è
        // sincrono e OnAirViewModel, nel suo handler, rilegge subito _audioService.IsBuffering
        // — se l'ordine fosse invertito (bug osservato: spinner sempre visibile durante il
        // play) il ViewModel vedrebbe ancora il valore precedente (true, residuo del
        // buffering iniziale) invece di quello aggiornato in questo stesso batch di eventi.
        int playbackState = player.PlaybackState;
        bool wasBuffering = IsBuffering;
        IsBuffering = playbackState == AndroidX.Media3.Common.BasePlayer.InterfaceConsts.StateBuffering;

        if (IsBuffering && !wasBuffering)
            _bufferingStartedAt = DateTime.UtcNow;
        else if (!IsBuffering)
            _bufferingStartedAt = DateTime.MinValue;

        bool isPlaying = player.IsPlaying;
        if (isPlaying != IsPlaying)
        {
            _logger.LogDebug("AndroidMedia3AudioService: OnEvents isPlaying={IsPlaying} shouldBePlaying={ShouldBePlaying}", isPlaying, _shouldBePlaying);
            IsPlaying = isPlaying;
            PlaybackStateChanged?.Invoke(this, isPlaying);
        }

        if (_shouldBePlaying && _watchdog is null)
            StartWatchdog();
    }

    private void HandlePlayerError(PlaybackException? error)
    {
        _logger.LogError("AndroidMedia3AudioService: PlayerError {Message}", error?.Message);
        IsPlaying = false;
        IsBuffering = false;
        PlaybackStateChanged?.Invoke(this, false);

        if (_shouldBePlaying)
            TryQueueReconnect();
        else
            ErrorOccurred?.Invoke(this, error?.Message);
    }

    private void HandleMediaItemTransition(MediaItem? mediaItem)
    {
        _currentPlayableItem = mediaItem;
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

    // Non porta 1:1 la rilevazione "stato stantio" di AudioService.OnWatchdogElapsed (lì
    // possibile perché il MediaElement esponeva lo stato in modo sincrono); qui, senza un
    // device per verificare quali transizioni di stato il Player riporta realmente in
    // scenari di stallo, ci si limita al caso già osservabile con certezza dagli eventi
    // Player.Listener: buffering bloccato oltre soglia. Da estendere se il test su device
    // (§7) mostra altri casi di stallo non coperti da OnPlayerError/buffering-bloccato.
    private void OnWatchdogElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_shouldBePlaying || _currentStation is null)
            return;

        bool isStuckBuffering = IsBuffering &&
            _bufferingStartedAt != DateTime.MinValue &&
            (DateTime.UtcNow - _bufferingStartedAt).TotalSeconds > BufferingTimeoutSeconds;

        if (isStuckBuffering)
        {
            _logger.LogInformation("AndroidMedia3AudioService: watchdog — buffering bloccato, riconnessione");
            TryQueueReconnect();
        }
    }

    // Stesso principio di thread-safety di AudioService (fix #14, §6 del piano): un solo
    // reconnect alla volta, guardia via Interlocked invece del CancellationTokenSource
    // usato lì (qui non serve annullare un probe HTTP in corso lato client: la
    // ri-risoluzione dell'URL avviene lato server in RadioLibrarySessionCallback).
    private void TryQueueReconnect()
    {
        if (Interlocked.CompareExchange(ref _reconnectGuard, 1, 0) != 0)
            return;

        AzuraStation? station = _currentStation;
        if (station is null)
        {
            Interlocked.Exchange(ref _reconnectGuard, 0);
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await PlayAsync(station);
            }
            finally
            {
                Interlocked.Exchange(ref _reconnectGuard, 0);
            }
        });
    }

    private sealed class PlayerListener : Java.Lang.Object, IPlayerListener
    {
        private readonly AndroidMedia3AudioService _owner;

        public PlayerListener(AndroidMedia3AudioService owner) => _owner = owner;

        public void OnEvents(IPlayer? player, PlayerEvents? events) => _owner.HandlePlayerEvents(player);

        public void OnPlayerError(PlaybackException? error) => _owner.HandlePlayerError(error);

        public void OnMediaItemTransition(MediaItem? mediaItem, int reason) => _owner.HandleMediaItemTransition(mediaItem);
    }

    // IRunnable/IExecutor "grezzi" per consumare una IListenableFuture (vedi AwaitFutureAsync):
    // stesso ponte concettuale di CallbackToFutureAdapter usato in RadioLibrarySessionCallback
    // (§3.3), qui applicato al verso opposto (consumo anziché produzione di una future).
    private sealed class ActionRunnable : Java.Lang.Object, Java.Lang.IRunnable
    {
        private readonly Action _action;

        public ActionRunnable(Action action) => _action = action;

        public void Run() => _action();
    }

    private sealed class DirectExecutor : Java.Lang.Object, Java.Util.Concurrent.IExecutor
    {
        public static readonly DirectExecutor Instance = new();

        public void Execute(Java.Lang.IRunnable? command) => command?.Run();
    }
}
