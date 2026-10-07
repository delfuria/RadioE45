using RadioE45.Models;

namespace RadioE45.Services.Radio;

public interface IStationDetailService
{
    Task<AzuraCastStationDetailResponse?> FetchAsync(RadioStation station, CancellationToken ct = default);
}
