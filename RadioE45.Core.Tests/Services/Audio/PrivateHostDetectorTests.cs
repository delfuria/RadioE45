using RadioE45.Models;
using RadioE45.Services.Audio;

namespace RadioE45.Core.Tests.Services.Audio;

public sealed class PrivateHostDetectorTests
{
    [Theory]
    [InlineData("http://10.0.0.1/radio.mp3")]
    [InlineData("http://10.255.255.255")]
    [InlineData("http://172.16.0.1:8000/radio.mp3")]
    [InlineData("http://172.31.255.255")]
    [InlineData("http://192.168.1.100/listen/radioe45/radio.mp3")]
    [InlineData("192.168.1.100")]
    [InlineData("192.168.1.100:8060")]
    [InlineData("http://169.254.10.20")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://[::1]/radio.mp3")]
    [InlineData("::1")]
    [InlineData("http://[fe80::1]/radio.mp3")]
    [InlineData("http://[febf::1]")]
    [InlineData("http://[fc00::1]")]
    [InlineData("http://[fd12:3456:789a::1]:8000/radio.mp3")]
    [InlineData("http://[::ffff:192.168.1.1]")]
    [InlineData("http://localhost:8000")]
    [InlineData("LOCALHOST")]
    [InlineData("http://myserver/radio.mp3")]
    [InlineData("myserver")]
    [InlineData("https://azuracast.local/radio.mp3")]
    [InlineData("web.azuracast.orb.local")]
    [InlineData("http://radio.lan")]
    [InlineData("http://studio.home.arpa:8000")]
    [InlineData("http://radio.corp.internal")]
    public void Private_hosts_are_detected(string input) =>
        Assert.True(PrivateHostDetector.IsPrivateHost(input));

    [Theory]
    [InlineData("radioe45.ddns.net")]
    [InlineData("https://radioe45.ddns.net:8060/radio.mp3")]
    [InlineData("https://demo.azuracast.com/listen/azuratest_radio/radio.mp3")]
    [InlineData("demo.azuracast.com")]
    [InlineData("http://172.15.255.255")]
    [InlineData("http://172.32.0.1")]
    [InlineData("http://192.169.1.1")]
    [InlineData("http://11.0.0.1")]
    [InlineData("http://8.8.8.8")]
    [InlineData("http://[2001:4860:4860::8888]")]
    [InlineData("http://[fec0::1]")]
    [InlineData("https://radio.local.example.com")]
    public void Public_hosts_are_not_private(string input) =>
        Assert.False(PrivateHostDetector.IsPrivateHost(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://")]
    [InlineData("http://not a url")]
    [InlineData("://")]
    public void Empty_or_malformed_input_is_not_private(string? input) =>
        Assert.False(PrivateHostDetector.IsPrivateHost(input));

    [Theory]
    [InlineData("192.168.1.10", true)]
    [InlineData("http://192.168.1.10", true)]
    [InlineData("radioe45.ddns.net", false)]
    [InlineData("https://demo.azuracast.com", false)]
    public void Station_is_local_when_its_UrlBase_is_private(string urlBase, bool expected) =>
        Assert.Equal(expected, PrivateHostDetector.IsLocalStation(new AzuraStation { UrlBase = urlBase }));

    [Fact]
    public void Station_without_UrlBase_falls_back_to_StreamUrlFallback()
    {
        AzuraStation station = new() { UrlBase = "", StreamUrlFallback = "https://192.168.1.10:8000/radio.mp3" };

        Assert.True(PrivateHostDetector.IsLocalStation(station));
    }

    [Fact]
    public void FilterUrl_drops_private_urls_only_for_public_stations()
    {
        const string LanArt = "http://192.168.1.100/api/station/7/art/abc.jpg";
        const string PublicArt = "https://radioe45.ddns.net/api/station/7/art/abc.jpg";

        AzuraStation publicStation = new() { UrlBase = "radioe45.ddns.net" };
        AzuraStation lanStation = new() { UrlBase = "192.168.1.100" };

        Assert.Null(PrivateHostDetector.FilterUrl(LanArt, publicStation));
        Assert.Equal(PublicArt, PrivateHostDetector.FilterUrl(PublicArt, publicStation));
        Assert.Equal(LanArt, PrivateHostDetector.FilterUrl(LanArt, lanStation));
        Assert.Null(PrivateHostDetector.FilterUrl(null, publicStation));
    }
}
