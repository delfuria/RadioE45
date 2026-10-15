using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;
using RadioE45.Services.Radio;
using RadioE45.ViewModels;

namespace RadioE45.Core.Tests.ViewModels;

// OnAirViewModel.ActivateAsync: what used to be the MAUI OnAppearing code, plus the web rule
// that the radio never starts without a user gesture (IAppEnvironment.CanAutoplay).
public class OnAirViewModelActivateTests
{
    private readonly IAudioService _audio = Substitute.For<IAudioService>();
    private readonly INowPlayingService _nowPlaying = Substitute.For<INowPlayingService>();
    private readonly IAzuraStationCatalog _catalog = Substitute.For<IAzuraStationCatalog>();
    private readonly IAppSettingsRepository _settings = Substitute.For<IAppSettingsRepository>();
    private readonly IAppEnvironment _environment = Substitute.For<IAppEnvironment>();
    private readonly AzuraStation _station = new() { Id = 1, StationId = 1, Name = "E45" };

    private OnAirViewModel CreateViewModel(bool canAutoplay)
    {
        _environment.CanAutoplay.Returns(canAutoplay);
        _settings.GetAsync().Returns(new AppSettings());
        _catalog.GetFirst().Returns(_station);
        _catalog.Stations.Returns([_station]);
        // Like the real service: the playing station is known once PlayAsync is called.
        _audio.When(a => a.PlayAsync(Arg.Any<AzuraStation>()))
            .Do(call => _audio.CurrentStation.Returns(call.Arg<AzuraStation>()));

        return new OnAirViewModel(
            _audio,
            Substitute.For<IPodcastPlayerService>(),
            Substitute.For<IPodcastService>(),
            _nowPlaying,
            _catalog,
            _settings,
            Substitute.For<IScheduleService>(),
            Substitute.For<INavigationService>(),
            Substitute.For<IUiDispatcher>(),
            Substitute.For<ISettingsStore>(),
            _environment,
            NullLogger<OnAirViewModel>.Instance);
    }

    [Fact]
    public async Task First_activation_plays_the_default_station()
    {
        OnAirViewModel vm = CreateViewModel(canAutoplay: true);

        await vm.ActivateAsync();

        Assert.Same(_station, vm.CurrentStation);
        await _audio.Received(1).PlayAsync(_station);
        _audio.Received().SetVolume(vm.Volume);
    }

    [Fact]
    public async Task Without_autoplay_the_default_station_is_selected_but_not_played()
    {
        OnAirViewModel vm = CreateViewModel(canAutoplay: false);

        await vm.ActivateAsync();

        Assert.Same(_station, vm.CurrentStation);
        await _audio.DidNotReceive().PlayAsync(Arg.Any<AzuraStation>());
        await _nowPlaying.Received(1).StartPollingAsync(_station, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Next_activation_restarts_the_station_stopped_by_someone_else()
    {
        OnAirViewModel vm = CreateViewModel(canAutoplay: true);
        await vm.ActivateAsync();
        _audio.CurrentStation.Returns((AzuraStation?)null);   // e.g. a podcast episode stopped it

        await vm.ActivateAsync();

        await _audio.Received(2).PlayAsync(_station);
    }

    [Fact]
    public async Task Next_activation_does_not_restart_a_station_still_streaming()
    {
        OnAirViewModel vm = CreateViewModel(canAutoplay: true);
        await vm.ActivateAsync();

        await vm.ActivateAsync();

        await _audio.Received(1).PlayAsync(_station);
    }

    [Fact]
    public async Task Without_autoplay_next_activation_never_starts_the_radio()
    {
        OnAirViewModel vm = CreateViewModel(canAutoplay: false);
        await vm.ActivateAsync();

        await vm.ActivateAsync();

        await _audio.DidNotReceive().PlayAsync(Arg.Any<AzuraStation>());
    }

    [Fact]
    public async Task Without_autoplay_play_starts_the_selected_station()
    {
        OnAirViewModel vm = CreateViewModel(canAutoplay: false);
        await vm.ActivateAsync();

        await vm.PlayPauseCommand.ExecuteAsync(null);

        await _audio.Received(1).PlayAsync(_station);
    }
}
