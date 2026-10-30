using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;
using RadioE45.Services.Radio;
using RadioE45.ViewModels;
using InlineDispatcher = RadioE45.Core.Tests.ViewModels.PodcastEpisodesViewModelTabSwitchTests.InlineDispatcher;

namespace RadioE45.Core.Tests.ViewModels;

public class RadioListViewModelFilterTests
{
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly IAzuraStationCatalog _catalog = Substitute.For<IAzuraStationCatalog>();

    private RadioListViewModel Create(params AzuraStation[] stations)
    {
        _catalog.Stations.Returns(stations.ToList());
        var dispatcher = new InlineDispatcher();
        var onAir = new OnAirViewModel(
            Substitute.For<IAudioService>(), Substitute.For<IPodcastPlayerService>(), Substitute.For<IPodcastService>(),
            Substitute.For<INowPlayingService>(), _catalog, Substitute.For<IAppSettingsRepository>(),
            Substitute.For<IScheduleService>(), Substitute.For<INavigationService>(), dispatcher,
            Substitute.For<ISettingsStore>(), Substitute.For<IAppEnvironment>(), NullLogger<OnAirViewModel>.Instance);

        var vm = new RadioListViewModel(_catalog, onAir, Substitute.For<IRadioRepository>(), Substitute.For<IDatabaseService>(),
            Substitute.For<INavigationService>(), Substitute.For<IDialogService>(), dispatcher, _store,
            NullLogger<RadioListViewModel>.Instance);
        vm.Stations = new ObservableCollection<AzuraStation>(stations);
        return vm;
    }

    private static AzuraStation Station(int id, string name, string description = "") =>
        new() { Id = id, Name = name, Description = description, IsOnline = true };

    [Fact]
    public void Without_a_filter_all_the_stations_show()
    {
        RadioListViewModel vm = Create(Station(1, "Radio E45"), Station(2, "Jazz"));

        Assert.Equal(2, vm.FilteredStations.Count);
        Assert.False(vm.HasFilter);
    }

    [Theory]
    [InlineData("jazz")]
    [InlineData("JAZ")]
    [InlineData("  jazz  ")]
    public void Filter_ignores_case_and_surrounding_spaces(string text)
    {
        RadioListViewModel vm = Create(Station(1, "Radio E45"), Station(2, "Jazz"));

        vm.FilterText = text;

        Assert.Equal([2], vm.FilteredStations.Select(s => s.Id));
    }

    [Fact]
    public void Filter_ignores_accents_both_ways()
    {
        RadioListViewModel vm = Create(Station(1, "Città Radio"), Station(2, "Cafe"));

        vm.FilterText = "citta";
        Assert.Equal([1], vm.FilteredStations.Select(s => s.Id));

        vm.FilterText = "café";
        Assert.Equal([2], vm.FilteredStations.Select(s => s.Id));
    }

    [Fact]
    public void Filter_searches_the_name_only_not_the_description()
    {
        RadioListViewModel vm = Create(Station(1, "Alpha", "La radio che collega il territorio"), Station(2, "Beta", "Classical"));

        vm.FilterText = "c";
        Assert.Empty(vm.FilteredStations);

        vm.FilterText = "alp";
        Assert.Equal([1], vm.FilteredStations.Select(s => s.Id));
    }

    [Fact]
    public void ClearFilter_shows_every_station_again()
    {
        RadioListViewModel vm = Create(Station(1, "A"), Station(2, "B"));
        vm.FilterText = "a";

        vm.ClearFilterCommand.Execute(null);

        Assert.Equal("", vm.FilterText);
        Assert.False(vm.HasFilter);
        Assert.Equal(2, vm.FilteredStations.Count);
    }

    [Fact]
    public void A_filter_without_matches_says_so_while_an_empty_list_keeps_its_own_message()
    {
        RadioListViewModel vm = Create(Station(1, "A"));
        string emptyList = vm.EmptyMessage;

        vm.FilterText = "zzz";

        Assert.Empty(vm.FilteredStations);
        Assert.NotEqual(emptyList, vm.EmptyMessage);
    }

    [Fact]
    public void The_filter_follows_a_reloaded_list()
    {
        RadioListViewModel vm = Create(Station(1, "Alpha"), Station(2, "Beta"));
        vm.FilterText = "alpha";

        vm.Stations = new ObservableCollection<AzuraStation>([Station(1, "Alpha"), Station(3, "Alphabet")]);

        Assert.Equal([1, 3], vm.FilteredStations.Select(s => s.Id));
    }

    [Fact]
    public void View_mode_defaults_to_list_and_is_saved_when_changed()
    {
        RadioListViewModel vm = Create(Station(1, "A"));
        Assert.True(vm.IsListMode);

        vm.ShowGridViewCommand.Execute(null);

        Assert.True(vm.IsGridMode);
        Assert.False(vm.IsListMode);
        _store.Received().Set("RadioList.ViewMode", (int)StationListViewMode.Grid);
    }

    [Fact]
    public void View_mode_is_restored_from_the_preferences()
    {
        _store.Get("RadioList.ViewMode", Arg.Any<int>()).Returns((int)StationListViewMode.Grid);

        RadioListViewModel vm = Create(Station(1, "A"));

        Assert.Equal(StationListViewMode.Grid, vm.ViewMode);
    }

    [Fact]
    public async Task Reordering_is_off_with_a_filter_and_in_the_grid()
    {
        AzuraStation first = Station(1, "A"), second = Station(2, "AB");
        RadioListViewModel vm = Create(first, second);
        Assert.True(vm.CanReorder);

        vm.FilterText = "a";
        await vm.MoveStationAsync(first, second);
        Assert.Equal([1, 2], vm.Stations.Select(s => s.Id));
        Assert.False(vm.CanReorder);

        vm.ClearFilterCommand.Execute(null);
        vm.ShowGridViewCommand.Execute(null);
        Assert.False(vm.CanReorder);
    }

    [Fact]
    public async Task Reordering_without_a_filter_moves_the_station_and_keeps_the_filtered_copy_in_step()
    {
        AzuraStation first = Station(1, "A"), second = Station(2, "B");
        RadioListViewModel vm = Create(first, second);

        await vm.MoveStationAsync(first, second);

        Assert.Equal([2, 1], vm.Stations.Select(s => s.Id));
        Assert.Equal([2, 1], vm.FilteredStations.Select(s => s.Id));
    }
}
