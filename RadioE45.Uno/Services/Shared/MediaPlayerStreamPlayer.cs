using RadioE45.Services.Audio;
using RadioE45.Services.Platform;
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
/// - PlaybackSession.Position is not used (unreliable for live streams);
/// - libVLC raises no event when the network drops (S1, confirmed in S7b with a 3-minute cut):
///   when <see cref="INetworkMonitor"/> reports the loss while a source is open, the player
///   reports Buffering, so the UI does not stay on "playing" during the stall and the watchdog
///   of <see cref="LiveStreamAudioService"/> (stuck buffering > 12 s) takes over.
/// - on the web, close only pauses and keeps the source (see <see cref="Close"/>).
/// System metadata goes through IPlatformNowPlayingService (MPRIS, Media Session): SetMetadata
/// is a no-op.
/// </summary>
public sealed class MediaPlayerStreamPlayer : IStreamPlayer, IDisposable
{
    private static readonly TimeSpan StateDebounce = TimeSpan.FromMilliseconds(400);

    private readonly MediaPlayer _player;
    private readonly StreamStateDebouncer _states;
    private readonly ILogger _logger;
    private bool _closed = true;

    public MediaPlayerStreamPlayer(INetworkMonitor networkMonitor, ILogger logger)
    {
        _logger = logger;
        networkMonitor.ConnectivityChanged += OnConnectivityChanged;
        _player = new MediaPlayer { AutoPlay = false };
        _states = new StreamStateDebouncer(StateDebounce, TimeProvider.System,
            (previous, current) => StateChanged?.Invoke(this, new StreamPlayerStateChangedEventArgs(previous, current)));

        _player.MediaFailed += OnMediaFailed;
        _player.MediaEnded += OnMediaEnded;
        _player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    public bool HasSource => !_closed && _player.Source is not null;

    public event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;
    public event EventHandler<string?>? Failed;
    public event EventHandler? Ended;

    public void Open(string url)
    {
        _states.Push(StreamPlayerState.Opening);
        _closed = false;
        _player.Source = MediaSource.CreateFromUri(new Uri(url));
        _player.Play();
    }

    public void Close()
    {
        // Already closed: touching the player again is not harmless on the web, where the
        // deferred stop (HTMLMediaElement.load) aborts a play() just started by the podcast player.
        if (_closed || _player.Source is null)
            return;

        _closed = true;
        _player.Pause();
        // Web: Chrome removes the media controls (Media Session) as soon as the audio element
        // loses its source, so a pause from the controls would close them. The paused source
        // stays (the browser stops reading it; the server drops the idle listener) and the next
        // Open replaces it with a fresh connection to the live stream.
        if (!OperatingSystem.IsBrowser())
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

    private void OnConnectivityChanged(object? sender, bool hasInternet)
    {
        if (!hasInternet && HasSource)
        {
            _logger.LogInformation("Network lost while streaming: reporting Buffering");
            _states.ReportNow(StreamPlayerState.Buffering);
        }
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession session, object args)
    {
        // A closed player keeps reporting Paused/None: only the explicit Stopped from Close counts.
        if (_closed || _player.Source is null)
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
        // with no message: that is our own close, not a failure. Same for a parked source.
        if (_closed || _player.Source is null)
            return;

        // The web reports a play() without a user gesture as NotAllowedError (§5).
        _logger.LogWarning("MediaFailed {Error}: {Message}", args.Error, args.ErrorMessage);
        _states.ReportNow(StreamPlayerState.Failed);
        Failed?.Invoke(this, args.ErrorMessage);
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        if (!_closed)
            Ended?.Invoke(this, EventArgs.Empty);
    }
}
