using System.Globalization;
using RadioE45.Models;

namespace RadioE45.Core.Tests.Models;

public class AzuraCastPodcastTests
{
    [Theory]
    [InlineData("it", 0, "Many")]
    [InlineData("it", 1, "One")]
    [InlineData("it", 2, "Many")]
    [InlineData("en", 1, "One")]
    [InlineData("en", 5, "Many")]
    [InlineData("pl", 1, "One")]
    [InlineData("pl", 2, "Few")]
    [InlineData("pl", 4, "Few")]
    [InlineData("pl", 5, "Many")]
    [InlineData("pl", 12, "Many")]
    [InlineData("pl", 14, "Many")]
    [InlineData("pl", 22, "Few")]
    [InlineData("pl", 112, "Many")]
    public void PluralForm_FollowsTheLanguageRules(string culture, int count, string expected) =>
        Assert.Equal(expected, AzuraCastPodcast.PluralForm(count, CultureInfo.GetCultureInfo(culture)));
}
