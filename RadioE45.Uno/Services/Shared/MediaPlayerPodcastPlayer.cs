using RadioE45.Services.Audio;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace RadioE45.Services.Shared;

/// <summary>
/// <see cref="IPodcastMediaPlayer"/> on the Uno <see cref="MediaPlayer"/> without a visual element.
/// Unlike live radio, pause is a real pause and seeking works (episodes are files).
/// - A new MediaPlayer is created for every episode opened: on macOS, reopening a file on a
///   player whose source was cleared stops in Paused right after Playing and never recovers (S7b).
///   Released players are stopped but NOT disposed and are kept referenced: on macOS (Uno 6.7)
///   the native player keeps sending position updates after Dispose and
///   MacOSMediaPlayerExtension.OnPeriodicPositionUpdate crashes the app with a
///   NullReferenceException. One small object per episode opened in a session.
///   On the web the opposite holds: Uno's browser MediaPlayer instances interfere (a released one
///   pauses the new one: "play() request was interrupted by a call to pause()"), and reusing one
///   player works (as the live radio does), so the browser keeps a single player, created up
///   front: its HTML element is created asynchronously, and a play() issued right after creating
///   the player finds no element and is silently dropped.
/// - MediaPlayer has no position event: the position is sampled every 500 ms while playing
///   (MAUI's MediaElement raises PositionChanged at a similar rate).
/// - States are debounced like the live player (<see cref="StreamStateDebouncer"/>).
/// </summary>
public sealed class MediaPlayerPodcastPlayer : IPodcastMediaPlayer, IDisposable
{
    private static readonly TimeSpan StateDebounce = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan PositionInterval = TimeSpan.FromMilliseconds(500);

    private readonly StreamStateDebouncer _states;
    private readonly Timer _positionTimer;
    private readonly ILogger _logger;
    private readonly List<MediaPlayer> _released = [];
    private MediaPlayer? _player;
    private double _volume = 1;
    private volatile bool _rawPlaying;
    // After MediaEnded, libVLC on Linux (Uno 6.7) reloads the source as a bogus local path
    // ("file:///<cwd>/https:\\host\…") and raises MediaFailed: ignored until the next Open.
    private volatile bool _ended;

    public MediaPlayerPodcastPlayer(ILogger logger)
    {
        _logger = logger;
        _states = new StreamStateDebouncer(StateDebounce, TimeProvider.System,
            (previous, current) => StateChanged?.Invoke(this, new StreamPlayerStateChangedEventArgs(previous, current)));
        _positionTimer = new Timer(_ => SamplePosition(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        if (OperatingSystem.IsBrowser())
            _player = CreatePlayer();
    }

    public StreamPlayerState State => _states.ReportedState;
    public TimeSpan Position => _player?.PlaybackSession.Position ?? TimeSpan.Zero;
    public TimeSpan Duration => _player?.PlaybackSession.NaturalDuration ?? TimeSpan.Zero;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = value;
            if (_player is not null)
                _player.Volume = value;
        }
    }

    public event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;
    public event EventHandler<string?>? Failed;
    public event EventHandler? Ended;

    public void Open(string url)
    {
        _ended = false;
        _states.Push(StreamPlayerState.Opening);

        MediaPlayer player;
        if (OperatingSystem.IsBrowser() && _player is { } current)
        {
            player = current;
        }
        else
        {
            ReleasePlayer();
            player = _player = CreatePlayer();
        }

        player.Source = MediaSource.CreateFromUri(new Uri(url));
        player.Play();
    }

    private MediaPlayer CreatePlayer()
    {
        var player = new MediaPlayer { AutoPlay = false, Volume = _volume };
        player.MediaFailed += OnMediaFailed;
        player.MediaEnded += OnMediaEnded;
        player.PlaybackSession.PlaybackStateChanged += OnPlaybackStateChanged;
        return player;
    }

    public void Play() => _player?.Play();

    public void Pause() => _player?.Pause();

    public void Close()
    {
        if (OperatingSystem.IsBrowser() && _player is { } player)
        {
            SetSampling(false);
            player.Pause();
            player.Source = null;
        }
        else
        {
            ReleasePlayer();
        }

        _states.ReportNow(StreamPlayerState.Stopped);
    }

    // Right after opening, the backend may report Playing before it can really seek (AVPlayer on
    // macOS, spike S1) and silently ignore the new position: set it, check, and retry for up to
    // ~2 s. Later seeks (user dragging the slider) succeed on the first try.
    public async Task SeekAsync(TimeSpan position)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            if (_player is not { } player)
                return;

            player.PlaybackSession.Position = position;
            await Task.Delay(200);
            var now = player.PlaybackSession.Position;
            if ((now - position).Duration() < TimeSpan.FromSeconds(1.5) || now > position)
                return;
        }

        _logger.LogWarning("Podcast seek to {Position} not applied by the player", position);
    }

    public void Dispose()
    {
        ReleasePlayer();
        _positionTimer.Dispose();
        _states.Dispose();
    }

    private void ReleasePlayer()
    {
        SetSampling(false);
        if (_player is not { } player)
            return;

        _player = null;
        player.MediaFailed -= OnMediaFailed;
        player.MediaEnded -= OnMediaEnded;
        player.PlaybackSession.PlaybackStateChanged -= OnPlaybackStateChanged;
        player.Pause();
        player.Source = null;
        _released.Add(player); // see the class remarks: never Dispose
    }

    private void OnPlaybackStateChanged(MediaPlaybackSession session, object args)
    {
        if (_player?.PlaybackSession != session || _player.Source is null)
            return; // late event of a released or closed player

        var state = session.PlaybackState switch
        {
            MediaPlaybackState.Opening => StreamPlayerState.Opening,
            MediaPlaybackState.Buffering => StreamPlayerState.Buffering,
            MediaPlaybackState.Playing => StreamPlayerState.Playing,
            MediaPlaybackState.Paused => StreamPlayerState.Paused,
            _ => StreamPlayerState.None,
        };
        SetSampling(state == StreamPlayerState.Playing);
        _states.Push(state);
    }

    private void SetSampling(bool playing)
    {
        if (_rawPlaying == playing)
            return;
        _rawPlaying = playing;
        _positionTimer.Change(playing ? PositionInterval : Timeout.InfiniteTimeSpan, PositionInterval);
    }

    private void SamplePosition()
    {
        if (_rawPlaying && _player is { } player)
            PositionChanged?.Invoke(this, player.PlaybackSession.Position);
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        if (sender != _player || sender.Source is null || _ended)
            return; // released player, the web's empty error when Close removes the source, or the post-end reload

        _logger.LogWarning("Podcast MediaFailed {Error}: {Message}", args.Error, args.ErrorMessage);
        SetSampling(false);
        _states.ReportNow(StreamPlayerState.Failed);
        Failed?.Invoke(this, args.ErrorMessage);
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        if (sender != _player)
            return;

        _ended = true;
        SetSampling(false);
        Ended?.Invoke(this, EventArgs.Empty);
    }
}
