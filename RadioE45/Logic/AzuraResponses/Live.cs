using System.Text.Json.Serialization;

namespace RadioE45.Logic.AzuraResponses;

public class Live
{
    [JsonPropertyName("is_live")]
    public bool IsLive { get; set; }

    [JsonPropertyName("streamer_name")]
    public string StreamerName { get; set; } = string.Empty;

    [JsonPropertyName("broadcast_start")]
    public long? BroadcastStart { get; set; }

    [JsonPropertyName("art")]
    public string? Art { get; set; }
}
