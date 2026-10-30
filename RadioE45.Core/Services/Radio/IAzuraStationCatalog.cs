using RadioE45.Models;

namespace RadioE45.Services.Radio;

public interface IAzuraStationCatalog
{
    IReadOnlyList<AzuraStation> Stations { get; }
    DateTimeOffset LastLoadedAt { get; }
    event Action StationsRefreshed;

    /// <summary>Raised when the list is edited in place (remove, reorder); a reload raises <see cref="StationsRefreshed"/>.</summary>
    event Action StationListChanged;
    Task LoadAsync(CancellationToken ct = default);
    Task ReloadAsync(CancellationToken ct = default);
    void RemoveStation(int id);
    Task ReorderAsync(IReadOnlyList<int> orderedIds);
    Task SetFavoriteAsync(int dbId, bool isFavorite);
    AzuraStation? GetFavorite();
    AzuraStation? GetFirst();
}
