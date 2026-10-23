using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;
using RadioE45.Services.Radio;
using RadioE45.ViewModels;

namespace RadioE45.Core.Tests.ViewModels;

// Apple Local Network alert: the first search on a LAN host can fail while the alert is on screen,
// so private addresses are retried (Apple only) until the user answers.
public class AddStationViewModelTests
{
    private const string LanHost = "192.168.1.100";
    private const string PublicHost = "radioe45.ddns.net";

    private readonly IStationListService _stationList = Substitute.For<IStationListService>();
    private readonly IRadioRepository _repository = Substitute.For<IRadioRepository>();
    private readonly IAppEnvironment _environment = Substitute.For<IAppEnvironment>();
    private readonly List<TimeSpan> _delays = [];

    private static readonly List<AzuraCastStationListItem> Stations = [new() { Id = 1, Name = "Studio" }];

    private AddStationViewModel CreateViewModel(bool hasLocalNetworkPrivacy, string urlBase)
    {
        _environment.HasLocalNetworkPrivacy.Returns(hasLocalNetworkPrivacy);
        _repository.GetAllAsync().Returns(new List<RadioStation>());

        return new AddStationViewModel(
            _stationList,
            _repository,
            Substitute.For<IAzuraStationCatalog>(),
            Substitute.For<INavigationService>(),
            _environment,
            NullLogger<AddStationViewModel>.Instance)
        {
            UrlBase = urlBase,
            DelayAsync = delay => { _delays.Add(delay); return Task.CompletedTask; },
        };
    }

    [Fact]
    public async Task Lan_host_on_apple_is_retried_until_the_alert_is_answered()
    {
        _stationList.FetchAsync(LanHost, Arg.Any<CancellationToken>())
            .Returns((List<AzuraCastStationListItem>?)null, null, Stations);
        AddStationViewModel vm = CreateViewModel(hasLocalNetworkPrivacy: true, LanHost);

        await vm.SearchCommand.ExecuteAsync(null);

        await _stationList.Received(3).FetchAsync(LanHost, Arg.Any<CancellationToken>());
        Assert.True(vm.HasResults);
        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.AvailableStations);
        Assert.All(_delays, d => Assert.Equal(AddStationViewModel.LocalNetworkRetryDelay, d));
    }

    [Fact]
    public async Task Lan_host_on_apple_gives_up_after_the_max_attempts()
    {
        _stationList.FetchAsync(LanHost, Arg.Any<CancellationToken>()).Returns((List<AzuraCastStationListItem>?)null);
        AddStationViewModel vm = CreateViewModel(hasLocalNetworkPrivacy: true, LanHost);

        await vm.SearchCommand.ExecuteAsync(null);

        await _stationList.Received(AddStationViewModel.LocalNetworkMaxAttempts).FetchAsync(LanHost, Arg.Any<CancellationToken>());
        Assert.False(vm.HasResults);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task Public_host_is_not_retried()
    {
        _stationList.FetchAsync(PublicHost, Arg.Any<CancellationToken>()).Returns((List<AzuraCastStationListItem>?)null);
        AddStationViewModel vm = CreateViewModel(hasLocalNetworkPrivacy: true, PublicHost);

        await vm.SearchCommand.ExecuteAsync(null);

        await _stationList.Received(1).FetchAsync(PublicHost, Arg.Any<CancellationToken>());
        Assert.Empty(_delays);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task Lan_host_is_not_retried_without_local_network_privacy()
    {
        _stationList.FetchAsync(LanHost, Arg.Any<CancellationToken>()).Returns((List<AzuraCastStationListItem>?)null);
        AddStationViewModel vm = CreateViewModel(hasLocalNetworkPrivacy: false, LanHost);

        await vm.SearchCommand.ExecuteAsync(null);

        await _stationList.Received(1).FetchAsync(LanHost, Arg.Any<CancellationToken>());
        Assert.Empty(_delays);
    }

    [Fact]
    public async Task First_success_on_a_lan_host_does_not_wait()
    {
        _stationList.FetchAsync(LanHost, Arg.Any<CancellationToken>()).Returns(Stations);
        AddStationViewModel vm = CreateViewModel(hasLocalNetworkPrivacy: true, LanHost);

        await vm.SearchCommand.ExecuteAsync(null);

        await _stationList.Received(1).FetchAsync(LanHost, Arg.Any<CancellationToken>());
        Assert.Empty(_delays);
        Assert.True(vm.HasResults);
    }

    // The TV view lives as long as its root page: every visit has to start from the initial prompt.
    [Fact]
    public async Task Reset_returns_to_the_initial_prompt()
    {
        _stationList.FetchAsync(PublicHost, Arg.Any<CancellationToken>()).Returns(Stations);
        AddStationViewModel vm = CreateViewModel(hasLocalNetworkPrivacy: false, PublicHost);
        await vm.SearchCommand.ExecuteAsync(null);
        vm.AvailableStations[0].ToggleSelectedCommand.Execute(null);
        Assert.Equal(1, vm.SelectedCount);

        vm.Reset();

        Assert.Equal("", vm.UrlBase);
        Assert.False(vm.HasResults);
        Assert.Empty(vm.AvailableStations);
        Assert.Equal(0, vm.SelectedCount);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public void Toggling_a_station_selects_and_deselects_it()
    {
        var item = new StationSelectionItem(Stations[0], PublicHost, isAlreadyAdded: false);

        item.ToggleSelectedCommand.Execute(null);
        Assert.True(item.IsSelected);

        item.ToggleSelectedCommand.Execute(null);
        Assert.False(item.IsSelected);
    }

    [Fact]
    public void Toggling_an_already_added_station_changes_nothing()
    {
        var item = new StationSelectionItem(Stations[0], PublicHost, isAlreadyAdded: true);

        item.ToggleSelectedCommand.Execute(null);

        Assert.True(item.IsSelected);
    }
}
