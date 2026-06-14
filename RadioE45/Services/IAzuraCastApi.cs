using RadioE45.Models;
using Refit;

namespace RadioE45.Services;

public interface IAzuraCastApi
{
    [Get("/{stationId}")]
    Task<AzuraCastNowPlayingResponse> GetNowPlayingAsync(int stationId, CancellationToken ct = default);
}
