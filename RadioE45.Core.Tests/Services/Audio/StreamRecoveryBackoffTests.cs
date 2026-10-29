using RadioE45.Services.Audio;

namespace RadioE45.Core.Tests.Services.Audio;

public sealed class StreamRecoveryBackoffTests
{
    [Fact]
    public void Delays_GrowThenCapAtThirtySeconds()
    {
        StreamRecoveryBackoff backoff = new();
        List<int> seconds = [];

        for (int i = 0; i < 6; i++)
        {
            Assert.True(backoff.TryScheduleNext(out TimeSpan delay, out _));
            seconds.Add((int)delay.TotalSeconds);
        }

        Assert.Equal([3, 5, 10, 20, 30, 30], seconds);
    }

    [Fact]
    public void StopsAfterMaxAttempts()
    {
        StreamRecoveryBackoff backoff = new();

        for (int i = 0; i < StreamRecoveryBackoff.MaxAttempts; i++)
            Assert.True(backoff.TryScheduleNext(out _, out _));

        Assert.False(backoff.TryScheduleNext(out _, out _));
        Assert.Equal(StreamRecoveryBackoff.MaxAttempts, backoff.Attempt);
    }

    [Fact]
    public void Cancel_InvalidatesPendingRetryAndResetsAttempts()
    {
        StreamRecoveryBackoff backoff = new();
        backoff.TryScheduleNext(out _, out int generation);
        Assert.True(backoff.IsCurrent(generation));

        backoff.Cancel();

        Assert.False(backoff.IsCurrent(generation));
        Assert.Equal(0, backoff.Attempt);
        backoff.TryScheduleNext(out TimeSpan delay, out _);
        Assert.Equal(TimeSpan.FromSeconds(3), delay);
    }

    [Fact]
    public void Cancel_ReopensBudgetAfterExhaustion()
    {
        StreamRecoveryBackoff backoff = new();
        for (int i = 0; i < StreamRecoveryBackoff.MaxAttempts; i++)
            backoff.TryScheduleNext(out _, out _);

        backoff.Cancel();

        Assert.True(backoff.TryScheduleNext(out _, out _));
    }

    [Fact]
    public void Restart_SupersedesThePendingRetryAndStartsAgainFromTheFirstDelay()
    {
        StreamRecoveryBackoff backoff = new();
        backoff.TryScheduleNext(out _, out int pending);
        backoff.TryScheduleNext(out _, out _);

        int restarted = backoff.Restart();

        Assert.False(backoff.IsCurrent(pending));
        Assert.True(backoff.IsCurrent(restarted));
        Assert.Equal(0, backoff.Attempt);
        backoff.TryScheduleNext(out TimeSpan delay, out _);
        Assert.Equal(TimeSpan.FromSeconds(3), delay);
    }

    [Fact]
    public void Restart_ReopensBudgetAfterGivingUp()
    {
        StreamRecoveryBackoff backoff = new();
        for (int i = 0; i < StreamRecoveryBackoff.MaxAttempts; i++)
            backoff.TryScheduleNext(out _, out _);
        Assert.False(backoff.TryScheduleNext(out _, out _));

        backoff.Restart();

        Assert.True(backoff.TryScheduleNext(out _, out _));
    }

    [Fact]
    public void NewSchedule_SupersedesThePreviousOne()
    {
        StreamRecoveryBackoff backoff = new();
        backoff.TryScheduleNext(out _, out int first);
        backoff.TryScheduleNext(out _, out int second);

        Assert.False(backoff.IsCurrent(first));
        Assert.True(backoff.IsCurrent(second));
    }
}
