using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services;
using Refit;

namespace RadioE45.Services.Radio;

public class StationListService : IStationListService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StationListService> _logger;

    public StationListService(IHttpClientFactory httpClientFactory, ILogger<StationListService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<AzuraCastStationListItem>?> FetchAsync(string urlBase, CancellationToken ct = default)
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient("AzuraCast");
            client.BaseAddress = new Uri($"https://{urlBase}");
            client.Timeout = TimeSpan.FromSeconds(5);

            IAzuraCastStationApi api = RestService.For<IAzuraCastStationApi>(client);
            List<AzuraCastStationListItem> stations = await api.GetStationsAsync(ct);

            _logger.LogInformation("Fetched {Count} stations from {UrlBase}", stations.Count, urlBase);
            return stations;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching station list from {UrlBase}", urlBase);
            return null;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Timeout fetching station list from {UrlBase}", urlBase);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching station list from {UrlBase}", urlBase);
            return null;
        }
    }
}
