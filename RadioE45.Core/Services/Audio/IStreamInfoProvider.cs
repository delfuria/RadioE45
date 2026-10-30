using RadioE45.Models;

namespace RadioE45.Services.Audio;

public interface IStreamInfoProvider
{
    /// <summary>
    /// Describes the stream the station is playing (or would play): the data of the AzuraCast mount
    /// first, completed with the headers the stream server answers with. Never throws on network
    /// errors: missing values are left null.
    /// </summary>
    Task<StreamInfo> GetAsync(AzuraStation station, CancellationToken ct = default);
}
