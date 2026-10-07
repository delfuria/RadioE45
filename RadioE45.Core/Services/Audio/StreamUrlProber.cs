using Microsoft.Extensions.Logging;

namespace RadioE45.Services.Audio;

// Extracted from AudioService (was private) so the same probing logic is shared with the
// Android Media3 session callback (RadioPlaybackService.LibraryCallback), instead of being
// duplicated across the cross-platform MediaElement engine and the Android Auto browse tree.
public class StreamUrlProber : IStreamUrlProber
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StreamUrlProber> _logger;

    public StreamUrlProber(IHttpClientFactory httpClientFactory, ILogger<StreamUrlProber> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // Tried strictly in order, one at a time — callers build `urls` as a priority list (e.g. HLS vs.
    // direct Icecast/MP3 depending on AppSettings.PreferHlsStream) and rely on the first REACHABLE
    // candidate winning, not the fastest responder. Racing them all in parallel (the previous
    // implementation) silently ignored that order — whichever URL's server answered quicker won,
    // regardless of position, which is what let HLS keep winning even with the setting off.
    public async Task<string?> ProbeFirstReachableAsync(string[] urls, CancellationToken ct)
    {
        foreach (string url in urls)
        {
            string? result = await ProbeUrlAsync(url, ct);
            if (result is not null)
                return result;
        }

        return null;
    }

    private async Task<string?> ProbeUrlAsync(string url, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            HttpClient client = _httpClientFactory.CreateClient("AzuraCast");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Stream probe OK: {Url}", url);
                return url;
            }

            _logger.LogWarning("Stream probe: HTTP {Status} for {Url}", (int)response.StatusCode, url);
            return null;
        }
        catch (OperationCanceledException)
        {
            if (!ct.IsCancellationRequested)
                _logger.LogWarning("Stream probe timed out: {Url}", url);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stream probe failed for {Url}", url);
            return null;
        }
    }
}
