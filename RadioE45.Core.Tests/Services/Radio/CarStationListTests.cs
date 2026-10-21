using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Radio;

namespace RadioE45.Core.Tests.Services.Radio;

public class CarStationListTests
{
    private readonly IAzuraStationCatalog _catalog = Substitute.For<IAzuraStationCatalog>();
    private readonly IAudioService _audio = Substitute.For<IAudioService>();
    private readonly INowPlayingService _nowPlaying = Substitute.For<INowPlayingService>();
    private readonly CarStationList _list;

    public CarStationListTests()
    {
        _catalog.Stations.Returns(new List<AzuraStation>());
        _catalog.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _nowPlaying.Current.Returns(new NowPlayingInfo { Title = "Song" });
        _list = new CarStationList(_catalog, _audio, _nowPlaying, NullLogger<CarStationList>.Instance);
    }

    private static AzuraStation Station(int id, string name, string description = "") =>
        new() { Id = id, Name = name, Description = description };

    [Fact]
    public void Starts_in_Loading_with_a_placeholder_and_no_items()
    {
        Assert.Equal(CarStationListState.Loading, _list.State);
        Assert.Empty(_list.Items);
        Assert.False(string.IsNullOrEmpty(_list.PlaceholderText));
    }

    [Fact]
    public async Task LoadAsync_publishes_the_stations_once_the_catalog_has_loaded()
    {
        _catalog.Stations.Returns(new List<AzuraStation> { Station(1, "A"), Station(2, "B") });
        int changed = 0;
        _list.Changed += () => changed++;

        await _list.LoadAsync();

        Assert.Equal(CarStationListState.Ready, _list.State);
        Assert.Equal(["A", "B"], _list.Items.Select(i => i.Title));
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task A_catalog_that_finishes_loading_after_the_list_was_created_updates_the_list()
    {
        TaskCompletionSource gate = new();
        _catalog.LoadAsync(Arg.Any<CancellationToken>()).Returns(gate.Task);

        Task load = _list.LoadAsync();
        Assert.Equal(CarStationListState.Loading, _list.State);

        _catalog.Stations.Returns(new List<AzuraStation> { Station(1, "A") });
        gate.SetResult();
        await load;

        Assert.Equal(CarStationListState.Ready, _list.State);
        Assert.Single(_list.Items);
    }

    [Fact]
    public async Task LoadAsync_with_no_stations_is_Empty_and_the_placeholder_changes()
    {
        string loading = _list.PlaceholderText;

        await _list.LoadAsync();

        Assert.Equal(CarStationListState.Empty, _list.State);
        Assert.NotEqual(loading, _list.PlaceholderText);
    }

    [Fact]
    public async Task LoadAsync_swallows_a_catalog_failure_and_ends_Empty()
    {
        _catalog.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("db")));

        await _list.LoadAsync();

        Assert.Equal(CarStationListState.Empty, _list.State);
    }

    [Fact]
    public async Task StationsRefreshed_rebuilds_the_list_and_raises_Changed()
    {
        await _list.LoadAsync();
        int changed = 0;
        _list.Changed += () => changed++;

        _catalog.Stations.Returns(new List<AzuraStation> { Station(1, "A") });
        _catalog.StationsRefreshed += Raise.Event<Action>();

        Assert.Equal(CarStationListState.Ready, _list.State);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task The_playing_station_shows_the_track_title_as_subtitle_others_their_description()
    {
        _catalog.Stations.Returns(new List<AzuraStation> { Station(1, "A", "desc A"), Station(2, "B", "desc B") });
        _audio.CurrentStation.Returns(Station(2, "B"));

        await _list.LoadAsync();

        Assert.Equal("desc A", _list.Items[0].Subtitle);
        Assert.False(_list.Items[0].IsCurrent);
        Assert.Equal("Song", _list.Items[1].Subtitle);
        Assert.True(_list.Items[1].IsCurrent);
    }

    [Fact]
    public async Task Dispose_stops_listening_to_the_catalog()
    {
        await _list.LoadAsync();
        int changed = 0;
        _list.Changed += () => changed++;

        _list.Dispose();
        _catalog.StationsRefreshed += Raise.Event<Action>();

        Assert.Equal(0, changed);
    }
}
