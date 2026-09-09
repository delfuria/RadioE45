using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using Refit;

namespace RadioE45.Services.Radio;

public class StationDetailService : IStationDetailService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StationDetailService> _logger;

    public StationDetailService(IHttpClientFactory httpClientFactory, ILogger<StationDetailService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<AzuraCastStationDetailResponse?> FetchAsync(RadioStation station, CancellationToken ct = default)
    {
        string baseUrl = UrlBaseHelper.EnsureScheme(station.UrlBase);
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            HttpClient client = _httpClientFactory.CreateClient("AzuraCast");
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);

            IAzuraCastStationApi api = RestService.For<IAzuraCastStationApi>(client);
            AzuraCastStationDetailResponse detail = await api.GetStationDetailAsync(station.StationId, ct);

            _logger.LogInformation("Station detail: {Name} ({Shortcode}), stream: {ListenUrl}",
                detail.Name, detail.Shortcode, detail.ListenUrl);

            return detail;
        }
        catch (Refit.ApiException ex) when ((int)ex.StatusCode == 429)
        {
            _logger.LogWarning("Rate limit (429) per station {StationId} ({BaseUrl})", station.StationId, baseUrl);
            throw new StationRateLimitedException();
        }
        catch (Refit.ApiException ex)
        {
            _logger.LogError(ex, "HTTP error fetching station detail for {StationId} ({BaseUrl}) after {ElapsedMs}ms",
                station.StationId, baseUrl, sw.ElapsedMilliseconds);
            return null;
        }
        // Refit 13's ApiRequestException does NOT derive from ApiException, HttpRequestException
        // or TaskCanceledException — must be caught explicitly or it falls through to the generic
        // handler below and gets logged as "Unexpected error" instead of "Timeout".
        catch (Refit.ApiRequestException ex) when (ex.InnerException is TaskCanceledException or TimeoutException)
        {
            _logger.LogWarning(ex, "Timeout ({TimeoutSeconds}s) fetching station detail for {StationId} ({BaseUrl}) after {ElapsedMs}ms",
                5, station.StationId, baseUrl, sw.ElapsedMilliseconds);
            return null;
        }
        catch (Refit.ApiRequestException ex)
        {
            _logger.LogError(ex, "Request error fetching station detail for {StationId} ({BaseUrl}) after {ElapsedMs}ms",
                station.StationId, baseUrl, sw.ElapsedMilliseconds);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching station detail for {StationId} ({BaseUrl}) after {ElapsedMs}ms",
                station.StationId, baseUrl, sw.ElapsedMilliseconds);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Timeout fetching station detail for {StationId} ({BaseUrl}) after {ElapsedMs}ms",
                station.StationId, baseUrl, sw.ElapsedMilliseconds);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching station detail for {StationId} ({BaseUrl}) after {ElapsedMs}ms",
                station.StationId, baseUrl, sw.ElapsedMilliseconds);
            return null;
        }
    }
}