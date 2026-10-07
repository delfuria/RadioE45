using SQLite;

namespace RadioE45.Models;

[Table("PodcastEpisodeProgress")]
public class PodcastEpisodeProgress
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int StationId { get; set; }
    // FK verso RadioStations.Id (PK locale) — StationId sopra è invece l'id numerico AzuraCast,
    // non univoco tra stazioni diverse (vedi seed: stazioni distinte possono condividerlo).
    public int RadioStationId { get; set; }
    public string PodcastId { get; set; } = string.Empty;
    public string EpisodeId { get; set; } = string.Empty;
    public int PositionSeconds { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime LastPlayedAt { get; set; }
}
