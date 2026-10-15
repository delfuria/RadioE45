using RadioE45.Services.Audio;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace RadioE45.Services.Shared;

/// <summary>
/// <see cref="IStreamPlayer"/> on the Uno <see cref="MediaPlayer"/> without a visual element
/// (libVLC on Linux, the system player on macOS, HTML5 audio on the web). Rules from the S1/S2
/// spikes (docs/LINUX-UNO-ROADMAP.md §4.3):
/// - states are debounced (<see cref="StreamStateDebouncer"/>) because they flap right after
///   opening; Buffering is derived from the playback state (BufferingStarted/Ended are not
///   implemented in Uno);
/// - stop = Pause() + Source = null (there is no Stop()); a new source is needed to play again;
/// - PlaybackSession.Position is not used (unreliable for live streams).
/// System metadata (MPRIS, Media Session) comes in S9: SetMetadata is a no-op until then.
/// </summary>
public sealed class MediaPlayerStreamPlayer : IStreamPlayer, IDisposable
{
    private static readonly TimeSpan StateDebounce = TimeSpan.FromMilliseconds(400);

    private readonly MediaPlayer _player;
    private readonly StreamStateDebouncer _states;
    private readonly ILogger _logger;

    public MediaPlayerStreamPlayer(ILogger logger)
    {
        _logger = logger;
        _player = new MediaPlayer { AutoPlay = false };
        _states = new StreamStateDebouncer(StateDebounce, TimeProvider.System,
            (previous, current) => StateChanged?.Invoke(this, new StreamPlayerStateChangedEventArgs(previous, current)));

        _player.MediaFailed += OnMediaFailed;
        _player.MediaEnded += OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    public bool HasSource => _player.Source is not null;

    public event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;
    public event EventHandler<string?>? Failed;
    public event EventHandler? Ended;

    public void Open(string url)
    {
        _states.Push(StreamPlayerState.Opening);
        _player.Source = MediaSource.CreateFromUri(new Uri(url));
        _player.Play();
    }

    public void Close()
    {
        _player.Pause();
        _player.Source = null;
        _states.ReportNow(StreamPlayerState.Stopped);
    }

    public void SetVolume(double volume) => _player.Volume = volume;

    public void SetMetadata(string artist, string title, string? artworkUrl)
    {
    }

    public void ClearMetadata()
    {
    }

    public void Dispose()
    {
        _player.MediaFailed -= OnMediaFailed;
        _player.MediaEnded -= OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
        _states.Dispose();
        _player.Dispose();
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession session, object args)
    {
        // A closed player keeps reporting Paused/None: only the explicit Stopped from Close counts.
        if (_player.Source is null)
            return;

        _states.Push(session.PlaybackState switch
        {
            MediaPlaybackState.Opening => StreamPlayerState.Opening,
            MediaPlaybackState.Buffering => StreamPlayerState.Buffering,
            MediaPlaybackState.Playing => StreamPlayerState.Playing,
            MediaPlaybackState.Paused => StreamPlayerState.Paused,
            _ => StreamPlayerState.None,
        });
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        // On the web, removing the source (Close) makes the HTML5 audio element raise an error
        // with no message: that is our own close, not a failure.
        if (_player.Source is null)
            return;

        // The web reports a play() without a user gesture as NotAllowedError (§5).
        _logger.LogWarning("MediaFailed {Error}: {Message}", args.Error, args.ErrorMessage);
        _states.ReportNow(StreamPlayerState.Failed);
        Failed?.Invoke(this, args.ErrorMessage);
    }

    private void OnMediaEnded(MediaPlayer sender, object args) => Ended?.Invoke(this, EventArgs.Empty);
}
