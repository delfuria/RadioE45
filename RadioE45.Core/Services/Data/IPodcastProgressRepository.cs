using RadioE45.Models;

namespace RadioE45.Services.Data;

public interface IPodcastProgressRepository
{
    Task<PodcastEpisodeProgress?> GetAsync(int stationId, string episodeId);
    Task<List<PodcastEpisodeProgress>> GetForPodcastAsync(int stationId, string podcastId);
    Task SaveAsync(int stationId, int radioStationId, string podcastId, string episodeId, int positionSeconds, bool isCompleted);
}
