namespace RadioE45.Services.Audio;

public sealed class NullPlatformNowPlayingService : IPlatformNowPlayingService
{
    public void UpdateMetadata(string artist, string title, string? artworkUrl, int? elapsedSeconds, int? durationSeconds, bool isPlaying)
    {
    }

    public void UpdatePlaybackState(bool isPlaying)
    {
    }

    public void Clear()
    {
    }
}
