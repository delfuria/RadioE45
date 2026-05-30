using System.Text.Json.Serialization;

namespace RadioE45.Logic.AzuraResponses;

public class SongHistoryEntry
{
    [JsonPropertyName("sh_id")]
    public int ShId { get; set; }

    [JsonPropertyName("played_at")]
    public long PlayedAt { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("playlist")]
    public string Playlist { get; set; } = string.Empty;

    [JsonPropertyName("streamer")]
    public string Streamer { get; set; } = string.Empty;

    [JsonPropertyName("is_request")]
    public bool IsRequest { get; set; }

    [JsonPropertyName("song")]
    public Song Song { get; set; } = new();
}
