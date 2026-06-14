using RadioE45.Models;

namespace RadioE45.Services.Radio;

public interface INowPlayingService
{
    NowPlayingInfo Current { get; }

    event EventHandler<NowPlayingInfo> NowPlayingUpdated;

    Task StartPollingAsync(AzuraStation station, CancellationToken ct = default);
    Task StopPollingAsync();
    Task<NowPlayingInfo> FetchOnceAsync(AzuraStation station);
    void PausePolling();
    void ResumePolling();
}
