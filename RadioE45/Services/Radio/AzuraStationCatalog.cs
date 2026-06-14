using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;

namespace RadioE45.Services.Radio;

public class AzuraStationCatalog : IAzuraStationCatalog
{
    private readonly IRadioRepository _radioRepository;
    private readonly IStationDetailService _stationDetailService;
    private readonly ILogger<AzuraStationCatalog> _logger;
    private List<AzuraStation> _stations = [];
    private Task? _loadingTask;

    private PeriodicTimer? _offlineTimer;
    private CancellationTokenSource? _offlineTimerCts;

    public event Action? StationsRefreshed;

    public IReadOnlyList<AzuraStation> Stations => _stations.AsReadOnly();

    public AzuraStationCatalog(
        IRadioRepository radioRepository,
        IStationDetailService stationDetailService,
        ILogger<AzuraStationCatalog> logger)
    {
        _radioRepository = radioRepository;
        _stationDetailService = stationDetailService;
        _logger = logger;

        Connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    public Task LoadAsync(CancellationToken ct = default)
    {
        _loadingTask ??= LoadInternalAsync(ct);
        return _loadingTask;
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        _loadingTask = null;
        await LoadAsync(ct);
        StationsRefreshed?.Invoke();
    }

    private async Task LoadInternalAsync(CancellationToken ct)
    {
        List<RadioStation> dbStations = await _radioRepository.GetAllAsync();

        AzuraStation[] stations = await Task.WhenAll(dbStations.Select(async db =>
        {
            AzuraCastStationDetailResponse? detail = await _stationDetailService.FetchAsync(db, ct);
            AzuraStation station = detail is not null ? Map(detail, db) : MapFallback(db);
            _logger.LogInformation("Station loaded: {Name} online={IsOnline}", station.Name, station.IsOnline);
            return station;
        }));

        _stations = [.. stations];
        StartOfflineCheckIfNeeded();
    }

    private async Task RefreshOfflineStationsAsync(CancellationToken ct)
    {
        List<AzuraStation> offline = _stations.Where(s => !s.IsOnline).ToList();
        if (offline.Count == 0) return;

        List<RadioStation> dbStations = await _radioRepository.GetAllAsync();
        bool anyChanged = false;

        await Task.WhenAll(offline.Select(async station =>
        {
            RadioStation? db = dbStations.FirstOrDefault(d => d.Id == station.Id);
            if (db is null) return;

            AzuraCastStationDetailResponse? detail = await _stationDetailService.FetchAsync(db, ct);
            if (detail is null) return;

            station.Name = detail.Name;
            station.ShortName = detail.Shortcode;
            station.Description = detail.Description;
            station.PublicUrl = detail.Url;
            station.StreamUrl = detail.ListenUrl;
            station.StreamUrlFallback = $"https://{db.UrlBase}{db.StreamUrl}";
            station.HlsEnabled = detail.HlsEnabled;
            station.HlsIsDefault = detail.HlsIsDefault;
            station.HlsUrl = detail.HlsUrl;
            station.IsOnline = true;
            anyChanged = true;
            _logger.LogInformation("Station back online: {Name}", station.Name);
        }));

        if (anyChanged)
            StationsRefreshed?.Invoke();
    }

    private void StartOfflineCheckIfNeeded()
    {
        if (_offlineTimer is not null) return;
        if (!_stations.Any(s => !s.IsOnline)) return;

        var cts = new CancellationTokenSource();
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        _offlineTimerCts = cts;
        _offlineTimer = timer;
        _ = RunOfflineCheckLoopAsync(timer, cts.Token);
        _logger.LogInformation("Offline station check timer started");
    }

    private void StopOfflineCheck()
    {
        _offlineTimerCts?.Cancel();
    }

    private async Task RunOfflineCheckLoopAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                await RefreshOfflineStationsAsync(ct);
                if (_stations.All(s => s.IsOnline))
                {
                    _logger.LogInformation("All stations online — stopping offline check timer");
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            timer.Dispose();
            if (ReferenceEquals(_offlineTimer, timer))
            {
                _offlineTimer = null;
                _offlineTimerCts?.Dispose();
                _offlineTimerCts = null;
            }
        }
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            _logger.LogInformation("Connectivity restored — reloading station catalog");
            _ = ReloadAsync();
        }
    }

    private static AzuraStation Map(AzuraCastStationDetailResponse detail, RadioStation db) =>
        new()
        {
            Id = db.Id,
            StationId = db.StationId,
            UrlBase = db.UrlBase,
            LogoUrl = db.LogoUrl,
            IsFavorite = db.IsFavorite,
            SortOrder = db.SortOrder,
            IsTest = db.IsTest,
            WebsocketUrl = db.WebsocketUrl,
            Name = detail.Name,
            ShortName = detail.Shortcode,
            Description = detail.Description,
            PublicUrl = detail.Url,
            //TODO: sostituire con detail.ListenUrl quando l'URL base restituito dall'API sarà corretto
            StreamUrl = detail.ListenUrl,
            StreamUrlFallback = $"https://{db.UrlBase}{db.StreamUrl}",
            HlsEnabled = detail.HlsEnabled,
            HlsIsDefault = detail.HlsIsDefault,
            HlsUrl = detail.HlsUrl,
            IsOnline = true
        };

    private static AzuraStation MapFallback(RadioStation db) =>
        new()
        {
            Id = db.Id,
            StationId = db.StationId,
            UrlBase = db.UrlBase,
            LogoUrl = db.LogoUrl,
            IsFavorite = db.IsFavorite,
            SortOrder = db.SortOrder,
            IsTest = db.IsTest,
            WebsocketUrl = db.WebsocketUrl,
            Name = db.Name,
            ShortName = db.ShortName,
            Description = db.Description,
            StreamUrl = "",
            StreamUrlFallback = $"https://{db.UrlBase}{db.StreamUrl}",
            IsOnline = false
        };

    public AzuraStation? GetFavorite() =>
        _stations.FirstOrDefault(s => s.IsFavorite && s.IsOnline);

    public AzuraStation? GetFirst() =>
        _stations.Where(s => s.IsOnline).MinBy(s => s.SortOrder);

    public async Task SetFavoriteAsync(int dbId, bool isFavorite)
    {
        await _radioRepository.SetFavoriteAsync(dbId, isFavorite);

        // Mirror the DB behavior: setting a favorite clears all others
        if (isFavorite)
        {
            foreach (AzuraStation s in _stations)
                s.IsFavorite = s.Id == dbId;
        }
        else
        {
            AzuraStation? target = _stations.FirstOrDefault(s => s.Id == dbId);
            if (target is not null)
                target.IsFavorite = false;
        }
    }
}
