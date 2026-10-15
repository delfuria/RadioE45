using Microsoft.Extensions.Time.Testing;
using RadioE45.Services.Audio;

namespace RadioE45.Core.Tests.Services.Audio;

public class StreamStateDebouncerTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(400);

    private readonly FakeTimeProvider _time = new();
    private readonly List<(StreamPlayerState Previous, StreamPlayerState Current)> _reports = [];
    private readonly StreamStateDebouncer _debouncer;

    public StreamStateDebouncerTests()
    {
        _debouncer = new StreamStateDebouncer(Delay, _time, (p, c) => _reports.Add((p, c)));
    }

    [Fact]
    public void Stable_state_is_reported_after_the_delay()
    {
        _debouncer.Push(StreamPlayerState.Playing);
        _time.Advance(TimeSpan.FromMilliseconds(399));
        Assert.Empty(_reports);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal([(StreamPlayerState.None, StreamPlayerState.Playing)], _reports);
        Assert.Equal(StreamPlayerState.Playing, _debouncer.ReportedState);
    }

    [Fact]
    public void Flapping_after_open_reports_only_the_final_state()
    {
        // Sequence seen in the S1 spike within a few milliseconds.
        foreach (var state in new[]
                 {
                     StreamPlayerState.Opening, StreamPlayerState.Buffering, StreamPlayerState.Opening,
                     StreamPlayerState.Playing, StreamPlayerState.Paused, StreamPlayerState.Buffering,
                     StreamPlayerState.Playing,
                 })
        {
            _debouncer.Push(state);
            _time.Advance(TimeSpan.FromMilliseconds(5));
        }

        _time.Advance(Delay);

        Assert.Equal([(StreamPlayerState.None, StreamPlayerState.Playing)], _reports);
    }

    [Fact]
    public void Glitch_back_to_the_reported_state_is_not_reported()
    {
        _debouncer.Push(StreamPlayerState.Playing);
        _time.Advance(Delay);
        _reports.Clear();

        _debouncer.Push(StreamPlayerState.Paused);
        _time.Advance(TimeSpan.FromMilliseconds(50));
        _debouncer.Push(StreamPlayerState.Playing);
        _time.Advance(Delay * 2);

        Assert.Empty(_reports);
    }

    [Fact]
    public void Report_now_is_immediate_and_cancels_the_pending_state()
    {
        _debouncer.Push(StreamPlayerState.Playing);
        _time.Advance(Delay);
        _debouncer.Push(StreamPlayerState.Buffering);

        _debouncer.ReportNow(StreamPlayerState.Stopped);
        _time.Advance(Delay * 2);

        Assert.Equal(
            [(StreamPlayerState.None, StreamPlayerState.Playing), (StreamPlayerState.Playing, StreamPlayerState.Stopped)],
            _reports);
    }

    [Fact]
    public void Report_now_of_the_current_state_does_nothing()
    {
        _debouncer.ReportNow(StreamPlayerState.None);

        Assert.Empty(_reports);
    }
}
