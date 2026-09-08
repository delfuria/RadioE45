using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;

namespace RadioE45.Services.Audio;

// Player dedicato agli episodi podcast: MediaElement propria (non quella della radio live), foreground
// only per la v1 — non integra Media3/RadioPlaybackService (Android) né il Now Playing di sistema
// (iOS/Windows), che restano di pertinenza esclusiva di IAudioService. Vedi PODCAST-FEATURE.md.
public class PodcastPlayerService : IPodcastPlayerService
{
    private readonly IAudioService _audioService;
    private readonly IPodcastProgressRepository _progressRepository;
    private readonly ILogger<PodcastPlayerService> _logger;
    private MediaElement? _mediaElement;
    private AzuraStation? _station;
    private TimeSpan? _pendingResumePosition;
    // Vero tra "seek di ripresa avviato" e "seek completato": i campioni di posizione che arrivano
    // in questa finestra sono pre-seek (vicini a 0) e vanno soppressi, altrimenti la UI mostra un
    // flash a 0 prima che il campione post-seek corretto arrivi.
    private bool _resumeSeekInFlight;
    // Incrementato a ogni PlayAsync: un evento StateChanged "Playing" tardivo riferito a un
    // episodio ormai sostituito (utente ha cliccato un altro episodio nel frattempo) non deve
    // consumare/eseguire il seek con il target del nuovo episodio già impostato in _pendingResumePosition.
    private int _playToken;
    private int _pendingResumeToken;
    private DateTime _lastProgressSaveAt = DateTime.MinValue;
    private static readonly TimeSpan ProgressSaveInterval = TimeSpan.FromSeconds(5);

    public bool IsPlaying { get; private set; }
    public AzuraCastPodcastEpisode? CurrentEpisode { get; private set; }
    public TimeSpan Position => _mediaElement?.Position ?? TimeSpan.Zero;
    public TimeSpan Duration => _mediaElement?.Duration ?? TimeSpan.Zero;

    public event EventHandler<bool>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler? EpisodeCompleted;
    public event EventHandler<string?>? PlaybackFailed;

    private string? _lastFailureMessage;

    public PodcastPlayerService(IAudioService audioService, IPodcastProgressRepository progressRepository, ILogger<PodcastPlayerService> logger)
    {
        _audioService = audioService;
        _progressRepository = progressRepository;
        _logger = logger;
    }

    public void Initialize(MediaElement mediaElement)
    {
        if (_mediaElement is not null)
        {
            _mediaElement.StateChanged -= OnStateChanged;
            _mediaElement.MediaEnded -= OnMediaEnded;
            _mediaElement.PositionChanged -= OnMediaPositionChanged;
            _mediaElement.MediaFailed -= OnMediaFailed;
        }

        _mediaElement = mediaElement;
        _mediaElement.ShouldAutoPlay = false;
        _mediaElement.ShouldLoopPlayback = false;
        _mediaElement.ShouldShowPlaybackControls = false;

        _mediaElement.StateChanged += OnStateChanged;
        _mediaElement.MediaEnded += OnMediaEnded;
        _mediaElement.PositionChanged += OnMediaPositionChanged;
        _mediaElement.MediaFailed += OnMediaFailed;
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
        if (_mediaElement is null)
            return;

        int token = ++_playToken;

        // Un solo player attivo alla volta: avviare un episodio ferma la radio live.
        await _audioService.StopAsync();

        _station = station;
        CurrentEpisode = episode;
        _lastProgressSaveAt = DateTime.MinValue;
        _pendingResumePosition = startPositionSeconds > 0 ? TimeSpan.FromSeconds(startPositionSeconds) : null;
        _pendingResumeToken = token;
        _resumeSeekInFlight = false;

        _lastFailureMessage = null;
        await OpenAndPlayWithRetryAsync(episode.MediaUrl);
    }

