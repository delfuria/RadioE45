using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Platform;

namespace RadioE45.Core.Tests.Services.Audio;

public sealed class LiveStreamAudioServiceTests : IDisposable
{
    private const string Mp3 = "https://radio.example/radio.mp3";
    private const string Fallback = "https://radio.example:8000/radio.mp3";
    private const string Hls = "https://radio.example/hls/live.m3u8";

    private readonly IStreamUrlProber _prober = Substitute.For<IStreamUrlProber>();
    private readonly IPlatformNowPlayingService _nowPlaying = Substitute.For<IPlatformNowPlayingService>();
    private readonly IAudioFocusManager _focus = Substitute.For<IAudioFocusManager>();
    private readonly IAppSettingsRepository _settings = Substitute.For<IAppSettingsRepository>();
    private readonly FakeNetworkMonitor _network = new();
    private readonly FakeStreamPlayer _player = new();
    private readonly LiveStreamAudioService _service;

    public LiveStreamAudioServiceTests()
    {
        _focus.RequestFocus().Returns(true);
        _settings.GetAsync().Returns(new AppSettings { PreferHlsStream = false });
        // Reachability: first candidate wins, as when every URL answers.
        _prober.ProbeFirstReachableAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<string[]>()[0]);

        _service = new LiveStreamAudioService(
            _prober, _nowPlaying, _focus, _settings, _network, new InlineDispatcher(), NullLogger.Instance);
        _service.AttachPlayer(_player);
    }

    public void Dispose() => _service.Shutdown();

    private static AzuraStation Station(bool withHls = true) => new()
    {
        Id = 1,
        Name = "E45",
        StreamUrl = Mp3,
        StreamUrlFallback = Fallback,
        HlsUrl = withHls ? Hls : null,
    };

    [Fact]
    public void Candidates_prefer_direct_stream_unless_hls_is_preferred()
    {
        AzuraStation station = Station();

        Assert.Equal([Mp3, Fallback, Hls], LiveStreamAudioService.GetCandidateUrls(station, preferHls: false));
        Assert.Equal([Hls, Mp3, Fallback], LiveStreamAudioService.GetCandidateUrls(station, preferHls: true));
    }

    [Fact]
    public void Last_working_url_is_tried_first_and_not_repeated()
    {
        AzuraStation station = Station();
        station.OnAirStreamUrl = Fallback;

        Assert.Equal([Fallback, Mp3, Hls], LiveStreamAudioService.GetCandidateUrls(station, preferHls: false));
    }

    [Fact]
    public async Task Play_opens_the_first_reachable_url_and_raises_StreamOpened()
    {
        AzuraStation station = Station();
        AzuraStation? opened = null;
        _service.StreamOpened += (_, s) => opened = s;

        await _service.PlayAsync(station);

        Assert.Equal([Mp3], _player.OpenedUrls);
        Assert.Same(station, opened);
        Assert.Same(station, _service.CurrentStation);
        Assert.False(_service.IsHlsActive);
        Assert.Equal(Mp3, station.OnAirStreamUrl);
    }

    [Fact]
    public async Task IsHlsActive_reflects_the_url_actually_opened()
    {
        _prober.ProbeFirstReachableAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(Hls);

        await _service.PlayAsync(Station());

        Assert.True(_service.IsHlsActive);
    }

    [Fact]
    public async Task Nothing_is_opened_when_all_urls_are_unreachable()
    {
        _prober.ProbeFirstReachableAsync(Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns((string?)null);

        await _service.PlayAsync(Station());

        Assert.Empty(_player.OpenedUrls);
    }

    [Fact]
    public async Task Play_is_blocked_without_audio_focus()
    {
        _focus.RequestFocus().Returns(false);

        await _service.PlayAsync(Station());

        Assert.Empty(_player.OpenedUrls);
        Assert.Null(_service.CurrentStation);
    }

    // Live rule: pause closes the stream (no buffered, out-of-date audio) but keeps the station.
    [Fact]
    public async Task Pause_closes_the_stream_but_keeps_the_station()
    {
        AzuraStation station = Station();
        await _service.PlayAsync(station);

        await _service.PauseAsync();

        Assert.Equal(1, _player.CloseCount);
        Assert.False(_player.HasSource);
        Assert.Same(station, _service.CurrentStation);
        _nowPlaying.Received().UpdatePlaybackState(false);
        _focus.DidNotReceive().AbandonFocus();
    }

    [Fact]
    public async Task Resume_reopens_the_live_stream_instead_of_continuing_a_buffer()
    {
        await _service.PlayAsync(Station());
        await _service.PauseAsync();

        await _service.ResumeAsync();

        Assert.Equal([Mp3, Mp3], _player.OpenedUrls);
    }

    [Fact]
    public async Task Stop_closes_clears_the_station_and_releases_focus()
    {
        await _service.PlayAsync(Station());

        await _service.StopAsync();

        Assert.Null(_service.CurrentStation);
        Assert.False(_player.HasSource);
        Assert.Equal(1, _player.ClearMetadataCount);
        _focus.Received().AbandonFocus();
        _nowPlaying.Received().Clear();
    }

    [Fact]
    public async Task Resume_after_stop_does_nothing()
    {
        await _service.PlayAsync(Station());
        await _service.StopAsync();

        await _service.ResumeAsync();

        Assert.Single(_player.OpenedUrls);
    }

    [Fact]
    public async Task Media_failure_while_playing_reconnects_silently()
    {
        string? error = null;
        _service.ErrorOccurred += (_, e) => error = e;
        await _service.PlayAsync(Station());

        _player.RaiseFailed("network down");

        Assert.Equal(2, _player.OpenedUrls.Count);
        Assert.Null(error);
    }

    [Fact]
    public async Task Media_failure_after_pause_is_reported_without_reconnecting()
    {
        string? error = null;
        _service.ErrorOccurred += (_, e) => error = e;
        await _service.PlayAsync(Station());
        await _service.PauseAsync();

        _player.RaiseFailed("decoder error");

        Assert.Single(_player.OpenedUrls);
        Assert.Equal("decoder error", error);
    }

    [Fact]
    public async Task Stream_end_while_playing_reconnects()
    {
        await _service.PlayAsync(Station());

        _player.RaiseEnded();

        Assert.Equal(2, _player.OpenedUrls.Count);
    }

    [Fact]
    public async Task Connectivity_restored_while_playing_reconnects()
    {
        await _service.PlayAsync(Station());

        _network.Raise(hasInternet: false);
        Assert.Single(_player.OpenedUrls);

        _network.Raise(hasInternet: true);
        Assert.Equal(2, _player.OpenedUrls.Count);
    }

    [Fact]
    public async Task Connectivity_restored_while_paused_does_not_reconnect()
    {
        await _service.PlayAsync(Station());
        await _service.PauseAsync();

        _network.Raise(hasInternet: true);

        Assert.Single(_player.OpenedUrls);
    }

    [Fact]
    public async Task Watchdog_reconnects_a_stale_player()
    {
        await _service.PlayAsync(Station());
        _player.RaiseState(StreamPlayerState.Playing, StreamPlayerState.Stopped);

        _service.CheckPlaybackHealth();

        Assert.Equal(2, _player.OpenedUrls.Count);
    }

    [Fact]
    public async Task Watchdog_leaves_a_healthy_player_alone()
    {
        await _service.PlayAsync(Station());
        _player.RaiseState(StreamPlayerState.Opening, StreamPlayerState.Playing);

        _service.CheckPlaybackHealth();

        Assert.Single(_player.OpenedUrls);
    }

    [Fact]
    public async Task Player_state_drives_IsPlaying_and_IsBuffering()
    {
        var playing = new List<bool>();
        _service.PlaybackStateChanged += (_, p) => playing.Add(p);
        await _service.PlayAsync(Station());

        _player.RaiseState(StreamPlayerState.None, StreamPlayerState.Buffering);
        Assert.True(_service.IsBuffering);
        Assert.False(_service.IsPlaying);

        _player.RaiseState(StreamPlayerState.Buffering, StreamPlayerState.Playing);
        Assert.False(_service.IsBuffering);
        Assert.True(_service.IsPlaying);

        Assert.Equal([false, true], playing);
    }

    [Fact]
    public async Task Attaching_a_new_player_while_playing_moves_the_stream_to_it()
    {
        await _service.PlayAsync(Station());
        var replacement = new FakeStreamPlayer();

        _service.AttachPlayer(replacement);

        Assert.Equal(1, _player.CloseCount);
        Assert.Equal([Mp3], replacement.OpenedUrls);
    }

    [Fact]
    public async Task Volume_is_clamped()
    {
        await _service.PlayAsync(Station());

        _service.SetVolume(1.7);

        Assert.Equal(1.0, _player.Volume);
        _focus.Received().NotifyVolumeChanged(1.0);
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

    private sealed class FakeNetworkMonitor : INetworkMonitor
    {
        public bool HasInternet { get; private set; } = true;

        public event EventHandler<bool>? ConnectivityChanged;

        public void Raise(bool hasInternet)
        {
            HasInternet = hasInternet;
            ConnectivityChanged?.Invoke(this, hasInternet);
        }
    }

    private sealed class FakeStreamPlayer : IStreamPlayer
    {
        public List<string> OpenedUrls { get; } = [];
        public int CloseCount { get; private set; }
        public int ClearMetadataCount { get; private set; }
        public double Volume { get; private set; } = 1.0;
        public bool HasSource { get; private set; }

        public event EventHandler<StreamPlayerStateChangedEventArgs>? StateChanged;
        public event EventHandler<string?>? Failed;
        public event EventHandler? Ended;

        public void Open(string url)
        {
            OpenedUrls.Add(url);
            HasSource = true;
        }

        public void Close()
        {
            CloseCount++;
            HasSource = false;
        }

        public void SetVolume(double volume) => Volume = volume;

        public void SetMetadata(string artist, string title, string? artworkUrl)
        {
        }

        public void ClearMetadata() => ClearMetadataCount++;

        public void RaiseState(StreamPlayerState previous, StreamPlayerState current) =>
            StateChanged?.Invoke(this, new StreamPlayerStateChangedEventArgs(previous, current));

        public void RaiseFailed(string message) => Failed?.Invoke(this, message);

        public void RaiseEnded() => Ended?.Invoke(this, EventArgs.Empty);
    }
}
