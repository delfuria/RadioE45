using RadioE45.Models;

namespace RadioE45.Services.Data;

public interface IDbVersionRepository
{
    Task<List<DbVersion>> GetAllAsync();
    Task<DbVersion?> GetCurrentAsync();
    Task<int> InsertAsync(DbVersion version);
    Task<int> UpdateAsync(DbVersion version);
    Task<int> DeleteAsync(int id);
}
