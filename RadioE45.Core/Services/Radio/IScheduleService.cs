using RadioE45.Models;

namespace RadioE45.Services.Radio;

public interface IScheduleService
{
    /// <summary>
    /// Palinsesto appiattito: lo streamer prevale e le playlist sono ritagliate attorno a lui.
    /// <paramref name="minPlaylistDuration"/> scarta i frammenti di playlist più brevi (null = nessun filtro).
    /// </summary>
    Task<List<PlaylistSchedule>> GetScheduleAsync(AzuraStation station, TimeSpan? minPlaylistDuration = null, CancellationToken ct = default);
}
