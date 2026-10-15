namespace RadioE45.Services.Audio;

/// <summary>Progress of an episode as just saved by <see cref="IPodcastPlayerService"/>.</summary>
public sealed class PodcastProgressSavedEventArgs(int stationId, string episodeId, int positionSeconds, bool isCompleted) : EventArgs
{
    public int StationId { get; } = stationId;
    public string EpisodeId { get; } = episodeId;
    public int PositionSeconds { get; } = positionSeconds;
    public bool IsCompleted { get; } = isCompleted;
}
