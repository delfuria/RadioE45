using System.Net;
using System.Text.Json;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Updates;

namespace RadioE45.Core.Tests.Services.Updates;

public class AppleLookupUpdateSourceTests
{
    // Trimmed from a real iTunes Lookup response for the app.
    private const string Found = """
        {"resultCount":1,"results":[{"kind":"software","bundleId":"com.radioe45.app","trackName":"Radio E45",
        "version":"0.46.6","trackViewUrl":"https://apps.apple.com/it/app/radio-e45/id6780274998?uo=4"}]}
        """;

    private const string Empty = """{"resultCount":0,"results":[]}""";

    private static AppUpdateInfo? Parse(string json) => AppleLookupUpdateSource.Parse(JsonDocument.Parse(json));

    [Fact]
    public void A_record_gives_the_version_and_the_store_page()
    {
        AppUpdateInfo? info = Parse(Found);

        Assert.NotNull(info);
        Assert.Equal(new AppVersion(0, 46), info.Version);
        Assert.Equal(AppUpdateAction.OpenUrl, info.Action);
        Assert.Equal("https://apps.apple.com/it/app/radio-e45/id6780274998?uo=4", info.Url!.ToString());
    }

    [Fact]
    public void No_record_gives_null() => Assert.Null(Parse(Empty));

    [Fact]
    public void A_missing_results_property_gives_null() => Assert.Null(Parse("{}"));

    [Fact]
    public void An_unreadable_version_is_an_error() =>
        Assert.Throws<FormatException>(() => Parse("""{"results":[{"version":"soon"}]}"""));

    [Theory]
    [InlineData("""{"results":[{"version":"0.47"}]}""")]
    [InlineData("""{"results":[{"version":"0.47","trackViewUrl":"http://apps.apple.com/x"}]}""")]
    [InlineData("""{"results":[{"version":"0.47","trackViewUrl":"not a url"}]}""")]
    public void A_missing_or_unsafe_store_url_leaves_nothing_to_open(string json)
    {
        AppUpdateInfo? info = Parse(json);

        Assert.NotNull(info);
        Assert.Equal(AppUpdateAction.None, info.Action);
        Assert.Null(info.Url);
    }

    [Theory]
    [InlineData("IT", "it")]
    [InlineData("pl", "pl")]
    [InlineData("US", "us")]
    [InlineData("IV", "it")]
    [InlineData("001", "it")]
    [InlineData("", "it")]
    [InlineData(null, "it")]
    public void The_country_is_the_device_region_with_a_fallback(string? region, string expected) =>
        Assert.Equal(expected, AppleLookupUpdateSource.NormalizeCountry(region));

    [Fact]
    public async Task The_request_asks_for_the_app_bundle_and_reads_the_answer()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Found);
        AppleLookupUpdateSource source = Create(handler);

        AppUpdateInfo? info = await source.GetLatestAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new AppVersion(0, 46), info!.Version);
        Assert.Equal("itunes.apple.com", handler.LastUri!.Host);
        Assert.Contains("bundleId=com.radioe45.app", handler.LastUri.Query);
        Assert.Matches("country=[a-z]{2}$", handler.LastUri.Query);
    }

    [Fact]
    public async Task An_http_error_is_thrown_for_the_coordinator_to_swallow()
    {
        AppleLookupUpdateSource source = Create(new StubHandler(HttpStatusCode.ServiceUnavailable, ""));

        await Assert.ThrowsAsync<HttpRequestException>(() => source.GetLatestAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Malformed_json_is_thrown_for_the_coordinator_to_swallow()
    {
        AppleLookupUpdateSource source = Create(new StubHandler(HttpStatusCode.OK, "<html>"));

        await Assert.ThrowsAnyAsync<JsonException>(() => source.GetLatestAsync(TestContext.Current.CancellationToken));
    }

    private static AppleLookupUpdateSource Create(StubHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(AppleLookupUpdateSource.HttpClientName).Returns(_ => new HttpClient(handler, disposeHandler: false));
        return new AppleLookupUpdateSource(factory);
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