    // Il player nativo dietro MediaElement (AVPlayer su iOS/macCatalyst, incluso) può non essere
    // ancora pronto al primissimo Source impostato subito dopo Initialize — la richiesta viene
    // scartata in silenzio (nessuna eccezione, nessun evento). La radio live copre la stessa race
    // con un watchdog continuo (vedi AudioService); qui basta un breve retry limitato all'avvio.
    // Un errore reale (es. 403 dal server) arriva invece via MediaFailed — in quel caso niente
    // retry, l'errore viene propagato subito tramite PlaybackFailed.
    //
    // Il seek di ripresa NON va fatto qui né su MediaOpened/StateChanged=Playing: in entrambi i
    // casi il player nativo non è ancora davvero "seekable" (metadata caricati ma decodifica non
    // partita) e il SeekTo fallisce in silenzio (Task completa, posizione resta 0). Il seek va
    // fatto sul primo campione REALE di OnMediaPositionChanged — a quel punto il player sta
    // decodificando per davvero e il seek ha effetto. Vedi OnMediaPositionChanged.
    private async Task OpenAndPlayWithRetryAsync(string url)
    {
        MediaElement? mediaElement = _mediaElement;
        if (mediaElement is null)
            return;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Se c'è un resume pendente, Play() parte comunque subito (il seek ha effetto solo
                // dopo il primo campione reale di posizione, vedi commento sopra) — silenziare
                // l'audio evita che l'utente senta l'inizio dell'episodio dal secondo 0 prima che
                // lo scatti verso la posizione salvata. Ripristinato in SeekToResumeAsync.
                mediaElement.Volume = _pendingResumePosition is not null ? 0 : 1;
                mediaElement.ShouldAutoPlay = true;
                mediaElement.Source = MediaSource.FromUri(url);
                mediaElement.Play();
            });

            await Task.Delay(1500);

            if (_lastFailureMessage is not null)
            {
                _logger.LogWarning("Podcast playback failed: {Message} — {Url}", _lastFailureMessage, url);
                PlaybackFailed?.Invoke(this, _lastFailureMessage);
                // return;
            }

            if (mediaElement.CurrentState is MediaElementState.Playing or MediaElementState.Buffering
                or MediaElementState.Opening or MediaElementState.Paused)
                return;
        }

        _logger.LogWarning("Podcast playback failed to start after retries: {Url}", url);
        PlaybackFailed?.Invoke(this, null);
    }

    private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    {
        _lastFailureMessage = e.ErrorMessage;
    }

    public async Task PauseAsync()
    {
        MediaElement? mediaElement = _mediaElement;
        if (mediaElement is null)
            return;

        await MainThread.InvokeOnMainThreadAsync(mediaElement.Pause);
        await SaveProgressAsync();
    }

    public async Task ResumeAsync()
    {
        MediaElement? mediaElement = _mediaElement;
        if (mediaElement is null)
            return;

        await MainThread.InvokeOnMainThreadAsync(mediaElement.Play);
    }

    public async Task StopAsync()
    {
        MediaElement? mediaElement = _mediaElement;
        if (mediaElement is null)
            return;

        await SaveProgressAsync();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            mediaElement.Stop();
            mediaElement.Source = null;
        });

        CurrentEpisode = null;
        IsPlaying = false;
        PlaybackStateChanged?.Invoke(this, false);
    }

    public async Task SeekAsync(TimeSpan position)
    {
        if (_mediaElement is null)
            return;

        await _mediaElement.SeekTo(position, CancellationToken.None);
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        IsPlaying = e.NewState == MediaElementState.Playing;
        PlaybackStateChanged?.Invoke(this, IsPlaying);
    }

    private void OnMediaPositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        // Primo campione di posizione reale dopo Play(): qui il player sta davvero decodificando,
        // non più solo "aperto" — il SeekTo ha effetto reale solo a questo punto (vedi commento
        // su OpenAndPlayWithRetryAsync). Token-guard: ignora se nel frattempo è stato selezionato
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

        PositionChanged?.Invoke(this, e.Position);

        if (DateTime.UtcNow - _lastProgressSaveAt > ProgressSaveInterval)
            _ = SaveProgressAsync();
    }

    private async Task SeekToResumeAsync(TimeSpan target, int token)
    {
        MediaElement? mediaElement = _mediaElement;
        if (mediaElement is null || token != _playToken)
            return;

        try
        {
            await mediaElement.SeekTo(target, CancellationToken.None);
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
                await MainThread.InvokeOnMainThreadAsync(() => mediaElement.Volume = 1);
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
        MediaElement? mediaElement = _mediaElement;
        if (station is null || episode is null || mediaElement is null)
            return;

        _lastProgressSaveAt = DateTime.UtcNow;

        try
        {
            await _progressRepository.SaveAsync(station.StationId, station.Id, episode.PodcastId, episode.Id,
                (int)mediaElement.Position.TotalSeconds, completed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save podcast episode progress");
        }
    }
}
