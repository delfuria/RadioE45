using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Localization;

namespace RadioE45.Services.Radio;

public enum CarStationListState
{
    /// <summary>The catalog has not finished its first load: show a placeholder.</summary>
    Loading,
    Ready,
    /// <summary>The catalog loaded and has no stations (or failed to load).</summary>
    Empty,
}

public sealed record CarStationListItem(AzuraStation Station, string Title, string? Subtitle, bool IsCurrent);

/// <summary>
/// What the in-car station list shows (CarPlay), free of UI types: the platform delegate only maps
/// <see cref="Items"/> and <see cref="PlaceholderText"/> to its templates and redraws on
/// <see cref="Changed"/>, which may be raised on any thread.
/// </summary>
public sealed class CarStationList : IDisposable
{
    private readonly IAzuraStationCatalog _catalog;
    private readonly IAudioService _audioService;
    private readonly INowPlayingService _nowPlayingService;
    private readonly ILogger<CarStationList> _logger;
    private IReadOnlyList<CarStationListItem> _items = [];
    private CarStationListState _state = CarStationListState.Loading;
    private bool _isDisposed;

    public CarStationList(IAzuraStationCatalog catalog, IAudioService audioService, INowPlayingService nowPlayingService,
        ILogger<CarStationList> logger)
    {
        _catalog = catalog;
        _audioService = audioService;
        _nowPlayingService = nowPlayingService;
        _logger = logger;

        _catalog.StationsRefreshed += OnStationsRefreshed;
    }

    public event Action? Changed;

    public CarStationListState State => _state;

    public IReadOnlyList<CarStationListItem> Items => _items;

    public string Title => LocalizationResourceManager.Instance["CarPlay_Title"];

    /// <summary>Text for the single placeholder row shown while there is nothing to list.</summary>
    public string PlaceholderText => LocalizationResourceManager.Instance[
        _state == CarStationListState.Loading ? "CarPlay_Loading" : "CarPlay_NoStations"];

    /// <summary>
    /// Waits for the catalog's first load (it shares the load already started by the app, so it is
    /// safe to call at any time, including a cold start from CarPlay) and publishes the result.
    /// </summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        try
        {
            await _catalog.LoadAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Station catalog failed to load for the car station list");
        }

        Rebuild();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _catalog.StationsRefreshed -= OnStationsRefreshed;
    }

    private void OnStationsRefreshed() => Rebuild();

    private void Rebuild()
    {
        if (_isDisposed)
            return;

        int? currentId = _audioService.CurrentStation?.Id;
        string? currentTitle = _nowPlayingService.Current.Title;

        _items =
        [
            .. _catalog.Stations.Select(station =>
            {
                bool isCurrent = currentId == station.Id;
                string? subtitle = isCurrent && !string.IsNullOrEmpty(currentTitle) ? currentTitle : station.Description;
                return new CarStationListItem(station, station.Name, subtitle, isCurrent);
            }),
        ];
        _state = _items.Count > 0 ? CarStationListState.Ready : CarStationListState.Empty;

        Changed?.Invoke();
    }
}
