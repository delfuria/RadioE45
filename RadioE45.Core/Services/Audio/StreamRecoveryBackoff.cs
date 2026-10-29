namespace RadioE45.Services.Audio;

/// <summary>
/// Bounded backoff schedule for re-opening a live stream after every candidate URL failed (a tunnel, a
/// coverage gap), plus a generation token so a pending retry can be invalidated by anything that ends
/// the listener's intent to play: pause, stop, station change, task removal.
/// </summary>
/// <remarks>
/// Pure policy, no player or platform types: the platform service owns the timer and the player calls.
/// A retry scheduled by <see cref="TryScheduleNext"/> must check <see cref="IsCurrent"/> before acting.
/// </remarks>
public sealed class StreamRecoveryBackoff
{
    /// <summary>Retries allowed before giving up, until <see cref="Cancel"/> resets the count.</summary>
    public const int MaxAttempts = 20;

    private static readonly TimeSpan[] InitialDelays =
    [
        TimeSpan.FromSeconds(3),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
    ];

    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private int _generation;
    private int _attempt;

    /// <summary>Retries scheduled since the last <see cref="Cancel"/>.</summary>
    public int Attempt
    {
        get { lock (_gate) return _attempt; }
    }

    /// <summary>
    /// Books the next retry. Returns false once <see cref="MaxAttempts"/> retries have been spent.
    /// Scheduling invalidates any retry booked earlier.
    /// </summary>
    public bool TryScheduleNext(out TimeSpan delay, out int generation)
    {
        lock (_gate)
        {
            if (_attempt >= MaxAttempts)
            {
                delay = TimeSpan.Zero;
                generation = _generation;
                return false;
            }

            delay = _attempt < InitialDelays.Length ? InitialDelays[_attempt] : MaxDelay;
            _attempt++;
            generation = ++_generation;
            return true;
        }
    }

    /// <summary>True while no <see cref="Cancel"/> or newer <see cref="TryScheduleNext"/> has superseded <paramref name="generation"/>.</summary>
    public bool IsCurrent(int generation)
    {
        lock (_gate) return generation == _generation;
    }

    /// <summary>
    /// Supersedes any pending retry and starts a fresh schedule: the network came back, so the retry should not
    /// wait for the timer, and a recovery that had given up gets its whole budget again. Returns the generation
    /// of the retry the caller runs right away.
    /// </summary>
    public int Restart()
    {
        lock (_gate)
        {
            _attempt = 0;
            return ++_generation;
        }
    }

    /// <summary>Invalidates any pending retry and resets the attempt count (playback resumed, or the listener stopped it).</summary>
    public void Cancel()
    {
        lock (_gate)
        {
            _generation++;
            _attempt = 0;
        }
    }
}
