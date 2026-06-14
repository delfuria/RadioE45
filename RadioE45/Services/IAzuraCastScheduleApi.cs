using RadioE45.Models;
using Refit;

namespace RadioE45.Services;

public interface IAzuraCastScheduleApi
{
    [Get("/{stationId}/schedule")]
    Task<List<PlaylistSchedule>> GetScheduleAsync(int stationId, CancellationToken ct = default);
}
