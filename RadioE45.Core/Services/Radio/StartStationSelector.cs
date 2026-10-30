using RadioE45.Models;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Radio;

/// <summary>Picks the station to load at start, and remembers the one played last.</summary>
public static class StartStationSelector
{
    public const string LastStationKey = "last_station_id";

    public static void RememberLast(ISettingsStore store, int stationId) => store.Set(LastStationKey, stationId);

    public static AzuraStation? Select(IAzuraStationCatalog catalog, ISettingsStore store, StartStationMode mode)
    {
        AzuraStation? station = mode switch
        {
            StartStationMode.Favorite => catalog.GetFavorite(),
            StartStationMode.Last => FindLast(catalog, store),
            _ => null,
        };

        return station ?? catalog.GetFirst();
    }

    private static AzuraStation? FindLast(IAzuraStationCatalog catalog, ISettingsStore store)
    {
        int id = store.Get(LastStationKey, 0);
        return id == 0 ? null : catalog.Stations.FirstOrDefault(s => s.Id == id && s.IsOnline);
    }
}
