namespace RadioE45.Models;

public class NextPlayingInfo
{
    public string Artist { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ArtworkUrl { get; set; }
    public bool IsJingle { get; set; }
}
