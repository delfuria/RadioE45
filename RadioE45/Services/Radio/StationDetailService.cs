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
        try
        {
            string baseUrl = $"https://{station.UrlBase}";
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
            _logger.LogWarning("Rate limit (429) per station {StationId}", station.StationId);
            throw new StationRateLimitedException();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching station detail for {StationId}", station.StationId);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Timeout fetching station detail for {StationId}", station.StationId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching station detail for {StationId}", station.StationId);
            return null;
        }
    }
}