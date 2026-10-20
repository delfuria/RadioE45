using System.Net;
using System.Text.Json;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Updates;

namespace RadioE45.Core.Tests.Services.Updates;

public class WebVersionUpdateSourceTests
{
    private static readonly Uri AppBase = new("https://delfuria.github.io/RadioE45/");

    [Fact]
    public void The_version_file_gives_the_version_and_a_cache_busted_app_url()
    {
        AppUpdateInfo info = WebVersionUpdateSource.Parse(JsonDocument.Parse("""{"version":"0.47"}"""), AppBase);

        Assert.Equal(new AppVersion(0, 47), info.Version);
        Assert.Equal(AppUpdateAction.OpenUrl, info.Action);
        Assert.Equal("https://delfuria.github.io/RadioE45/?v=0.47", info.Url!.ToString());
    }

    [Theory]
    [InlineData("""{"version":"0.47.3"}""")]
    [InlineData("""{"version":"v0.47"}""")]
    public void Other_version_formats_reduce_to_major_minor(string json) =>
        Assert.Equal(new AppVersion(0, 47), WebVersionUpdateSource.Parse(JsonDocument.Parse(json), AppBase).Version);

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"version":47}""")]
    [InlineData("""{"version":"soon"}""")]
    [InlineData("[]")]
    public void A_missing_or_unreadable_version_is_an_error(string json) =>
        Assert.Throws<FormatException>(() => WebVersionUpdateSource.Parse(JsonDocument.Parse(json), AppBase));

    [Fact]
    public async Task The_request_goes_to_version_json_next_to_the_app()
    {
        Uri? requested = null;
        var handler = new StubHandler(HttpStatusCode.OK, """{"version":"0.47"}""", r => requested = r.RequestUri);
        WebVersionUpdateSource source = Create(handler);

        AppUpdateInfo? info = await source.GetLatestAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new AppVersion(0, 47), info!.Version);
        Assert.Equal("/RadioE45/version.json", requested!.AbsolutePath);
        Assert.StartsWith("?t=", requested.Query);
    }

    [Fact]
    public async Task A_missing_file_is_thrown_for_the_coordinator_to_swallow()
    {
        WebVersionUpdateSource source = Create(new StubHandler(HttpStatusCode.NotFound, "", _ => { }));

        await Assert.ThrowsAsync<HttpRequestException>(() => source.GetLatestAsync(TestContext.Current.CancellationToken));
    }

    private static WebVersionUpdateSource Create(StubHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(AppleLookupUpdateSource.HttpClientName).Returns(_ => new HttpClient(handler, disposeHandler: false));
        return new WebVersionUpdateSource(factory, AppBase);
    }

    private sealed class StubHandler(HttpStatusCode status, string body, Action<HttpRequestMessage> onRequest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onRequest(request);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
