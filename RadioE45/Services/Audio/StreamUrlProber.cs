using Microsoft.Extensions.Logging;

namespace RadioE45.Services.Audio;

// Estratto da AudioService (era private) affinché la stessa logica di probing venga
// riusata anche lato Android Media3 (session callback / bridge Fase 3), senza duplicarla.
public class StreamUrlProber : IStreamUrlProber
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StreamUrlProber> _logger;

    public StreamUrlProber(IHttpClientFactory httpClientFactory, ILogger<StreamUrlProber> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string?> ProbeFirstReachableAsync(string[] urls, CancellationToken ct)
    {
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        List<Task<string?>> tasks = urls.Select(url => ProbeUrlAsync(url, probeCts.Token)).ToList();

        while (tasks.Count > 0)
        {
            Task<string?> done = await Task.WhenAny(tasks);
            tasks.Remove(done);

            string? result = await done;
            if (result is not null)
            {
                probeCts.Cancel();
                return result;
            }
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
