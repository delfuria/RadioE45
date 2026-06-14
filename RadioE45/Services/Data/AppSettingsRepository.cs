using RadioE45.Models;
using SQLite;

namespace RadioE45.Services.Data;

public class AppSettingsRepository : IAppSettingsRepository
{
    private readonly IDatabaseService _db;

    public AppSettingsRepository(IDatabaseService db) => _db = db;

    public async Task<AppSettings> GetAsync()
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        return await conn.Table<AppSettings>().FirstOrDefaultAsync() ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        await conn.InsertOrReplaceAsync(settings);
    }
}
