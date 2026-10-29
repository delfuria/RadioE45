using RadioE45.ViewModels;

namespace RadioE45.Core.Tests.ViewModels;

// The notices are an embedded copy of the root THIRD-PARTY-NOTICES.txt: the screen must show them,
// and they must still contain the license texts the packages require.
public class OpenSourceLicensesViewModelTests
{
    [Fact]
    public void NoticesText_IsTheEmbeddedThirdPartyNotices()
    {
        OpenSourceLicensesViewModel viewModel = new();

        Assert.Contains("THIRD-PARTY NOTICES - Radio E45", viewModel.NoticesText);
        Assert.Contains("Apache License", viewModel.NoticesText);
        Assert.Contains("SIL Open Font License", viewModel.NoticesText);
        Assert.Contains("GNU LESSER GENERAL PUBLIC LICENSE", viewModel.NoticesText);
    }

    [Fact]
    public void Title_And_Intro_AreLocalized()
    {
        OpenSourceLicensesViewModel viewModel = new();

        Assert.False(string.IsNullOrWhiteSpace(viewModel.Title));
        Assert.False(string.IsNullOrWhiteSpace(viewModel.IntroText));
    }
}
