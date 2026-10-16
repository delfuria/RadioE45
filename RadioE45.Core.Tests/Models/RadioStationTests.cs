using RadioE45.Models;

namespace RadioE45.Core.Tests.Models;

public class RadioStationTests
{
    [Theory]
    [InlineData("demo.azuracast.com", "demo.azuracast.com")]
    [InlineData("https://demo.azuracast.com", "demo.azuracast.com")]
    [InlineData("http://Demo.AzuraCast.com/", "demo.azuracast.com")]
    [InlineData("  https://radio.example.org:8443/ ", "radio.example.org:8443")]
    public void UrlBaseKey_IgnoresSchemeSlashAndCase(string input, string expected) =>
        Assert.Equal(expected, RadioStation.UrlBaseKey(input));
}
