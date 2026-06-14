using RadioE45.Models;
using SQLite;

namespace RadioE45.Services.Data;

public class LogRepository : ILogRepository
{
    private readonly IDatabaseService _db;

    public LogRepository(IDatabaseService db) => _db = db;

    public async Task InsertAsync(Log log)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(log);
    }

    public async Task TrimToLastAsync(int count)
    {
        SQLiteAsyncConnection conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync(
            "DELETE FROM Logs WHERE Id NOT IN (SELECT Id FROM Logs ORDER BY Id DESC LIMIT ?)",
            count);
    }
}
