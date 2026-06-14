using RadioE45.Models;

namespace RadioE45.Services.Radio;

public interface IScheduleService
{
    Task<List<PlaylistSchedule>> GetScheduleAsync(AzuraStation station, CancellationToken ct = default);
}
