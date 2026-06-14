using System.Text.Json.Serialization;

namespace RadioE45.Models;

public class AzuraCastNowPlayingResponse
{
    [JsonPropertyName("station")]
    public StationInfo Station { get; set; } = new();

    [JsonPropertyName("now_playing")]
    public CurrentSong NowPlaying { get; set; } = new();

    [JsonPropertyName("playing_next")]
    public NextSong? PlayingNext { get; set; }

    [JsonPropertyName("listeners")]
    public ListenersInfo Listeners { get; set; } = new();

    [JsonPropertyName("live")]
    public LiveInfo Live { get; set; } = new();
}

public class CurrentSong
{
    [JsonPropertyName("song")]
    public SongInfo Song { get; set; } = new();

    [JsonPropertyName("played_at")]
    public long PlayedAt { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("elapsed")]
    public int Elapsed { get; set; }
}

public class SongInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("artist")]
    public string Artist { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("art")]
    public string? ArtUrl { get; set; }
}

public class NextSong
{
    [JsonPropertyName("song")]
    public SongInfo Song { get; set; } = new();

    [JsonPropertyName("played_at")]
    public long PlayedAt { get; set; }

    [JsonPropertyName("duration")]
    public double Duration { get; set; }
}

public class StationInfo
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("listen_url")]
    public string ListenUrl { get; set; } = string.Empty;
}

public class ListenersInfo
{
    [JsonPropertyName("current")]
    public int Current { get; set; }
}

public class LiveInfo
{
    [JsonPropertyName("is_live")]
    public bool IsLive { get; set; }

    [JsonPropertyName("streamer_name")]
    public string StreamerName { get; set; } = string.Empty;
}
