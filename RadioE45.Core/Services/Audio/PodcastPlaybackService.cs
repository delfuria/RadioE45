using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Audio;

// Player dedicato agli episodi podcast: player proprio (non quello della radio live), foreground
// only per la v1 — non integra Media3/RadioPlaybackService (Android) né il Now Playing di sistema
// (iOS/Windows), che restano di pertinenza esclusiva di IAudioService. Vedi PODCAST-FEATURE.md.
// Logica condivisa da MAUI (MediaElement) e Uno (MediaPlayer) tramite IPodcastMediaPlayer:
// spostata senza modifiche di comportamento dal PodcastPlayerService MAUI (S7b).
public class PodcastPlaybackService : IPodcastPlayerService
{
    private static readonly TimeSpan ProgressSaveInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StartCheckDelay = TimeSpan.FromMilliseconds(1500);
    private const int StartAttempts = 5;

    private readonly IAudioService _audioService;
    private readonly IPodcastProgressRepository _progressRepository;
    private readonly IUiDispatcher _dispatcher;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private IPodcastMediaPlayer? _player;
    private AzuraStation? _station;
    private TimeSpan? _pendingResumePosition;
    // Vero tra "seek di ripresa avviato" e "seek completato": i campioni di posizione che arrivano
    // in questa finestra sono pre-seek (vicini a 0) e vanno soppressi, altrimenti la UI mostra un
    // flash a 0 prima che il campione post-seek corretto arrivi.
    private bool _resumeSeekInFlight;
    // Incrementato a ogni PlayAsync: un campione di posizione tardivo riferito a un episodio ormai
    // sostituito (utente ha cliccato un altro episodio nel frattempo) non deve consumare/eseguire
    // il seek con il target del nuovo episodio già impostato in _pendingResumePosition.
    private int _playToken;
    private int _pendingResumeToken;
    private DateTimeOffset _lastProgressSaveAt = DateTimeOffset.MinValue;
    private string? _lastFailureMessage;
    // Last real position sample of the current episode: used to save progress when the player
    // can no longer report it (e.g. MAUI's page-hosted MediaElement being torn down while the
    // page disappears reads 0, which would overwrite the saved position with the start).
    private TimeSpan _lastKnownPosition;

    public PodcastPlaybackService(IAudioService audioService, IPodcastProgressRepository progressRepository,
        IUiDispatcher dispatcher, ILogger logger)
        : this(audioService, progressRepository, dispatcher, TimeProvider.System, logger)
    {
    }

    internal PodcastPlaybackService(IAudioService audioService, IPodcastProgressRepository progressRepository,
        IUiDispatcher dispatcher, TimeProvider time, ILogger logger)
    {
        _audioService = audioService;
        _progressRepository = progressRepository;
        _dispatcher = dispatcher;
        _time = time;
        _logger = logger;
    }

    public bool IsPlaying { get; private set; }
    public AzuraCastPodcastEpisode? CurrentEpisode { get; private set; }
    public TimeSpan Position => _player?.Position ?? TimeSpan.Zero;
    public TimeSpan Duration => _player?.Duration ?? TimeSpan.Zero;

    public event EventHandler<bool>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler? EpisodeCompleted;
    public event EventHandler<string?>? PlaybackFailed;
    public event EventHandler<string>? EpisodeStopped;
    public event EventHandler<PodcastProgressSavedEventArgs>? ProgressSaved;

    /// <summary>Connects the head's player (MAUI: the page's MediaElement; Uno: created once).</summary>
    public void AttachPlayer(IPodcastMediaPlayer player)
    {
        if (_player is not null)
        {
            _player.StateChanged -= OnStateChanged;
            _player.Ended -= OnMediaEnded;
            _player.PositionChanged -= OnMediaPositionChanged;
            _player.Failed -= OnMediaFailed;
        }

        _player = player;
        _player.StateChanged += OnStateChanged;
        _player.Ended += OnMediaEnded;
        _player.PositionChanged += OnMediaPositionChanged;
        _player.Failed += OnMediaFailed;
    }

