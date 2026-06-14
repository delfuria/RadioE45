namespace RadioE45.Services.Audio;

public sealed class PlatformNowPlayingStateTracker
{
    private readonly object _sync = new();
    private string _artist = string.Empty;
    private string _title = string.Empty;
    private string? _artworkUrl;
    private int? _elapsedSeconds;
    private int? _durationSeconds;
    private bool _isPlaying;
    private int _artworkVersion;

    public PlatformNowPlayingUpdateResult UpdateMetadata(
        string artist,
        string title,
        string? artworkUrl,
        int? elapsedSeconds,
        int? durationSeconds,
        bool isPlaying)
    {
        string? normalizedArtworkUrl = string.IsNullOrWhiteSpace(artworkUrl) ? null : artworkUrl;
        string? artworkUrlToLoad = null;
        int artworkVersion = 0;

        lock (_sync)
        {
            _artist = artist;
            _title = title;
            _elapsedSeconds = elapsedSeconds;
            _durationSeconds = durationSeconds;
            _isPlaying = isPlaying;

            if (!string.Equals(_artworkUrl, normalizedArtworkUrl, StringComparison.Ordinal))
            {
                _artworkUrl = normalizedArtworkUrl;
                _artworkVersion++;
                artworkVersion = _artworkVersion;
                artworkUrlToLoad = normalizedArtworkUrl;
            }

            return new PlatformNowPlayingUpdateResult(CreateSnapshot(), artworkUrlToLoad, artworkVersion);
        }
    }

    public PlatformNowPlayingSnapshot UpdatePlaybackState(bool isPlaying)
    {
        lock (_sync)
        {
            _isPlaying = isPlaying;
            return CreateSnapshot();
        }
    }

    public PlatformNowPlayingSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return CreateSnapshot();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _artist = string.Empty;
            _title = string.Empty;
            _artworkUrl = null;
            _elapsedSeconds = null;
            _durationSeconds = null;
            _isPlaying = false;
            _artworkVersion++;
        }
    }

    public bool IsArtworkCurrent(string artworkUrl, int artworkVersion)
    {
        lock (_sync)
        {
            return _artworkVersion == artworkVersion &&
                   string.Equals(_artworkUrl, artworkUrl, StringComparison.Ordinal);
        }
    }

    private PlatformNowPlayingSnapshot CreateSnapshot() =>
        new(_artist, _title, _artworkUrl, _elapsedSeconds, _durationSeconds, _isPlaying);
}
