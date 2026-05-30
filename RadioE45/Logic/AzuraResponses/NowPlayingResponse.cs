using System.Text.Json.Serialization;

namespace RadioE45.Logic.AzuraResponses;

public class NowPlayingResponse
{
    [JsonPropertyName("station")]
    public Station Station { get; set; } = new();

    [JsonPropertyName("listeners")]
    public Listeners Listeners { get; set; } = new();

    [JsonPropertyName("live")]
    public Live Live { get; set; } = new();

    [JsonPropertyName("now_playing")]
    public NowPlayingEntry NowPlaying { get; set; } = new();

    [JsonPropertyName("playing_next")]
    public PlayingNext? PlayingNext { get; set; }

    [JsonPropertyName("song_history")]
    public List<SongHistoryEntry> SongHistory { get; set; } = [];

    [JsonPropertyName("is_online")]
    public bool IsOnline { get; set; }

    [JsonPropertyName("cache")]
    public object? Cache { get; set; }
}
