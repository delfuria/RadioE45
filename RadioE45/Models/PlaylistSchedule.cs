using System.Text.Json.Serialization;

namespace RadioE45.Models;

public class PlaylistSchedule
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("start_timestamp")]
    public long StartTimestamp { get; set; }

    [JsonPropertyName("start")]
    public DateTimeOffset Start { get; set; }

    [JsonPropertyName("end_timestamp")]
    public long EndTimestamp { get; set; }

    [JsonPropertyName("end")]
    public DateTimeOffset End { get; set; }

    [JsonPropertyName("is_now")]
    public bool IsNow { get; set; }

    /// <summary>Voce di tipo streamer (DJ live) invece di playlist AutoDJ.</summary>
    [JsonIgnore]
    public bool IsStreamer => string.Equals(Type, "streamer", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Realmente in onda adesso. Playlist: in orario e nessun live collegato.
    /// Streamer: in orario e collegato. Valorizzato dal ViewModel (IsNow dice solo "in orario").
    /// </summary>
    [JsonIgnore]
    public bool IsOnAir { get; set; }

    [JsonIgnore]
    public bool ShowLiveBadge => IsStreamer && IsOnAir;

    [JsonIgnore]
    public bool ShowPlaylistOnAirBadge => !IsStreamer && IsOnAir;

    /// <summary>Streamer in orario ma non ancora collegato.</summary>
    [JsonIgnore]
    public bool ShowInScheduleBadge => IsStreamer && IsNow && !IsOnAir;

    /// <summary>Copia con fascia oraria diversa (usata per ritagliare le playlist attorno agli streamer).</summary>
    public PlaylistSchedule WithRange(DateTimeOffset start, DateTimeOffset end, bool isNow) => new()
    {
        Id = Id,
        Type = Type,
        Name = Name,
        Title = Title,
        Description = Description,
        Start = start,
        StartTimestamp = start.ToUnixTimeSeconds(),
        End = end,
        EndTimestamp = end.ToUnixTimeSeconds(),
        IsNow = isNow,
    };
}
