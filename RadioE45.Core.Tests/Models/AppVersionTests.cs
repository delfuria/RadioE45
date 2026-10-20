using RadioE45.Models;

namespace RadioE45.Core.Tests.Models;

// Updates are compared on Major.Minor only: a patch release never prompts.
public class AppVersionTests
{
    [Theory]
    [InlineData("0.46", 0, 46)]
    [InlineData("0.46.6", 0, 46)]
    [InlineData("v0.46.6", 0, 46)]
    [InlineData("V0.46", 0, 46)]
    [InlineData("0.46.0.0", 0, 46)]
    [InlineData("0.30", 0, 30)]
    [InlineData("1", 1, 0)]
    [InlineData("  v1.2.3  ", 1, 2)]
    [InlineData("0.46.6-beta", 0, 46)]
    [InlineData("0.46.6+abc1234", 0, 46)]
    public void TryParse_reads_the_formats_used_by_the_channels(string text, int major, int minor)
    {
        Assert.True(AppVersion.TryParse(text, out AppVersion version));
        Assert.Equal(new AppVersion(major, minor), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("v")]
    [InlineData("0.x")]
    [InlineData("0..6")]
    [InlineData("-1.2")]
    [InlineData("1.2.3.4.5")]
    public void TryParse_rejects_unreadable_text(string? text) =>
        Assert.False(AppVersion.TryParse(text, out _));

    [Fact]
    public void A_patch_difference_is_not_a_difference()
    {
        AppVersion.TryParse("0.46", out AppVersion app);
        AppVersion.TryParse("0.46.6", out AppVersion store);

        Assert.Equal(app, store);
        Assert.False(store > app);
    }

    [Theory]
    [InlineData("0.46.6", "0.47", -1)]
    [InlineData("0.9", "0.46", -1)]
    [InlineData("1.0", "0.99", 1)]
    [InlineData("0.46", "0.46.9", 0)]
    public void CompareTo_orders_by_major_then_minor(string left, string right, int expectedSign)
    {
        AppVersion.TryParse(left, out AppVersion a);
        AppVersion.TryParse(right, out AppVersion b);

        Assert.Equal(expectedSign, Math.Sign(a.CompareTo(b)));
    }
}
