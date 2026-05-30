using System.Text.Json.Serialization;

namespace RadioE45.Logic.AzuraResponses;

public class NowPlayingEntry : SongHistoryEntry
{
    [JsonPropertyName("elapsed")]
    public int Elapsed { get; set; }

    [JsonPropertyName("remaining")]
    public int Remaining { get; set; }
}
