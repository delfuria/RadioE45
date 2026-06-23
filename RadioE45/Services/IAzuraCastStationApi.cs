using RadioE45.Models;
using Refit;

namespace RadioE45.Services;

public interface IAzuraCastStationApi
{
    [Get("/api/station/{stationId}")]
    Task<AzuraCastStationDetailResponse> GetStationDetailAsync(int stationId, CancellationToken ct = default);

    [Get("/api/stations")]
    Task<List<AzuraCastStationListItem>> GetStationsAsync(CancellationToken ct = default);
}
