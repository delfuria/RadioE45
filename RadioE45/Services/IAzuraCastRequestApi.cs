using RadioE45.Models;
using Refit;

namespace RadioE45.Services;

public interface IAzuraCastRequestApi
{
    [Get("/{stationId}/requests")]
    Task<List<AzuraCastRequestItem>> GetRequestableSongsAsync(int stationId, CancellationToken ct = default);

    [Post("/{stationId}/request/{requestId}")]
    Task SubmitSongRequestAsync(int stationId, string requestId, CancellationToken ct = default);
}
