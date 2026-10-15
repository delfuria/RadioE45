using NSubstitute;
using RadioE45.Services.Platform;

namespace RadioE45.Core.Tests.Services.Platform;

/// <summary>Same sizes the MAUI desktop heads used before they moved to Core (v0.44).</summary>
public class DesktopWindowSizeTests
{
    [Theory]
    [InlineData("Portrait", 500, 855)]
    [InlineData("Landscape", 900, 450)]
    [InlineData(null, 500, 855)]
    [InlineData("", 500, 855)]
    [InlineData("landscape", 500, 855)] // Enum.TryParse is case-sensitive, as in the old MAUI code
    [InlineData("garbage", 500, 855)]
    public void Size_for_stored_orientation(string? orientation, double width, double height)
    {
        Assert.Equal((width, height), DesktopWindowSize.For(orientation));
    }

    [Fact]
    public void Size_from_settings_defaults_to_portrait()
    {
        var settings = Substitute.For<ISettingsStore>();
        settings.Get(DesktopWindowSize.OrientationKey, "Portrait").Returns("Portrait");

        Assert.Equal((500d, 855d), DesktopWindowSize.For(settings));
        settings.Received().Get(DesktopWindowSize.OrientationKey, "Portrait");
    }
}
