using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;
using RadioE45.Services.Radio;
using RadioE45.ViewModels;

namespace RadioE45.Core.Tests.ViewModels;

// The "station not responding" error: it stays while the now-playing poll keeps answering, and goes
// away when the stream plays or the engine withdraws it.
public class OnAirViewModelStreamErrorTests
{
    private readonly IAudioService _audio = Substitute.For<IAudioService>();
    private readonly INowPlayingService _nowPlaying = Substitute.For<INowPlayingService>();

    private OnAirViewModel CreateViewModel()
    {
        IUiDispatcher dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.When(d => d.Post(Arg.Any<Action>())).Do(call => call.Arg<Action>()());

        IAppSettingsRepository settings = Substitute.For<IAppSettingsRepository>();
        settings.GetAsync().Returns(new AppSettings());

        return new OnAirViewModel(
            _audio,
            Substitute.For<IPodcastPlayerService>(),
            Substitute.For<IPodcastService>(),
            _nowPlaying,
            Substitute.For<IAzuraStationCatalog>(),
            settings,
            Substitute.For<IScheduleService>(),
            Substitute.For<INavigationService>(),
            dispatcher,
            Substitute.For<ISettingsStore>(),
            Substitute.For<IAppEnvironment>(),
            NullLogger<OnAirViewModel>.Instance);
    }

    private void RaiseError(string? message) =>
        _audio.ErrorOccurred += Raise.Event<EventHandler<string?>>(_audio, message);

    private void RaiseNowPlaying() =>
        _nowPlaying.NowPlayingUpdated += Raise.Event<EventHandler<NowPlayingInfo>>(_nowPlaying, NowPlayingInfo.Empty);

    [Fact]
    public void Stream_error_is_shown()
    {
        OnAirViewModel vm = CreateViewModel();

        RaiseError("The station is not responding.");

        Assert.Equal("The station is not responding.", vm.ErrorMessage);
    }

    [Fact]
    public void Stream_error_survives_the_now_playing_poll()
    {
        OnAirViewModel vm = CreateViewModel();
        RaiseError("The station is not responding.");

        RaiseNowPlaying();

        Assert.Equal("The station is not responding.", vm.ErrorMessage);
    }

    [Fact]
    public void Stream_error_goes_away_when_the_stream_plays()
    {
        OnAirViewModel vm = CreateViewModel();
        RaiseError("The station is not responding.");

        _audio.PlaybackStateChanged += Raise.Event<EventHandler<bool>>(_audio, true);

        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage));
        RaiseNowPlaying();
        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    [Fact]
    public void Stream_error_goes_away_when_the_engine_withdraws_it()
    {
        OnAirViewModel vm = CreateViewModel();
        RaiseError("The station is not responding.");

        RaiseError(null);
        RaiseNowPlaying();

        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    [Fact]
    public void Now_playing_poll_clears_other_errors_as_before()
    {
        OnAirViewModel vm = CreateViewModel();
        vm.ErrorMessage = "Something else";

        RaiseNowPlaying();

        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage));
    }
}
