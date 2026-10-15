namespace RadioE45.Services.Audio;

/// <summary>
/// The player operations <see cref="PodcastPlaybackService"/> needs, implemented per head
/// (MAUI: CommunityToolkit MediaElement, Uno: Windows.Media.Playback.MediaPlayer). Unlike
/// <see cref="IStreamPlayer"/> (live radio) this is a seekable, pausable on-demand player.
/// Members are called on the UI thread; events may be raised on any thread.
/// </summary>
public interface IPodcastMediaPlayer
{
    StreamPlayerState State { get; }

    TimeSpan Position { get; }

    TimeSpan Duration { get; }

    /// <summary>0..1; the service mutes the player while it seeks to the resume position.</summary>
    double Volume { get; set; }

    /// <summary>Sets the episode URL and starts playing it as soon as it opens.</summary>
    void Open(string url);

    void Play();

    void Pause();

    /// <summary>Stops playback and releases the source.</summary>
    void Close();

    Task SeekAsync(TimeSpan position);

    event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;

    /// <summary>Playback position samples while the media is being decoded.</summary>
    event EventHandler<TimeSpan>? PositionChanged;

    /// <summary>Raised when the media fails to open or play; the argument is the error message.</summary>
    event EventHandler<string?>? Failed;

    event EventHandler? Ended;
}
