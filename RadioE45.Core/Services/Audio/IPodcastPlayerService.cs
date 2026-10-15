using RadioE45.Models;

namespace RadioE45.Services.Audio;

public interface IPodcastPlayerService
{
    bool IsPlaying { get; }
    AzuraCastPodcastEpisode? CurrentEpisode { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }

    event EventHandler<bool>? PlaybackStateChanged;
    event EventHandler<TimeSpan>? PositionChanged;
    event EventHandler? EpisodeCompleted;
    event EventHandler<string?>? PlaybackFailed;

    /// <summary>
    /// The episode (by id) was stopped and unloaded by <see cref="StopAsync"/>, whoever called it —
    /// including the live radio taking over (OnAirViewModel), not only the episodes page.
    /// </summary>
    event EventHandler<string>? EpisodeStopped;

    /// <summary>Raised after every progress save (periodic, pause, stop, end of episode).</summary>
    event EventHandler<PodcastProgressSavedEventArgs>? ProgressSaved;

    /// <summary>
    /// Legge il progresso salvato per l'episodio senza avviare la riproduzione — da usare per
    /// sincronizzare la UI (barra/testo) con lo stesso valore poi passato a PlayAsync.
    /// </summary>
    Task<int> GetResumePositionSecondsAsync(AzuraStation station, AzuraCastPodcastEpisode episode);

    Task PlayAsync(AzuraStation station, AzuraCastPodcastEpisode episode, int startPositionSeconds);
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    Task SeekAsync(TimeSpan position);
}
