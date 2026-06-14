using RadioE45.Models;

namespace RadioE45.Services.Data;

public interface IRadioRepository
{
    Task<List<RadioStation>> GetAllAsync();
    Task<RadioStation?> GetByIdAsync(int id);
    Task<int> InsertAsync(RadioStation station);
    Task<int> UpdateAsync(RadioStation station);
    Task<int> DeleteAsync(int id);
    Task<RadioStation?> GetFirstAsync();
    Task<RadioStation?> GetFavoriteAsync();
    Task SetFavoriteAsync(int stationId, bool isFavorite);
}
