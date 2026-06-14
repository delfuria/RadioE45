using Android.Graphics;

namespace RadioE45.Services.Audio;

internal static class AndroidNowPlayingStateStore
{
    private static readonly object Sync = new();
    private static PlatformNowPlayingSnapshot _snapshot = new(string.Empty, string.Empty, null, null, null, false);
    private static Bitmap? _artwork;

    public static event Action<PlatformNowPlayingSnapshot>? SnapshotChanged;

    public static void UpdateSnapshot(PlatformNowPlayingSnapshot snapshot)
    {
        lock (Sync)
        {
            _snapshot = snapshot;
        }
        SnapshotChanged?.Invoke(snapshot);
    }

    public static void UpdateArtwork(Bitmap? artwork)
    {
        lock (Sync)
        {
            _artwork = artwork;
        }
    }

    public static (PlatformNowPlayingSnapshot Snapshot, Bitmap? Artwork) GetState()
    {
        lock (Sync)
        {
            return (_snapshot, _artwork);
        }
    }

    public static void Clear()
    {
        var cleared = new PlatformNowPlayingSnapshot(string.Empty, string.Empty, null, null, null, false);
        lock (Sync)
        {
            _snapshot = cleared;
            _artwork = null;
        }
        SnapshotChanged?.Invoke(cleared);
    }
}
