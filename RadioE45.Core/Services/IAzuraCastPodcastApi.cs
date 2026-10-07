using RadioE45.Models;
using Refit;

namespace RadioE45.Services;

public interface IAzuraCastPodcastApi
{
    [Get("/{stationId}/public/podcasts")]
    Task<List<AzuraCastPodcast>> GetPodcastsAsync(int stationId, CancellationToken ct = default);

    [Get("/{stationId}/public/podcast/{podcastId}/episodes")]
    Task<List<AzuraCastPodcastEpisode>> GetEpisodesAsync(int stationId, string podcastId, CancellationToken ct = default);
}
