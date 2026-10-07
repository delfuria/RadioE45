using RadioE45.Models;

namespace RadioE45.Services.Data;

public interface IAppSettingsRepository
{
    Task<AppSettings> GetAsync();
    Task SaveAsync(AppSettings settings);
}
