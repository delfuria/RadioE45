using SQLite;

namespace RadioE45.Services.Data;

public interface IDatabaseService
{
    Task<SQLiteAsyncConnection> GetConnectionAsync();
    Task ResetToDefaultsAsync();
    Task SeedStationsAsync();
}
