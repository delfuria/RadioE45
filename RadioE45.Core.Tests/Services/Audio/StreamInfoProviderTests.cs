using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Audio;

namespace RadioE45.Core.Tests.Services.Audio;

public class StreamInfoProviderTests
{
    private const string Mp3 = "https://demo.example/listen/radio/radio.mp3";
    private const string Hls = "https://demo.example/hls/radio/live.m3u8";

    private static AzuraStation Station() => new()
    {
        Name = "Demo",
        StreamUrl = Mp3,
        HlsUrl = Hls,
        Mounts =
        [
            new() { Url = "https://demo.example/listen/radio/mobile.mp3", Bitrate = 64, Format = "mp3" },
            new() { Url = Mp3, Bitrate = 128, Format = "mp3", IsDefault = true },
        ],
    };

    private static StreamInfoProvider.HeaderValues Headers(string? contentType, params (string Name, string Value)[] values)
    {
        HttpResponseHeaders headers = new HttpResponseMessage().Headers;
        foreach ((string name, string value) in values)
            headers.TryAddWithoutValidation(name, value);
        return new StreamInfoProvider.HeaderValues(headers, contentType);
    }

    [Theory]
    [InlineData("channels=2;samplerate=44100;bitrate=128")]
    [InlineData("ice-channels=2;ice-samplerate=44100;ice-bitrate=128")]
    [InlineData(" Channels = 2 ; SampleRate=44100 ;bitrate=128;")]
    public void ParseAudioInfo_reads_both_icecast_spellings(string value)
    {
        Dictionary<string, int> info = StreamInfoProvider.ParseAudioInfo(value);

        Assert.Equal(2, info["channels"]);
        Assert.Equal(44100, info["samplerate"]);
        Assert.Equal(128, info["bitrate"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage;x=y;=3")]
    public void ParseAudioInfo_ignores_what_it_does_not_understand(string? value) =>
        Assert.Empty(StreamInfoProvider.ParseAudioInfo(value));

    [Fact]
    public void Build_without_headers_uses_the_mount_that_is_playing()
    {
        AzuraStation station = Station();
        station.OnAirStreamUrl = "https://demo.example/listen/radio/mobile.mp3";

        StreamInfo info = StreamInfoProvider.Build(station, station.OnAirStreamUrl, isHls: false, headerValues: null);

        Assert.Equal("mp3", info.Format);
        Assert.Equal(64, info.BitrateKbps);
        Assert.Null(info.SampleRateHz);
        Assert.Null(info.Channels);
        Assert.Null(info.Genre);
    }

    [Fact]
    public void Build_falls_back_to_the_default_mount_when_the_url_matches_none()
    {
        StreamInfo info = StreamInfoProvider.Build(Station(), "http://192.168.1.2:8000/radio.mp3", isHls: false, headerValues: null);

        Assert.Equal(128, info.BitrateKbps);
    }

    [Fact]
    public void Build_completes_the_mount_with_the_stream_headers()
    {
        StreamInfo info = StreamInfoProvider.Build(Station(), Mp3, isHls: false, Headers(
            "audio/mpeg",
            ("ice-audio-info", "channels=2;samplerate=44100;bitrate=192"),
            ("icy-genre", " Ambient Chill ")));

        Assert.Equal("mp3", info.Format);
        Assert.Equal(192, info.BitrateKbps); // the actual bitrate wins over the configured one
        Assert.Equal(44100, info.SampleRateHz);
        Assert.Equal(2, info.Channels);
        Assert.Equal("Ambient Chill", info.Genre);
    }

    [Fact]
    public void Build_takes_the_format_from_the_content_type_when_there_is_no_mount()
    {
        var station = new AzuraStation { Name = "Plain", StreamUrl = Mp3 };

        StreamInfo info = StreamInfoProvider.Build(station, Mp3, isHls: false,
            Headers("audio/aacp", ("icy-br", "96,96")));

        Assert.Equal("aac", info.Format);
        Assert.Equal(96, info.BitrateKbps);
    }

    [Fact]
    public void Build_of_an_hls_stream_only_knows_the_format()
    {
        StreamInfo info = StreamInfoProvider.Build(Station(), Hls, isHls: true, headerValues: null);

        Assert.Equal("HLS", info.Format);
        Assert.Null(info.BitrateKbps);
    }

    [Fact]
    public void PlayedUrl_prefers_what_the_player_opened()
    {
        AzuraStation station = Station();
        Assert.Equal(Mp3, StreamInfoProvider.PlayedUrl(station));

        station.OnAirStreamUrl = Hls;
        Assert.Equal(Hls, StreamInfoProvider.PlayedUrl(station));
        Assert.True(StreamInfoProvider.IsHls(station, Hls));
    }

    [Fact]
    public async Task GetAsync_reads_the_headers_of_the_stream()
    {
        var handler = new FakeHandler(request =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
            response.Headers.TryAddWithoutValidation("ice-audio-info", "channels=1;samplerate=22050;bitrate=64");
            return response;
        });
        StreamInfoProvider provider = Create(handler);

        StreamInfo info = await provider.GetAsync(Station(), TestContext.Current.CancellationToken);

        Assert.Equal(Mp3, handler.RequestedUrl);
        Assert.Equal(22050, info.SampleRateHz);
        Assert.Equal(1, info.Channels);
        Assert.Equal(64, info.BitrateKbps);
    }

    [Fact]
    public async Task GetAsync_survives_an_unreachable_stream()
    {
        StreamInfoProvider provider = Create(new FakeHandler(_ => throw new HttpRequestException("down")));

        StreamInfo info = await provider.GetAsync(Station(), TestContext.Current.CancellationToken);

        Assert.Equal("Demo", info.StationName);
        Assert.Equal(128, info.BitrateKbps); // still what the API says
        Assert.Null(info.SampleRateHz);
    }

    [Fact]
    public async Task GetAsync_does_not_open_an_hls_stream()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        AzuraStation station = Station();
        station.OnAirStreamUrl = Hls;

        StreamInfo info = await Create(handler).GetAsync(station, TestContext.Current.CancellationToken);

        Assert.Null(handler.RequestedUrl);
        Assert.Equal("HLS", info.Format);
    }

    private static StreamInfoProvider Create(FakeHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AzuraCast").Returns(_ => new HttpClient(handler, disposeHandler: false));
        return new StreamInfoProvider(factory, NullLogger<StreamInfoProvider>.Instance);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public string? RequestedUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrl = request.RequestUri?.ToString();
            return Task.FromResult(respond(request));
        }
    }
}
