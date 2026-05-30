using System.Text.Json.Serialization;

namespace RadioE45.Logic.AzuraResponses;

public class PlayingNext
{
    [JsonPropertyName("cued_at")]
    public long CuedAt { get; set; }

    [JsonPropertyName("played_at")]
    public long PlayedAt { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("playlist")]
    public string Playlist { get; set; } = string.Empty;

    [JsonPropertyName("is_request")]
    public bool IsRequest { get; set; }

    [JsonPropertyName("song")]
    public Song Song { get; set; } = new();
}
