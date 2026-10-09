namespace RadioE45.Services.Audio;

/// <summary>
/// The few player operations <see cref="LiveStreamAudioService"/> needs, implemented per head
/// (MAUI: CommunityToolkit MediaElement, Uno: Windows.Media.Playback.MediaPlayer). Members are
/// called on the UI thread; events may be raised on any thread.
/// </summary>
public interface IStreamPlayer
{
    /// <summary>Whether a source is currently set (null after <see cref="Close"/>).</summary>
    bool HasSource { get; }

    /// <summary>Sets the stream URL and starts playing it as soon as it opens.</summary>
    void Open(string url);

    /// <summary>Stops playback and releases the source (and its network connection).</summary>
    void Close();

    void SetVolume(double volume);

    /// <summary>Metadata shown by the player's own system integration, if it has one.</summary>
    void SetMetadata(string artist, string title, string? artworkUrl);

    void ClearMetadata();

    event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;

    /// <summary>Raised when the media fails to open or play; the argument is the error message.</summary>
    event EventHandler<string?>? Failed;

    /// <summary>Raised when the stream ends (for a live stream: the server closed it).</summary>
    event EventHandler? Ended;
}

public enum StreamPlayerState
{
    None,
    Opening,
    Buffering,
    Playing,
    Paused,
    Stopped,
    Failed,
}

public sealed class StreamPlayerStateChangedEventArgs(StreamPlayerState previousState, StreamPlayerState newState) : EventArgs
{
    public StreamPlayerState PreviousState { get; } = previousState;

    public StreamPlayerState NewState { get; } = newState;
}
