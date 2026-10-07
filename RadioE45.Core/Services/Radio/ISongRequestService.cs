using RadioE45.Models;

namespace RadioE45.Services.Radio;

public interface ISongRequestService
{
    Task<List<AzuraCastRequestItem>> GetRequestableSongsAsync(AzuraStation station, CancellationToken ct = default);
    Task SubmitRequestAsync(AzuraStation station, string requestId, CancellationToken ct = default);
}
