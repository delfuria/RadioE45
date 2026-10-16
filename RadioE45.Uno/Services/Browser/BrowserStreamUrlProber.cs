#if RADIOE45_BROWSER
using RadioE45.Services.Audio;

namespace RadioE45.Services.Browser;

/// <summary>
/// <see cref="IStreamUrlProber"/> for the web. The probe is a fetch, subject to CORS and to the
/// browser's network rules, while the audio element that plays the stream is not: Safari rejects
/// the fetch of the direct Icecast port of Radio E45 ("TypeError: Load failed") but plays it.
/// Same order as the Core <see cref="StreamUrlProber"/> (first URL that answers with a success
/// status wins); when none does, the first URL that failed only at the fetch level (no HTTP
/// status, no timeout) is returned, so the audio element gets to try it.
/// </summary>
public sealed class BrowserStreamUrlProber : IStreamUrlProber
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BrowserStreamUrlProber> _logger;

    public BrowserStreamUrlProber(IHttpClientFactory httpClientFactory, ILogger<BrowserStreamUrlProber> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string?> ProbeFirstReachableAsync(string[] urls, CancellationToken ct)
    {
        string? fetchBlocked = null;

        foreach (var url in urls)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(3));

                var client = _httpClientFactory.CreateClient("AzuraCast");
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

                if (response.IsSuccessStatusCode)
                    return url;

                _logger.LogWarning("Stream probe: HTTP {Status} for {Url}", (int)response.StatusCode, url);
            }
            catch (OperationCanceledException)
            {
                if (ct.IsCancellationRequested)
                    return null;
                _logger.LogWarning("Stream probe timed out: {Url}", url);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is null)
            {
                _logger.LogInformation("Stream probe blocked by the browser for {Url} ({Message}): left to the audio element", url, ex.Message);
                fetchBlocked ??= url;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stream probe failed for {Url}", url);
            }
        }

        return fetchBlocked;
    }
}
#endif
