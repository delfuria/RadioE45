using RadioE45.Models;

namespace RadioE45.Services.Radio;

public interface IStationListService
{
    Task<List<AzuraCastStationListItem>?> FetchAsync(string urlBase, CancellationToken ct = default);
}
