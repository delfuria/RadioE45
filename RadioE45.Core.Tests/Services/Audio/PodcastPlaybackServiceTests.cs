using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;

namespace RadioE45.Core.Tests.Services.Audio;

/// <summary>
/// Behaviour moved unchanged from the MAUI PodcastPlayerService (S7b): start retry, muted resume
/// seek on the first real position sample, token guard, progress saving, end of episode.
/// </summary>
public class PodcastPlaybackServiceTests
{
    private static readonly TimeSpan StartCheck = TimeSpan.FromMilliseconds(1500);

    private readonly IAudioService _radio = Substitute.For<IAudioService>();
    private readonly IPodcastProgressRepository _progress = Substitute.For<IPodcastProgressRepository>();
    private readonly FakeTimeProvider _time = new();
    private readonly FakePodcastPlayer _player = new();
    private readonly PodcastPlaybackService _service;

    public PodcastPlaybackServiceTests()
    {
        _service = new PodcastPlaybackService(_radio, _progress, new InlineDispatcher(), _time, NullLogger.Instance);
        _service.AttachPlayer(_player);
    }

    private static readonly AzuraStation Station = new() { Id = 3, StationId = 30, Name = "E45" };

    private static AzuraCastPodcastEpisode Episode(string id) => new()
    {
        Id = id,
        PodcastId = "pod",
        Links = new AzuraCastPodcastEpisodeLinks { Download = $"https://radio.example/{id}.mp3" },
    };

    private Task PlayAsync(AzuraCastPodcastEpisode episode, int startSeconds = 0) =>
        AdvanceUntilCompletedAsync(_service.PlayAsync(Station, episode, startSeconds));

