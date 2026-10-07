namespace RadioE45.Services.Audio;

public interface IPlatformNowPlayingService
{
    void UpdateMetadata(string artist, string title, string? artworkUrl, int? elapsedSeconds, int? durationSeconds, bool isPlaying);
    void UpdatePlaybackState(bool isPlaying);
    void Clear();
}
