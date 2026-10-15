namespace RadioE45.Services.Audio;

/// <summary>
/// Reports a player state only after it has been stable for <c>delay</c>. The Uno MediaPlayer
/// (libVLC on Linux, AVPlayer on macOS, HTML5 audio on the web) flaps right after opening
/// (Opening → Buffering → Opening → Playing → Paused → Buffering → Playing within a few ms, and on
/// macOS "Playing" arrives before real audio): forwarding every step would make the UI and
/// <see cref="LiveStreamAudioService"/>'s watchdog react to states that never really happened
/// (docs/LINUX-UNO-ROADMAP.md §4.3). Thread-safe; the callback runs on a timer thread or on the
/// caller of <see cref="ReportNow"/>.
/// </summary>
public sealed class StreamStateDebouncer : IDisposable
{
    private readonly TimeSpan _delay;
    private readonly Action<StreamPlayerState, StreamPlayerState> _report;
    private readonly ITimer _timer;
    private readonly object _gate = new();
    private StreamPlayerState _pending = StreamPlayerState.None;
    private StreamPlayerState _reported = StreamPlayerState.None;

    /// <param name="report">Called with (previous reported state, new reported state).</param>
    public StreamStateDebouncer(TimeSpan delay, TimeProvider timeProvider, Action<StreamPlayerState, StreamPlayerState> report)
    {
        _delay = delay;
        _report = report;
        _timer = timeProvider.CreateTimer(_ => OnStable(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public StreamPlayerState ReportedState
    {
        get { lock (_gate) return _reported; }
    }

    /// <summary>A raw state from the player: reported once nothing else arrives within the delay.</summary>
    public void Push(StreamPlayerState state)
    {
        lock (_gate)
        {
            _pending = state;
            // Back to the reported state before the delay: a glitch, nothing to report.
            _timer.Change(state == _reported ? Timeout.InfiniteTimeSpan : _delay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Reports a state immediately, dropping any pending one (e.g. Stopped on Close).</summary>
    public void ReportNow(StreamPlayerState state)
    {
        StreamPlayerState previous;
        lock (_gate)
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _pending = state;
            if (state == _reported)
                return;
            previous = _reported;
            _reported = state;
        }

        _report(previous, state);
    }

    public void Dispose() => _timer.Dispose();

    private void OnStable()
    {
        StreamPlayerState previous, current;
        lock (_gate)
        {
            if (_pending == _reported)
                return;
            previous = _reported;
            current = _reported = _pending;
        }

        _report(previous, current);
    }
}
