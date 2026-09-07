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
    private DateTime _lastProgressSaveAt = DateTime.MinValue;
    private static readonly TimeSpan ProgressSaveInterval = TimeSpan.FromSeconds(10);

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

    public async Task PlayAsync(AzuraStation station, AzuraCastPodcastEpisode episode)
    {
        if (_mediaElement is null)
            return;

        // Un solo player attivo alla volta: avviare un episodio ferma la radio live.
        await _audioService.StopAsync();

        _station = station;
        CurrentEpisode = episode;
        _lastProgressSaveAt = DateTime.MinValue;

        PodcastEpisodeProgress? saved = await _progressRepository.GetAsync(station.StationId, episode.Id);
        _pendingResumePosition = saved is { IsCompleted: false } && saved.PositionSeconds > 3
            ? TimeSpan.FromSeconds(saved.PositionSeconds)
            : null;

        _lastFailureMessage = null;
        await OpenAndPlayWithRetryAsync(episode.MediaUrl);
    }

    // Il player nativo dietro MediaElement (AVPlayer su iOS/macCatalyst, incluso) può non essere
    // ancora pronto al primissimo Source impostato subito dopo Initialize — la richiesta viene
    // scartata in silenzio (nessuna eccezione, nessun evento). La radio live copre la stessa race
    // con un watchdog continuo (vedi AudioService); qui basta un breve retry limitato all'avvio.
    // Un errore reale (es. 403 dal server) arriva invece via MediaFailed — in quel caso niente
    // retry, l'errore viene propagato subito tramite PlaybackFailed.
    private async Task OpenAndPlayWithRetryAsync(string url)
    {
        MediaElement? mediaElement = _mediaElement;
        if (mediaElement is null)
            return;

        for (int attempt = 0; attempt < 5; attempt++)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
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

    private async void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        IsPlaying = e.NewState == MediaElementState.Playing;
        PlaybackStateChanged?.Invoke(this, IsPlaying);

        if (e.NewState == MediaElementState.Playing && _pendingResumePosition is { } resume && _mediaElement is not null)
        {
            _pendingResumePosition = null;
            try
            {
                await _mediaElement.SeekTo(resume, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resume episode from saved position");
            }
        }
    }

    private void OnMediaPositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        PositionChanged?.Invoke(this, e.Position);

        if (DateTime.UtcNow - _lastProgressSaveAt > ProgressSaveInterval)
            _ = SaveProgressAsync();
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
            await _progressRepository.SaveAsync(station.StationId, episode.PodcastId, episode.Id,
                (int)mediaElement.Position.TotalSeconds, completed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save podcast episode progress");
        }
    }
}