    // Each start attempt waits on the fake clock; its continuation (and the next attempt's delay)
    // runs on the thread pool, so time is advanced step by step until the task completes.
    private async Task AdvanceUntilCompletedAsync(Task task)
    {
        for (int i = 0; i < 100 && !task.IsCompleted; i++)
        {
            _time.Advance(StartCheck);
            await Task.Delay(20);
        }

        await task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Play_stops_the_radio_and_opens_the_episode_at_full_volume()
    {
        await PlayAsync(Episode("e1"));

        await _radio.Received(1).StopAsync();
        Assert.Equal(["https://radio.example/e1.mp3"], _player.Opened);
        Assert.Equal(1, _player.Volume);
        Assert.Equal("e1", _service.CurrentEpisode?.Id);
    }

    [Fact]
    public async Task Open_is_retried_while_the_player_ignores_it_then_failure_is_reported()
    {
        _player.StateAfterOpen = StreamPlayerState.None; // native player not ready: request dropped
        var failures = new List<string?>();
        _service.PlaybackFailed += (_, m) => failures.Add(m);

        Task play = _service.PlayAsync(Station, Episode("e1"), 0);
        await AdvanceUntilCompletedAsync(play);

        Assert.Equal(5, _player.Opened.Count);
        Assert.Equal([null], failures);
    }

    [Fact]
    public async Task Media_failure_message_is_reported()
    {
        _player.FailOnOpen = "403 Forbidden";
        var failures = new List<string?>();
        _service.PlaybackFailed += (_, m) => failures.Add(m);

        await PlayAsync(Episode("e1"));

        Assert.Contains("403 Forbidden", failures);
    }

    [Fact]
    public async Task Resume_is_muted_and_seeks_on_the_first_position_sample_only()
    {
        var positions = new List<TimeSpan>();
        _service.PositionChanged += (_, p) => positions.Add(p);

        await PlayAsync(Episode("e1"), startSeconds: 120);
        Assert.Equal(0, _player.Volume);

        _player.RaisePosition(TimeSpan.FromSeconds(0.2)); // pre-seek sample: seek, not forwarded
        Assert.Equal([TimeSpan.FromSeconds(120)], _player.Seeks);
        Assert.Empty(positions);
        Assert.Equal(1, _player.Volume); // seek completed: unmuted

        _player.RaisePosition(TimeSpan.FromSeconds(121));
        Assert.Equal([TimeSpan.FromSeconds(121)], positions);
    }

    [Fact]
    public async Task Samples_during_the_resume_seek_are_suppressed()
    {
        _player.SeekCompletion = new TaskCompletionSource();
        var positions = new List<TimeSpan>();
        _service.PositionChanged += (_, p) => positions.Add(p);

        await PlayAsync(Episode("e1"), startSeconds: 60);
        _player.RaisePosition(TimeSpan.Zero);
        _player.RaisePosition(TimeSpan.FromSeconds(0.5)); // seek still in flight

        Assert.Empty(positions);
        Assert.Equal(0, _player.Volume);

        _player.SeekCompletion.SetResult();
        _player.RaisePosition(TimeSpan.FromSeconds(61));
        Assert.Equal([TimeSpan.FromSeconds(61)], positions);
        Assert.Equal(1, _player.Volume);
    }

    [Fact]
    public async Task Switching_episode_drops_the_previous_resume_target()
    {
        Task first = _service.PlayAsync(Station, Episode("e1"), 300);
        Task second = _service.PlayAsync(Station, Episode("e2"), 0);
        await AdvanceUntilCompletedAsync(Task.WhenAll(first, second));

        _player.RaisePosition(TimeSpan.FromSeconds(1));

        Assert.Empty(_player.Seeks);
        Assert.Equal("e2", _service.CurrentEpisode?.Id);
    }

    [Fact]
    public async Task Pause_and_stop_save_the_position()
    {
        await PlayAsync(Episode("e1"));
        _player.Position = TimeSpan.FromSeconds(42);

        await _service.PauseAsync();
        Assert.False(_player.IsPlayingRaw);
        await _progress.Received(1).SaveAsync(30, 3, "pod", "e1", 42, false);

        _player.Position = TimeSpan.FromSeconds(50);
        await _service.StopAsync();
        await _progress.Received(1).SaveAsync(30, 3, "pod", "e1", 50, false);
        Assert.True(_player.Closed);
        Assert.Null(_service.CurrentEpisode);
        Assert.False(_service.IsPlaying);
    }

    [Fact]
    public async Task Stop_keeps_the_last_known_position_when_the_player_lost_it()
    {
        // MAUI: leaving the Podcast tab tears down the page's MediaElement, which then reads 0.
        await PlayAsync(Episode("e1"));
        _player.RaisePosition(TimeSpan.FromSeconds(95));
        _player.Position = TimeSpan.Zero;
        _progress.ClearReceivedCalls(); // the sample also triggered the periodic save

        await _service.StopAsync();

        await _progress.Received(1).SaveAsync(30, 3, "pod", "e1", 95, false);
    }

    [Fact]
    public async Task Stop_before_any_sample_keeps_the_resume_position()
    {
        await PlayAsync(Episode("e1"), startSeconds: 300);
        _player.Position = TimeSpan.Zero; // resume seek not happened yet

        await _service.StopAsync();

        await _progress.Received(1).SaveAsync(30, 3, "pod", "e1", 300, false);
    }

    [Fact]
    public async Task Resume_plays_from_the_current_position()
    {
        await PlayAsync(Episode("e1"));
        await _service.PauseAsync();

        await _service.ResumeAsync();

        Assert.True(_player.IsPlayingRaw);
        Assert.Single(_player.Opened); // a real pause: not reopened (unlike live radio)
    }

    [Fact]
    public async Task End_of_episode_saves_it_as_completed()
    {
        await PlayAsync(Episode("e1"));
        _player.Position = TimeSpan.FromSeconds(900);
        var completed = 0;
        _service.EpisodeCompleted += (_, _) => completed++;

        _player.RaiseEnded();
        await Task.Yield();

        await _progress.Received(1).SaveAsync(30, 3, "pod", "e1", 900, true);
        Assert.Equal(1, completed);
        Assert.Null(_service.CurrentEpisode);
    }

    [Fact]
    public async Task Progress_is_saved_at_most_every_five_seconds_while_playing()
    {
        await PlayAsync(Episode("e1"));

        _player.RaisePosition(TimeSpan.FromSeconds(1));
        _time.Advance(TimeSpan.FromSeconds(1));
        _player.RaisePosition(TimeSpan.FromSeconds(2));
        await _progress.ReceivedWithAnyArgs(1).SaveAsync(default, default, default!, default!, default, default);

        _time.Advance(TimeSpan.FromSeconds(5));
        _player.RaisePosition(TimeSpan.FromSeconds(7));
        await _progress.ReceivedWithAnyArgs(2).SaveAsync(default, default, default!, default!, default, default);
    }

    [Theory]
    [InlineData(2, false, 0)]   // too close to the start
    [InlineData(600, true, 0)]  // completed: start over
    [InlineData(600, false, 600)]
    public async Task Resume_position_from_saved_progress(int saved, bool completed, int expected)
    {
        _progress.GetAsync(30, "e1").Returns(new PodcastEpisodeProgress { PositionSeconds = saved, IsCompleted = completed });

        Assert.Equal(expected, await _service.GetResumePositionSecondsAsync(Station, Episode("e1")));
    }

    [Fact]
    public async Task Player_state_drives_is_playing()
    {
        var states = new List<bool>();
        _service.PlaybackStateChanged += (_, p) => states.Add(p);

        await PlayAsync(Episode("e1"));

        Assert.True(_service.IsPlaying);
        Assert.Contains(true, states);
    }

    private sealed class FakePodcastPlayer : IPodcastMediaPlayer
    {
        public List<string> Opened { get; } = [];
        public List<TimeSpan> Seeks { get; } = [];
        public StreamPlayerState StateAfterOpen { get; set; } = StreamPlayerState.Playing;
        public string? FailOnOpen { get; set; }
        public TaskCompletionSource? SeekCompletion { get; set; }
        public bool Closed { get; private set; }
        public bool IsPlayingRaw { get; private set; }

        public StreamPlayerState State { get; private set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Duration => TimeSpan.FromMinutes(30);
        public double Volume { get; set; } = 1;

        public event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;
        public event EventHandler<TimeSpan>? PositionChanged;
        public event EventHandler<string?>? Failed;
        public event EventHandler? Ended;

        public void Open(string url)
        {
            Opened.Add(url);
            if (FailOnOpen is not null)
            {
                Failed?.Invoke(this, FailOnOpen);
                SetState(StreamPlayerState.Failed);
                return;
            }

            SetState(StateAfterOpen);
            IsPlayingRaw = StateAfterOpen == StreamPlayerState.Playing;
        }

        public void Play()
        {
            IsPlayingRaw = true;
            SetState(StreamPlayerState.Playing);
        }

        public void Pause()
        {
            IsPlayingRaw = false;
            SetState(StreamPlayerState.Paused);
        }

        public void Close()
        {
            Closed = true;
            IsPlayingRaw = false;
            SetState(StreamPlayerState.Stopped);
        }

        public Task SeekAsync(TimeSpan position)
        {
            Seeks.Add(position);
            return SeekCompletion?.Task ?? Task.CompletedTask;
        }

        public void RaisePosition(TimeSpan position)
        {
            Position = position;
            PositionChanged?.Invoke(this, position);
        }

        public void RaiseEnded() => Ended?.Invoke(this, EventArgs.Empty);

        private void SetState(StreamPlayerState state)
        {
            var previous = State;
            State = state;
            StateChanged?.Invoke(this, new StreamPlayerStateChangedEventArgs(previous, state));
        }
    }

    private sealed class InlineDispatcher : IUiDispatcher
    {
        public bool IsUiThread => true;

        public void Post(Action action) => action();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task InvokeAsync(Func<Task> action) => action();

        public IUiTimer? CreateTimer(TimeSpan interval, Action tick) => null;
    }
}
