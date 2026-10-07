using RadioE45.Models;

namespace RadioE45.Services.Data;

public interface ILogRepository
{
    Task InsertAsync(Log log);
    Task TrimToLastAsync(int count);
}
