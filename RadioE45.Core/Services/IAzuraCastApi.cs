using RadioE45.Models;
using Refit;

namespace RadioE45.Services;

public interface IAzuraCastApi
{
    [Get("/{stationId}?_={cacheBust}")]
    Task<AzuraCastNowPlayingResponse> GetNowPlayingAsync(int stationId, long cacheBust, CancellationToken ct = default);
}
