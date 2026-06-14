using RadioE45.Models;
using SQLite;

namespace RadioE45.Services.Data;

public class DbVersionRepository : IDbVersionRepository
{
    private readonly IDatabaseService _db;

    public DbVersionRepository(IDatabaseService db)
    {
        _db = db;
    }

    public async Task<List<DbVersion>> GetAllAsync()
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        return await conn.Table<DbVersion>().OrderByDescending(v => v.LastDbUpdate).ToListAsync();
    }

    public async Task<DbVersion?> GetCurrentAsync()
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        return await conn.Table<DbVersion>().OrderByDescending(v => v.LastDbUpdate).FirstOrDefaultAsync();
    }

    public async Task<int> InsertAsync(DbVersion version)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        return await conn.InsertAsync(version);
    }

    public async Task<int> UpdateAsync(DbVersion version)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        return await conn.UpdateAsync(version);
    }

    public async Task<int> DeleteAsync(int id)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        DbVersion? version = await conn.Table<DbVersion>().Where(v => v.Id == id).FirstOrDefaultAsync();
        if (version is null)
            return 0;
        return await conn.DeleteAsync(version);
    }
}
