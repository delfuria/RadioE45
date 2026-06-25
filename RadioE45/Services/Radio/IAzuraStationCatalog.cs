using RadioE45.Models;

namespace RadioE45.Services.Radio;

public interface IAzuraStationCatalog
{
    IReadOnlyList<AzuraStation> Stations { get; }
    event Action StationsRefreshed;
    Task LoadAsync(CancellationToken ct = default);
    Task ReloadAsync(CancellationToken ct = default);
    void RemoveStation(int id);
    Task SetFavoriteAsync(int dbId, bool isFavorite);
    AzuraStation? GetFavorite();
    AzuraStation? GetFirst();
}
