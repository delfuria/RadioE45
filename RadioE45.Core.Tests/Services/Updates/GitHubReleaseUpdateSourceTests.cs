using System.Net;
using System.Text.Json;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Updates;

namespace RadioE45.Core.Tests.Services.Updates;

public class GitHubReleaseUpdateSourceTests
{
    // Trimmed from the real releases/latest response (tag v0.46.6).
    private const string Release = """
        {"tag_name":"v0.46.6","html_url":"https://github.com/delfuria/RadioE45/releases/tag/v0.46.6",
         "draft":false,"prerelease":false,
         "assets":[{"name":"RadioE45.apk"},{"name":"RadioE45.dmg"},
                   {"name":"radioe45_0.46.6_amd64.deb"},{"name":"radioe45_0.46.6_arm64.deb"},
                   {"name":"RadioE45_Setup_arm64.exe"},{"name":"RadioE45_Setup_x64.exe"}]}
        """;

    private static AppUpdateInfo? Parse(string json, GitHubReleaseChannel channel) =>
        GitHubReleaseUpdateSource.Parse(JsonDocument.Parse(json), channel);

    [Theory]
    [InlineData(GitHubReleaseChannel.LinuxDeb)]
    [InlineData(GitHubReleaseChannel.WindowsInstaller)]
    public void A_release_with_the_channel_file_gives_version_and_release_page(GitHubReleaseChannel channel)
    {
        AppUpdateInfo? info = Parse(Release, channel);

        Assert.NotNull(info);
        Assert.Equal(new AppVersion(0, 46), info.Version);
        Assert.Equal(AppUpdateAction.OpenUrl, info.Action);
        Assert.Equal("https://github.com/delfuria/RadioE45/releases/tag/v0.46.6", info.Url!.ToString());
    }

    [Fact]
    public void A_release_without_the_channel_file_is_not_offered()
    {
        const string onlyDeb = """{"tag_name":"v0.47","html_url":"https://github.com/x/y","assets":[{"name":"radioe45_0.47.0_amd64.deb"}]}""";

        Assert.Null(Parse(onlyDeb, GitHubReleaseChannel.WindowsInstaller));
        Assert.NotNull(Parse(onlyDeb, GitHubReleaseChannel.LinuxDeb));
    }

    [Theory]
    [InlineData("""{"tag_name":"v0.47","draft":true,"assets":[{"name":"a.deb"}]}""")]
    [InlineData("""{"tag_name":"v0.47","prerelease":true,"assets":[{"name":"a.deb"}]}""")]
    [InlineData("""{"tag_name":"v0.47"}""")]
    [InlineData("""[]""")]
    public void Drafts_prereleases_and_releases_without_assets_are_ignored(string json) =>
        Assert.Null(Parse(json, GitHubReleaseChannel.LinuxDeb));

    [Theory]
    [InlineData("v0.46")]
    [InlineData("0.30")]
    [InlineData("V0.47.1")]
    public void The_usual_tag_formats_are_read(string tag)
    {
        string json = $$"""{"tag_name":"{{tag}}","html_url":"https://github.com/x/y","assets":[{"name":"a.deb"}]}""";

        Assert.NotNull(Parse(json, GitHubReleaseChannel.LinuxDeb));
    }

    [Fact]
    public void An_unreadable_tag_is_an_error() =>
        Assert.Throws<FormatException>(() =>
            Parse("""{"tag_name":"nightly","assets":[{"name":"a.deb"}]}""", GitHubReleaseChannel.LinuxDeb));

    [Theory]
    [InlineData("""{"tag_name":"v0.47","html_url":"http://github.com/x","assets":[{"name":"a.deb"}]}""")]
    [InlineData("""{"tag_name":"v0.47","html_url":"https://evil.example/x","assets":[{"name":"a.deb"}]}""")]
    [InlineData("""{"tag_name":"v0.47","assets":[{"name":"a.deb"}]}""")]
    public void Only_a_github_https_page_can_be_opened(string json)
    {
        AppUpdateInfo? info = Parse(json, GitHubReleaseChannel.LinuxDeb);

        Assert.NotNull(info);
        Assert.Equal(AppUpdateAction.None, info.Action);
        Assert.Null(info.Url);
    }

    [Fact]
    public async Task The_request_identifies_the_app_and_reads_the_answer()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, Release);
        GitHubReleaseUpdateSource source = Create(handler);

        AppUpdateInfo? info = await source.GetLatestAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new AppVersion(0, 46), info!.Version);
        Assert.Equal(GitHubReleaseUpdateSource.LatestReleaseUrl, handler.Request!.RequestUri!.ToString());
        Assert.NotEmpty(handler.Request.Headers.UserAgent);
    }

    [Fact]
    public async Task An_http_error_is_thrown_for_the_coordinator_to_swallow()
    {
        GitHubReleaseUpdateSource source = Create(new CapturingHandler(HttpStatusCode.Forbidden, "{}"));

        await Assert.ThrowsAsync<HttpRequestException>(() => source.GetLatestAsync(TestContext.Current.CancellationToken));
    }

    private static GitHubReleaseUpdateSource Create(CapturingHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(AppleLookupUpdateSource.HttpClientName).Returns(_ => new HttpClient(handler, disposeHandler: false));
        return new GitHubReleaseUpdateSource(factory, GitHubReleaseChannel.LinuxDeb);
    }

    private sealed class CapturingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
