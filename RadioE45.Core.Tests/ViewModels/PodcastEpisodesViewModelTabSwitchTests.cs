using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;
using RadioE45.Services.Radio;
using RadioE45.ViewModels;

namespace RadioE45.Core.Tests.ViewModels;

/// <summary>
/// Bug reported on 2026-10-15 (MAUI, present in earlier versions too): listen to an episode,
/// switch to On Air, come back to the episodes page (same page instance, kept in the Podcast tab
/// stack) and tap the episode again → nothing played and the player showed 0, while the saved
/// position was correct in the DB. Going back to the podcast list and reopening worked.
/// Returning to On Air restarts the radio, which stops the episode directly on the podcast
/// service (OnAirViewModel.StartPlayAndNowPollingAsync), behind the episodes ViewModel's back.
/// Both variants are covered because Shell does not guarantee OnDisappearing for a page pushed
/// above the tab root when switching tabs.
/// </summary>
public class PodcastEpisodesViewModelTabSwitchTests
{
    private static readonly TimeSpan StartCheck = TimeSpan.FromMilliseconds(1500);

    private readonly FakeTimeProvider _time = new();
    private readonly FakePodcastPlayer _player = new();
    private readonly InMemoryProgressRepository _progress = new();
    private readonly PodcastPlaybackService _service;
    private readonly OnAirViewModel _onAir;
    private readonly PodcastEpisodesViewModel _vm;

    private static readonly AzuraStation Station = new() { Id = 3, StationId = 30, Name = "Demo" };

    public PodcastEpisodesViewModelTabSwitchTests()
    {
        var dispatcher = new InlineDispatcher();
        _service = new PodcastPlaybackService(Substitute.For<IAudioService>(), _progress, dispatcher, _time, NullLogger.Instance);
        _service.AttachPlayer(_player);

        var podcasts = Substitute.For<IPodcastService>();
        podcasts.GetEpisodesAsync(Arg.Any<AzuraStation>(), Arg.Any<AzuraCastPodcast>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new List<AzuraCastPodcastEpisode> { Episode() })); // fresh objects, as from the API

        _onAir = new OnAirViewModel(
            Substitute.For<IAudioService>(), _service, podcasts, Substitute.For<INowPlayingService>(),
            Substitute.For<IAzuraStationCatalog>(), Substitute.For<IAppSettingsRepository>(),
            Substitute.For<IScheduleService>(), Substitute.For<INavigationService>(), dispatcher,
            Substitute.For<ISettingsStore>(), Substitute.For<IAppEnvironment>(), NullLogger<OnAirViewModel>.Instance)
        {
            CurrentStation = Station,
        };

