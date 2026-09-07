using RadioE45.Models;

namespace RadioE45.Services.Radio;

public interface IPodcastService
{
    Task<List<AzuraCastPodcast>> GetPodcastsAsync(AzuraStation station, CancellationToken ct = default);
    Task<List<AzuraCastPodcastEpisode>> GetEpisodesAsync(AzuraStation station, AzuraCastPodcast podcast, CancellationToken ct = default);
}