    // Lettura rapida e senza effetti collaterali del progresso salvato — usata dal ViewModel per
    // valorizzare la barra di avanzamento PRIMA di avviare la riproduzione, cosicché barra e punto
    // di ripresa effettivo derivino dallo stesso identico valore (vedi PlayAsync sotto).
    public async Task<int> GetResumePositionSecondsAsync(AzuraStation station, AzuraCastPodcastEpisode episode)
    {
        PodcastEpisodeProgress? saved = await _progressRepository.GetAsync(station.StationId, episode.Id);
        return saved is { IsCompleted: false } && saved.PositionSeconds > 3 ? saved.PositionSeconds : 0;
    }

    public async Task PlayAsync(AzuraStation station, AzuraCastPodcastEpisode episode, int startPositionSeconds)
    {
        if (_player is null)
            return;

        int token = ++_playToken;

        // Un solo player attivo alla volta: avviare un episodio ferma la radio live.
        await _audioService.StopAsync();

        _station = station;
        CurrentEpisode = episode;
        _lastProgressSaveAt = DateTimeOffset.MinValue;
        _pendingResumePosition = startPositionSeconds > 0 ? TimeSpan.FromSeconds(startPositionSeconds) : null;
        _pendingResumeToken = token;
        _resumeSeekInFlight = false;
        _lastKnownPosition = TimeSpan.FromSeconds(startPositionSeconds);

        _lastFailureMessage = null;
        await OpenAndPlayWithRetryAsync(episode.MediaUrl);
    }

    // Il player nativo (AVPlayer su iOS/macCatalyst, incluso) può non essere ancora pronto al
    // primissimo Source impostato subito dopo l'aggancio — la richiesta viene scartata in silenzio
    // (nessuna eccezione, nessun evento). La radio live copre la stessa race con un watchdog
    // continuo (vedi LiveStreamAudioService); qui basta un breve retry limitato all'avvio.
    // Un errore reale (es. 403 dal server) arriva invece via Failed — in quel caso niente
    // retry, l'errore viene propagato subito tramite PlaybackFailed.
    //
    // Il seek di ripresa NON va fatto qui né all'apertura/StateChanged=Playing: in entrambi i
    // casi il player nativo non è ancora davvero "seekable" (metadata caricati ma decodifica non
    // partita) e il seek fallisce in silenzio (Task completa, posizione resta 0). Il seek va
    // fatto sul primo campione REALE di posizione — a quel punto il player sta decodificando per
    // davvero e il seek ha effetto. Vedi OnMediaPositionChanged.
    private async Task OpenAndPlayWithRetryAsync(string url)
    {
        IPodcastMediaPlayer? player = _player;
        if (player is null)
            return;

        for (int attempt = 0; attempt < StartAttempts; attempt++)
        {
            await _dispatcher.InvokeAsync(() =>
            {
                // Se c'è un resume pendente, il play parte comunque subito (il seek ha effetto solo
                // dopo il primo campione reale di posizione, vedi commento sopra) — silenziare
                // l'audio evita che l'utente senta l'inizio dell'episodio dal secondo 0 prima che
                // lo scatti verso la posizione salvata. Ripristinato in SeekToResumeAsync.
                player.Volume = _pendingResumePosition is not null ? 0 : 1;
                player.Open(url);
            });

            await Task.Delay(StartCheckDelay, _time);

            if (_lastFailureMessage is not null)
            {
                _logger.LogWarning("Podcast playback failed: {Message} — {Url}", _lastFailureMessage, url);
                PlaybackFailed?.Invoke(this, _lastFailureMessage);
            }

            if (player.State is StreamPlayerState.Playing or StreamPlayerState.Buffering
                or StreamPlayerState.Opening or StreamPlayerState.Paused)
                return;
        }

        _logger.LogWarning("Podcast playback failed to start after retries: {Url}", url);
        PlaybackFailed?.Invoke(this, null);
    }

    private void OnMediaFailed(object? sender, string? errorMessage)
    {
        _lastFailureMessage = errorMessage;
    }

    public async Task PauseAsync()
    {
        IPodcastMediaPlayer? player = _player;
        if (player is null)
            return;

        await _dispatcher.InvokeAsync(player.Pause);
        await SaveProgressAsync();
    }