        _vm = new PodcastEpisodesViewModel(podcasts, _service, _progress, _onAir, dispatcher,
            NullLogger<PodcastEpisodesViewModel>.Instance)
        {
            PodcastId = "pod",
        };
    }

    private static AzuraCastPodcastEpisode Episode() => new()
    {
        Id = "e1",
        PodcastId = "pod",
        Title = "Episode 1",
        Media = new AzuraCastPodcastMedia { LengthSeconds = 600 },
        Links = new AzuraCastPodcastEpisodeLinks { Download = "https://radio.example/e1.mp3" },
    };

    private AzuraCastPodcastEpisode Card => Assert.Single(_vm.Episodes);

    private async Task TapAsync(AzuraCastPodcastEpisode episode)
    {
        Task tap = _vm.PlayEpisodeCommand.ExecuteAsync(episode);
        for (int i = 0; i < 100 && !tap.IsCompleted; i++)
        {
            _time.Advance(StartCheck);
            await Task.Delay(20);
        }

        await tap.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private async Task ListenUntilAsync(int seconds)
    {
        await _vm.LoadEpisodesCommand.ExecuteAsync(null);
        await TapAsync(Card);
        _player.RaisePosition(TimeSpan.FromSeconds(seconds));
    }

    // On Air → OnAirViewModel.RestartAsync → StartPlayAndNowPollingAsync stops the episode.
    private Task RadioRestartStopsEpisodeAsync() => _service.StopAsync();

    [Fact]
    public async Task Tab_switch_without_page_lifecycle_then_tap_resumes_from_saved_position()
    {
        await ListenUntilAsync(11);

        await RadioRestartStopsEpisodeAsync();

        // The episodes ViewModel must notice that the player no longer holds the episode...
        Assert.Null(_vm.CurrentEpisode);
        Assert.False(_vm.IsPlaying);
        // ...and the card shows the saved position, without reloading the list.
        Assert.Equal(11, Card.ResumePositionSeconds);
        Assert.Equal(PodcastEpisodeProgressState.InProgress, Card.ProgressState);

        await TapAsync(Card);

        Assert.Equal(2, _player.Opened.Count);   // reopened, not a Play() on a closed player
        Assert.Equal(0, _player.Volume);         // muted until the resume seek
        _player.RaisePosition(TimeSpan.FromSeconds(0.2));
        Assert.Equal([TimeSpan.FromSeconds(11)], _player.Seeks);
        Assert.Equal("0:11", _vm.ElapsedTimeText);
    }

    [Fact]
    public async Task Tab_switch_with_page_lifecycle_then_tap_resumes_from_saved_position()
    {
        await ListenUntilAsync(11);

        // Episodes page OnDisappearing + On Air restart racing on the same service.
        Task leave = _vm.StopPlaybackAsync();
        await RadioRestartStopsEpisodeAsync();
        await leave;
        _vm.Cleanup();

        // Back to the Podcast tab: OnAppearing.
        _vm.Activate();
        await _vm.LoadEpisodesCommand.ExecuteAsync(null);
        Assert.Equal(11, Card.ResumePositionSeconds);

        await TapAsync(Card);

        Assert.Equal(2, _player.Opened.Count);
        _player.RaisePosition(TimeSpan.FromSeconds(0.2));
        Assert.Equal([TimeSpan.FromSeconds(11)], _player.Seeks);
    }

    [Fact]
    public async Task Position_sample_after_stop_does_not_reset_the_display()
    {
        await ListenUntilAsync(11);

        await RadioRestartStopsEpisodeAsync();
        var forwarded = new List<TimeSpan>();
        _service.PositionChanged += (_, p) => forwarded.Add(p);
        _player.RaisePosition(TimeSpan.Zero); // native player reports 0 after Stop

        Assert.Empty(forwarded);           // no episode: the sample is not forwarded to the UI
        Assert.Null(_vm.CurrentEpisode);   // mini player hidden instead of showing 0:00
        Assert.Equal(11, _progress.Saved["e1"].PositionSeconds);
    }

    [Fact]
    public async Task Pause_updates_the_card_live()
    {
        await ListenUntilAsync(42);

        await _vm.PlayPauseCommand.ExecuteAsync(null);

        Assert.Equal(42, Card.ResumePositionSeconds);
        Assert.Equal("0:42", Card.ResumePositionText);
    }

    internal sealed class InMemoryProgressRepository : IPodcastProgressRepository
    {
        public Dictionary<string, PodcastEpisodeProgress> Saved { get; } = [];

        public Task<PodcastEpisodeProgress?> GetAsync(int stationId, string episodeId) =>
            Task.FromResult(Saved.TryGetValue(episodeId, out var p) && p.StationId == stationId ? p : null);

        public Task<List<PodcastEpisodeProgress>> GetForPodcastAsync(int stationId, string podcastId) =>
            Task.FromResult(Saved.Values.Where(p => p.StationId == stationId && p.PodcastId == podcastId).ToList());

        public Task SaveAsync(int stationId, int radioStationId, string podcastId, string episodeId, int positionSeconds, bool isCompleted)
        {
            Saved[episodeId] = new PodcastEpisodeProgress
            {
                StationId = stationId, RadioStationId = radioStationId, PodcastId = podcastId,
                EpisodeId = episodeId, PositionSeconds = positionSeconds, IsCompleted = isCompleted,
            };
            return Task.CompletedTask;
        }
    }

    internal sealed class FakePodcastPlayer : IPodcastMediaPlayer
    {
        public List<string> Opened { get; } = [];
        public List<TimeSpan> Seeks { get; } = [];
        public StreamPlayerState State { get; private set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Duration => TimeSpan.FromMinutes(10);
        public double Volume { get; set; } = 1;

        public event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;
        public event EventHandler<TimeSpan>? PositionChanged;
        public event EventHandler<string?>? Failed;
        public event EventHandler? Ended;

        public void Open(string url)
        {
            Opened.Add(url);
            SetState(StreamPlayerState.Playing);
        }

        public void Play() => SetState(StreamPlayerState.Playing);

        public void Pause() => SetState(StreamPlayerState.Paused);

        public void Close()
        {
            Position = TimeSpan.Zero; // like a torn-down / stopped native player
            SetState(StreamPlayerState.Stopped);
        }

        public Task SeekAsync(TimeSpan position)
        {
            Seeks.Add(position);
            Position = position;
            return Task.CompletedTask;
        }

        public void RaisePosition(TimeSpan position)
        {
            Position = position;
            PositionChanged?.Invoke(this, position);
        }

        private void SetState(StreamPlayerState state)
        {
            var previous = State;
            State = state;
            StateChanged?.Invoke(this, new StreamPlayerStateChangedEventArgs(previous, state));
        }

        public void RaiseFailedOrEnded()
        {
            Failed?.Invoke(this, null);
            Ended?.Invoke(this, EventArgs.Empty);
        }
    }

    internal sealed class InlineDispatcher : IUiDispatcher
    {
        public bool IsUiThread => true;
        public void Post(Action action) => action();
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
        public Task InvokeAsync(Func<Task> action) => action();
        public IUiTimer? CreateTimer(TimeSpan interval, Action tick) => null;
    }
}
