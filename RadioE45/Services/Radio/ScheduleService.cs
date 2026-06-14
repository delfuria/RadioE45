using Microsoft.Extensions.Logging;
using RadioE45.Models;
using Refit;

namespace RadioE45.Services.Radio;

public class ScheduleService : IScheduleService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(IHttpClientFactory httpClientFactory, ILogger<ScheduleService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<List<PlaylistSchedule>> GetScheduleAsync(AzuraStation station, CancellationToken ct = default)
    {
        try
        {
            HttpClient client = _httpClientFactory.CreateClient("AzuraCast");
            client.BaseAddress = new Uri($"https://{station.UrlBase}/api/station");

            IAzuraCastScheduleApi api = RestService.For<IAzuraCastScheduleApi>(client);
            List<PlaylistSchedule> items = await api.GetScheduleAsync(station.StationId, ct);

            return Map(items);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API error fetching schedule for station {StationId}", station.StationId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching schedule for station {StationId}", station.StationId);
            throw;
        }
    }

    private static List<PlaylistSchedule> Map(List<PlaylistSchedule> items) =>
        items.Where(s => !s.Title.Contains("jingle", StringComparison.InvariantCultureIgnoreCase)).ToList();
}
