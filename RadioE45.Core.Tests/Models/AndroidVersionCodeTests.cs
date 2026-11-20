using RadioE45.Models;
using Xunit;

namespace RadioE45.Core.Tests.Models;

public class AndroidVersionCodeTests
{
    [Theory]
    [InlineData(3538947, 54)] // 0.54.3
    [InlineData(3407873, 52)] // 0.52.1
    [InlineData(3080193, 47)] // 0.47.1
    public void MinorIsTheHighBits(int versionCode, int minor) =>
        Assert.Equal(new AppVersion(0, minor), AndroidVersionCode.ToAppVersion(versionCode, 0));

    [Fact]
    public void PatchOnlyReleaseDoesNotChangeTheVersion() =>
        Assert.Equal(AndroidVersionCode.ToAppVersion(3538945, 0), AndroidVersionCode.ToAppVersion(3538950, 0));
}
