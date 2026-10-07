using RadioE45.Models;
using SQLite;

namespace RadioE45.Services.Data;

public class PodcastProgressRepository : IPodcastProgressRepository
{
    private readonly IDatabaseService _db;

    public PodcastProgressRepository(IDatabaseService db)
    {
        _db = db;
    }

    public async Task<PodcastEpisodeProgress?> GetAsync(int stationId, string episodeId)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        return await conn.Table<PodcastEpisodeProgress>()
            .Where(p => p.StationId == stationId && p.EpisodeId == episodeId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<PodcastEpisodeProgress>> GetForPodcastAsync(int stationId, string podcastId)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        return await conn.Table<PodcastEpisodeProgress>()
            .Where(p => p.StationId == stationId && p.PodcastId == podcastId)
            .ToListAsync();
    }

    public async Task SaveAsync(int stationId, int radioStationId, string podcastId, string episodeId, int positionSeconds, bool isCompleted)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        PodcastEpisodeProgress? existing = await GetAsync(stationId, episodeId);

        if (existing is not null)
        {
            existing.RadioStationId = radioStationId;
            existing.PositionSeconds = positionSeconds;
            existing.IsCompleted = isCompleted;
            existing.LastPlayedAt = DateTime.UtcNow;
            await conn.UpdateAsync(existing);
        }
        else
        {
            await conn.InsertAsync(new PodcastEpisodeProgress
            {
                StationId = stationId,
                RadioStationId = radioStationId,
                PodcastId = podcastId,
                EpisodeId = episodeId,
                PositionSeconds = positionSeconds,
                IsCompleted = isCompleted,
                LastPlayedAt = DateTime.UtcNow
            });
        }
    }
}
