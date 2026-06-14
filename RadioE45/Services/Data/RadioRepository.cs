using RadioE45.Models;
using SQLite;

namespace RadioE45.Services.Data;

public class RadioRepository : IRadioRepository
{
    private readonly IDatabaseService _db;

    public RadioRepository(IDatabaseService db)
    {
        _db = db;
    }

    public async Task<List<RadioStation>> GetAllAsync()
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
#if DEBUG
        return await conn.Table<RadioStation>().OrderBy(s => s.SortOrder).ToListAsync();
#else
        return await conn.Table<RadioStation>().Where(s => !s.IsTest).OrderBy(s => s.SortOrder).ToListAsync();
#endif
    }

    public async Task<RadioStation?> GetByIdAsync(int id)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        return await conn.Table<RadioStation>().Where(s => s.Id == id).FirstOrDefaultAsync();
    }

    public async Task<int> InsertAsync(RadioStation station)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        return await conn.InsertAsync(station);
    }

    public async Task<int> UpdateAsync(RadioStation station)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        return await conn.UpdateAsync(station);
    }

    public async Task<int> DeleteAsync(int id)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        RadioStation? station = await GetByIdAsync(id);
        if (station is null)
            return 0;
        return await conn.DeleteAsync(station);
    }

    public async Task<RadioStation?> GetFavoriteAsync()
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        return await conn.Table<RadioStation>().Where(s => s.IsFavorite).FirstOrDefaultAsync();
    }

    public async Task SetFavoriteAsync(int stationId, bool isFavorite)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        if (isFavorite)
        {
            // await conn.ExecuteAsync("UPDATE RadioStations SET IsFavorite = 0 WHERE Id != ?", stationId);
            List<RadioStation> others = await conn.Table<RadioStation>().Where(s => s.Id != stationId).ToListAsync();
            others.ForEach(s => s.IsFavorite = false);
            await conn.UpdateAllAsync(others);
        }
        // await conn.ExecuteAsync("UPDATE RadioStations SET IsFavorite = ? WHERE Id = ?", isFavorite ? 1 : 0, stationId);
        RadioStation? target = await conn.Table<RadioStation>().Where(s => s.Id == stationId).FirstOrDefaultAsync();
        if (target is not null)
        {
            target.IsFavorite = isFavorite;
            await conn.UpdateAsync(target);
        }
    }

    public async Task<RadioStation?> GetFirstAsync()
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
#if DEBUG
        return await conn.Table<RadioStation>().OrderBy(s => s.SortOrder).FirstOrDefaultAsync();
#else
        return await conn.Table<RadioStation>().Where(s => !s.IsTest).OrderBy(s => s.SortOrder).FirstOrDefaultAsync();
#endif
    }
}