    public async Task ResumeAsync()
    {
        IPodcastMediaPlayer? player = _player;
        if (player is null)
            return;

        await _dispatcher.InvokeAsync(player.Play);
    }

    public async Task StopAsync()
    {
        IPodcastMediaPlayer? player = _player;
        if (player is null)
            return;

        AzuraCastPodcastEpisode? episode = CurrentEpisode;
        await SaveProgressAsync();

        // Unload before closing: samples the player emits while stopping (0) must be ignored.
        CurrentEpisode = null;
        await _dispatcher.InvokeAsync(player.Close);

        IsPlaying = false;
        PlaybackStateChanged?.Invoke(this, false);
        if (episode is not null)
            EpisodeStopped?.Invoke(this, episode.Id);
    }

    public async Task SeekAsync(TimeSpan position)
    {
        if (_player is null)
            return;

        await _player.SeekAsync(position);
    }

    private void OnStateChanged(object? sender, StreamPlayerStateChangedEventArgs e)
    {
        IsPlaying = e.NewState == StreamPlayerState.Playing;
        PlaybackStateChanged?.Invoke(this, IsPlaying);
    }

    private void OnMediaPositionChanged(object? sender, TimeSpan position)
    {
        // No episode loaded (stopped): the player may still report a last sample (0 after Stop);
        // forwarding it would show 0:00 and, worse, be saved over the real position.
        if (CurrentEpisode is null)
            return;

        // Primo campione di posizione reale dopo il play: qui il player sta davvero decodificando,
        // non più solo "aperto" — il seek ha effetto reale solo a questo punto (vedi commento su
        // OpenAndPlayWithRetryAsync). Token-guard: ignora se nel frattempo è stato selezionato
        // un altro episodio (_playToken avanzato).
        if (_pendingResumePosition is { } resume && _pendingResumeToken == _playToken)
        {
            _pendingResumePosition = null;
            _resumeSeekInFlight = true;
            _ = SeekToResumeAsync(resume, _pendingResumeToken);
            return; // campione pre-seek (vicino a 0): non propagarlo alla UI, provocherebbe un flash
        }

        if (_resumeSeekInFlight)
            return; // seek ancora in corso: eventuali altri campioni pre-seek restano soppressi

        _lastKnownPosition = position;
        PositionChanged?.Invoke(this, position);

        if (_time.GetUtcNow() - _lastProgressSaveAt > ProgressSaveInterval)
            _ = SaveProgressAsync();
    }

    private async Task SeekToResumeAsync(TimeSpan target, int token)
    {
        IPodcastMediaPlayer? player = _player;
        if (player is null || token != _playToken)
            return;

        try
        {
            await player.SeekAsync(target);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resume episode from saved position");
        }
        finally
        {
            if (token == _playToken)
            {
                _resumeSeekInFlight = false;
                await _dispatcher.InvokeAsync(() => player.Volume = 1);
            }
        }
    }

    private async void OnMediaEnded(object? sender, EventArgs e)
    {
        await SaveProgressAsync(completed: true);
        CurrentEpisode = null;
        IsPlaying = false;
        PlaybackStateChanged?.Invoke(this, false);
        EpisodeCompleted?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveProgressAsync(bool completed = false)
    {
        AzuraStation? station = _station;
        AzuraCastPodcastEpisode? episode = CurrentEpisode;
        IPodcastMediaPlayer? player = _player;
        if (station is null || episode is null || player is null)
            return;

        _lastProgressSaveAt = _time.GetUtcNow();

        TimeSpan position = player.Position;
        if (position <= TimeSpan.Zero && _lastKnownPosition > TimeSpan.Zero)
            position = _lastKnownPosition;

        try
        {
            await _progressRepository.SaveAsync(station.StationId, station.Id, episode.PodcastId, episode.Id,
                (int)position.TotalSeconds, completed);
            ProgressSaved?.Invoke(this, new PodcastProgressSavedEventArgs(station.StationId, episode.Id, (int)position.TotalSeconds, completed));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save podcast episode progress");
        }
    }
}
