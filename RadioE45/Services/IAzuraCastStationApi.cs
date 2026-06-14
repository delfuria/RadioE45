using RadioE45.Models;
using Refit;

namespace RadioE45.Services;

public interface IAzuraCastStationApi
{
    [Get("/api/station/{stationId}")]
    Task<AzuraCastStationDetailResponse> GetStationDetailAsync(int stationId, CancellationToken ct = default);
}
