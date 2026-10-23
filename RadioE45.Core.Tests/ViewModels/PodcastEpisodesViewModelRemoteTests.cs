using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;
using RadioE45.Services.Radio;
using RadioE45.ViewModels;
using FakePodcastPlayer = RadioE45.Core.Tests.ViewModels.PodcastEpisodesViewModelTabSwitchTests.FakePodcastPlayer;
using InMemoryProgressRepository = RadioE45.Core.Tests.ViewModels.PodcastEpisodesViewModelTabSwitchTests.InMemoryProgressRepository;
using InlineDispatcher = RadioE45.Core.Tests.ViewModels.PodcastEpisodesViewModelTabSwitchTests.InlineDispatcher;

namespace RadioE45.Core.Tests.ViewModels;

/// <summary>Commands added for the remote control (Android TV): relative seek and filter cycling.</summary>
public class PodcastEpisodesViewModelRemoteTests
{
    private static readonly TimeSpan StartCheck = TimeSpan.FromMilliseconds(1500);
    private static readonly AzuraStation Station = new() { Id = 3, StationId = 30, Name = "Demo" };

    private readonly FakeTimeProvider _time = new();
    private readonly FakePodcastPlayer _player = new();
    private readonly PodcastPlaybackService _service;
    private readonly PodcastEpisodesViewModel _vm;

    public PodcastEpisodesViewModelRemoteTests()
    {
        var dispatcher = new InlineDispatcher();
        var progress = new InMemoryProgressRepository();
        _service = new PodcastPlaybackService(Substitute.For<IAudioService>(), progress, dispatcher, _time, NullLogger.Instance);
        _service.AttachPlayer(_player);

        var podcasts = Substitute.For<IPodcastService>();
        podcasts.GetEpisodesAsync(Arg.Any<AzuraStation>(), Arg.Any<AzuraCastPodcast>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new List<AzuraCastPodcastEpisode>
            {
                Episode("e1", season: 1),
                Episode("e2", season: 2),
            }));

        var onAir = new OnAirViewModel(
            Substitute.For<IAudioService>(), _service, podcasts, Substitute.For<INowPlayingService>(),
            Substitute.For<IAzuraStationCatalog>(), Substitute.For<IAppSettingsRepository>(),
            Substitute.For<IScheduleService>(), Substitute.For<INavigationService>(), dispatcher,
            Substitute.For<ISettingsStore>(), Substitute.For<IAppEnvironment>(), NullLogger<OnAirViewModel>.Instance)
        {
            CurrentStation = Station,
        };

        _vm = new PodcastEpisodesViewModel(podcasts, _service, progress, onAir, dispatcher,
            NullLogger<PodcastEpisodesViewModel>.Instance)
        {
            PodcastId = "pod",
        };
    }

    private static AzuraCastPodcastEpisode Episode(string id, int season) => new()
    {
        Id = id,
        PodcastId = "pod",
        Title = id,
        SeasonNumber = season,
        EpisodeNumber = 1,
        Media = new AzuraCastPodcastMedia { LengthSeconds = 600 },
        Links = new AzuraCastPodcastEpisodeLinks { Download = $"https://radio.example/{id}.mp3" },
    };

    [Fact]
    public async Task Cycling_the_season_filter_walks_the_options_and_wraps_around()
    {
        await _vm.LoadEpisodesCommand.ExecuteAsync(null);
        Assert.Equal(2, _vm.Episodes.Count);          // "all seasons"

        _vm.CycleSeasonCommand.Execute(null);
        Assert.Equal(1, _vm.SelectedSeason!.Value);
        Assert.Equal("e1", Assert.Single(_vm.Episodes).Id);

        _vm.CycleSeasonCommand.Execute(null);
        Assert.Equal(2, _vm.SelectedSeason!.Value);

        _vm.CycleSeasonCommand.Execute(null);
        Assert.Null(_vm.SelectedSeason!.Value);       // back to "all seasons"
        Assert.Equal(2, _vm.Episodes.Count);
    }

    [Fact]
    public async Task Cycling_the_status_filter_selects_the_next_status()
    {
        await _vm.LoadEpisodesCommand.ExecuteAsync(null);

        _vm.CycleStatusCommand.Execute(null);

        Assert.Equal(PodcastEpisodeProgressState.NotStarted, _vm.SelectedStatus!.Value);
        Assert.Equal(2, _vm.Episodes.Count);
    }

    [Fact]
    public async Task Seek_by_moves_from_the_current_position_and_stays_inside_the_episode()
    {
        await _vm.LoadEpisodesCommand.ExecuteAsync(null);
        Task play = _vm.PlayEpisodeCommand.ExecuteAsync(_vm.Episodes[0]);
        for (int i = 0; i < 100 && !play.IsCompleted; i++)
        {
            _time.Advance(StartCheck);
            await Task.Delay(20);
        }

        await play.WaitAsync(TimeSpan.FromSeconds(5));
        _player.RaisePosition(TimeSpan.FromSeconds(100));
        _player.Seeks.Clear();

        await _vm.SeekByCommand.ExecuteAsync(30);
        await _vm.SeekByCommand.ExecuteAsync(-500);
        await _vm.SeekByCommand.ExecuteAsync(10_000);

        Assert.Equal(
            [TimeSpan.FromSeconds(130), TimeSpan.Zero, TimeSpan.FromSeconds(600)],
            _player.Seeks.Take(3).ToList());
    }
}
