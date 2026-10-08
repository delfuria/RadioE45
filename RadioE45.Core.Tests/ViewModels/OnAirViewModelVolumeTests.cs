using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;
using RadioE45.Services.Radio;
using RadioE45.ViewModels;

namespace RadioE45.Core.Tests.ViewModels;

// Volume/mute rules that used to be #if ANDROID || IOS in the MAUI-only ViewModel and are now
// driven by IAppEnvironment.UsesSystemVolume.
public class OnAirViewModelVolumeTests
{
    private const string VolumeKey = "player_volume";

    private readonly IAudioService _audio = Substitute.For<IAudioService>();
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly IAppEnvironment _environment = Substitute.For<IAppEnvironment>();

    private OnAirViewModel CreateViewModel(bool usesSystemVolume, double storedVolume = 0.6)
    {
        _environment.UsesSystemVolume.Returns(usesSystemVolume);
        _store.Get(VolumeKey, 1.0).Returns(storedVolume);

        return new OnAirViewModel(
            _audio,
            Substitute.For<IPodcastPlayerService>(),
            Substitute.For<IPodcastService>(),
            Substitute.For<INowPlayingService>(),
            Substitute.For<IAzuraStationCatalog>(),
            Substitute.For<IAppSettingsRepository>(),
            Substitute.For<IScheduleService>(),
            Substitute.For<INavigationService>(),
            Substitute.For<IUiDispatcher>(),
            _store,
            _environment,
            NullLogger<OnAirViewModel>.Instance);
    }

    [Fact]
    public void Desktop_starts_from_the_stored_volume()
    {
        OnAirViewModel vm = CreateViewModel(usesSystemVolume: false, storedVolume: 0.6);

        Assert.Equal(0.6, vm.Volume);
    }

    [Fact]
    public void Phone_starts_at_full_scale_ignoring_the_stored_volume()
    {
        OnAirViewModel vm = CreateViewModel(usesSystemVolume: true, storedVolume: 0.6);

        Assert.Equal(1.0, vm.Volume);
    }

    [Fact]
    public void Desktop_unmute_restores_and_persists_the_previous_volume()
    {
        OnAirViewModel vm = CreateViewModel(usesSystemVolume: false, storedVolume: 0.6);

        vm.ToggleMuteCommand.Execute(null);
        Assert.True(vm.IsMuted);
        Assert.Equal(0, vm.Volume);
        _audio.Received().SetVolume(0);

        vm.ToggleMuteCommand.Execute(null);
        Assert.False(vm.IsMuted);
        Assert.Equal(0.6, vm.Volume);
        _audio.Received().SetVolume(0.6);
        _store.Received().Set(VolumeKey, 0.6);
    }

    [Fact]
    public void Phone_unmute_restores_full_scale_without_persisting()
    {
        OnAirViewModel vm = CreateViewModel(usesSystemVolume: true);

        vm.ToggleMuteCommand.Execute(null);
        vm.ToggleMuteCommand.Execute(null);

        Assert.Equal(1.0, vm.Volume);
        _audio.Received().SetVolume(1.0);
        _store.DidNotReceive().Set(VolumeKey, Arg.Any<double>());
    }

    [Fact]
    public void Zero_volume_from_the_slider_is_never_persisted()
    {
        OnAirViewModel vm = CreateViewModel(usesSystemVolume: false);

        vm.SetVolumeCommand.Execute(0.0);

        _store.DidNotReceive().Set(VolumeKey, Arg.Any<double>());
        _audio.Received().SetVolume(0.0);
    }
}
