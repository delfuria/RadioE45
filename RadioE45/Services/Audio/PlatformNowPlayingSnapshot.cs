namespace RadioE45.Services.Audio;

public readonly record struct PlatformNowPlayingSnapshot(
    string Artist,
    string Title,
    string? ArtworkUrl,
    int? ElapsedSeconds,
    int? DurationSeconds,
    bool IsPlaying);
