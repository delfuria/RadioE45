using CommunityToolkit.Maui.Views;
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
    /// Called once from PodcastEpisodesPage to attach the MediaElement from the visual tree.
    /// </summary>
    void Initialize(MediaElement mediaElement);

    Task PlayAsync(AzuraStation station, AzuraCastPodcastEpisode episode);
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    Task SeekAsync(TimeSpan position);
}
