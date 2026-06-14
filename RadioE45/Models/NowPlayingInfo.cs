namespace RadioE45.Models;

public class NowPlayingInfo : IEquatable<NowPlayingInfo>, IComparable<NowPlayingInfo>
{
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ArtworkUrl { get; set; }
    public bool IsLive { get; set; }
    public string StreamerName { get; set; } = string.Empty;
    public int ListenerCount { get; set; }
    public int TrackDurationSeconds { get; set; }
    public int TrackElapsedSeconds { get; set; }
    public DateTime LastUpdated { get; set; }
    public bool IsJingle { get; set; }

    public NextPlayingInfo? Next { get; set; }

    public static NowPlayingInfo Empty => new()
    {
        Artist = "—",
        Title = "In attesa...",
        LastUpdated = DateTime.MinValue
    };

    // Equality is based on the fields that represent a track change.
    // Volatile fields (ListenerCount, LastUpdated) are excluded.
    public bool Equals(NowPlayingInfo? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        //return LastUpdated.Equals(other.LastUpdated);
        
        return Artist == other.Artist &&
               Title == other.Title &&
               ArtworkUrl == other.ArtworkUrl &&
               IsLive == other.IsLive &&
               StreamerName == other.StreamerName &&
               TrackElapsedSeconds == other.TrackElapsedSeconds &&
               TrackDurationSeconds == other.TrackDurationSeconds;
    }

    public override bool Equals(object? obj) => Equals(obj as NowPlayingInfo);

    public override int GetHashCode() =>
        HashCode.Combine(Artist, Title, ArtworkUrl, IsLive, StreamerName, TrackDurationSeconds);

    public int CompareTo(NowPlayingInfo? other)
    {
        if (other is null) return 1;
        return LastUpdated.CompareTo(other.LastUpdated);
    }
}
