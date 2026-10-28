using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RadioE45.Models;
using RadioE45.Services.Localization;
using RadioE45.Services.Radio;

namespace RadioE45.Core.Tests.Services.Radio;

public sealed class SongRequestServiceTests
{
    // Expected texts come from the same localization manager the service uses, so the tests do not
    // depend on the UI language and do not touch the shared culture.
    private static LocalizationResourceManager Loc => LocalizationResourceManager.Instance;

    [Theory]
    [InlineData("You must wait 5 minutes before submitting another request.", "Request_Err_TooRecent")]
    [InlineData("This song was already requested and is pending.", "Request_Err_AlreadyPending")]
    [InlineData("This song or artist has been played too recently.", "Request_Err_PlayedRecently")]
    [InlineData("The track is not requestable.", "Request_Err_NotRequestable")]
    [InlineData("You are not permitted to submit requests.", "Request_Err_NotPermitted")]
    [InlineData("Search crawlers are not permitted to use this feature.", "Request_Err_Bot")]
    public async Task SubmitRequest_MapsKnownServerReasonsToLocalizedText(string serverMessage, string resourceKey)
    {
        SongRequestService service = CreateService(HttpStatusCode.InternalServerError, ErrorJson(serverMessage));

        SongRequestException ex = await Assert.ThrowsAsync<SongRequestException>(
            () => service.SubmitRequestAsync(Station(), "abc"));

        string expected = resourceKey == "Request_Err_TooRecent"
            ? Loc.Format(resourceKey, "5")
            : Loc[resourceKey];
        Assert.Equal(expected, ex.Message);
    }

    [Fact]
    public async Task SubmitRequest_CooldownKeepsTheMinutesFromTheServer()
    {
        SongRequestService service = CreateService(
            HttpStatusCode.InternalServerError, ErrorJson("Please wait 2.5 minutes before the next request."));

        SongRequestException ex = await Assert.ThrowsAsync<SongRequestException>(
            () => service.SubmitRequestAsync(Station(), "abc"));

        Assert.Equal(Loc.Format("Request_Err_TooRecent", "2.5"), ex.Message);
    }

    [Fact]
    public async Task SubmitRequest_UnknownServerMessage_UsesTheGenericText()
    {
        SongRequestService service = CreateService(
            HttpStatusCode.InternalServerError, ErrorJson("Something nobody planned for."));

        SongRequestException ex = await Assert.ThrowsAsync<SongRequestException>(
            () => service.SubmitRequestAsync(Station(), "abc"));

        Assert.Equal(Loc["Err_SubmitRequest"], ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("{\"message\": null}")]
    [InlineData("{\"message\": \"   \"}")]
    [InlineData("{\"other\": \"field\"}")]
    public async Task SubmitRequest_MissingOrMalformedBody_UsesTheGenericText(string body)
    {
        SongRequestService service = CreateService(HttpStatusCode.InternalServerError, body);

        SongRequestException ex = await Assert.ThrowsAsync<SongRequestException>(
            () => service.SubmitRequestAsync(Station(), "abc"));

        Assert.Equal(Loc["Err_SubmitRequest"], ex.Message);
    }

    [Fact]
    public async Task SubmitRequest_Success_DoesNotThrowAndPostsToTheRequestRoute()
    {
        StubHandler handler = new(HttpStatusCode.OK, "{}");
        SongRequestService service = CreateService(handler);

        await service.SubmitRequestAsync(Station(stationId: 7), "req-42");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/api/station/7/request/req-42", handler.LastRequest.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SubmitRequest_NetworkFailure_IsNotMappedToAServerMessage()
    {
        StubHandler handler = new(new HttpRequestException("no network"));
        SongRequestService service = CreateService(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.SubmitRequestAsync(Station(), "abc"));
    }

    private static string ErrorJson(string message) =>
        "{\"success\": false, \"message\": \"" + message + "\"}";

    private static AzuraStation Station(int stationId = 1) =>
        new() { StationId = stationId, UrlBase = "radio.example" };

    private static SongRequestService CreateService(HttpStatusCode status, string body) =>
        CreateService(new StubHandler(status, body));

    private static SongRequestService CreateService(StubHandler handler)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AzuraCast").Returns(_ => new HttpClient(handler, disposeHandler: false));
        return new SongRequestService(factory, NullLogger<SongRequestService>.Instance);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly Exception? _failure;

        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public StubHandler(Exception failure)
        {
            _failure = failure;
            _body = "";
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (_failure is not null)
                throw _failure;

            // Refit reads the request back from the response to build its ApiException.
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                RequestMessage = request,
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
