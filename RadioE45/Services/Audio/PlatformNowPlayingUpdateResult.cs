namespace RadioE45.Services.Audio;

public readonly record struct PlatformNowPlayingUpdateResult(
    PlatformNowPlayingSnapshot Snapshot,
    string? ArtworkUrlToLoad,
    int ArtworkVersion);
