using SQLite;

namespace RadioE45.Models;

[Table("PodcastEpisodeProgress")]
public class PodcastEpisodeProgress
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int StationId { get; set; }
    public string PodcastId { get; set; } = string.Empty;
    public string EpisodeId { get; set; } = string.Empty;
    public int PositionSeconds { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime LastPlayedAt { get; set; }
}
