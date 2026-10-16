using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Audio;
using RadioE45.Services.Data;
using RadioE45.Services.Localization;
using RadioE45.Services.Radio;
using RadioE45.Services.Platform;

namespace RadioE45.ViewModels;

public partial class AddStationViewModel : BaseViewModel
{
    private readonly INavigationService _navigation;
    private readonly IStationListService _stationListService;
    private readonly IRadioRepository _radioRepository;
    private readonly IAzuraStationCatalog _catalog;
    private readonly IAppEnvironment _environment;

    // Apple Local Network alert: the first request to a LAN host can fail while the alert is still
    // on screen (TN3179), so a private address is retried until the user answers — bounded by
    // attempts and total time, so a LAN server that is really down still reports the error.
    internal const int LocalNetworkMaxAttempts = 10;
    internal static readonly TimeSpan LocalNetworkRetryDelay = TimeSpan.FromSeconds(2);
    internal static readonly TimeSpan LocalNetworkRetryBudget = TimeSpan.FromSeconds(30);

    // Injectable for tests, so retries don't wait for real.
    internal Func<TimeSpan, Task> DelayAsync { get; set; } = delay => Task.Delay(delay);

    [ObservableProperty]
    public partial string UrlBase { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BottomButtonText))]
    [NotifyPropertyChangedFor(nameof(HasSelections))]
    public partial int SelectedCount { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<StationSelectionItem> AvailableStations { get; set; } = [];

    [ObservableProperty]
    public partial bool HasResults { get; set; }

    public bool HasSelections => SelectedCount > 0;
    public string BottomButtonText => HasSelections
        ? LocalizationResourceManager.Instance.Format("AddStation_AddSelected", SelectedCount)
        : LocalizationResourceManager.Instance["Common_Cancel"];

    public AddStationViewModel(
        IStationListService stationListService,
        IRadioRepository radioRepository,
        IAzuraStationCatalog catalog,
        INavigationService navigation,
        IAppEnvironment environment,
        ILogger<AddStationViewModel> logger)
    {
        Logger = logger;
        _navigation = navigation;
        _stationListService = stationListService;
        _radioRepository = radioRepository;
        _catalog = catalog;
        _environment = environment;
        Title = LocalizationResourceManager.Instance["AddStation_Title"];
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(UrlBase) || IsBusy)
            return;

        await SafeExecuteAsync(async () =>
        {
            HasResults = false;
            ErrorMessage = null;

            string urlBase = UrlBase.Trim()
                //.Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                // .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');

            List<AzuraCastStationListItem>? stations = await FetchStationsAsync(urlBase);
            if (stations == null)
            {
                ErrorMessage = LocalizationResourceManager.Instance["AddStation_ServerError"];
                return;
            }

            List<RadioStation> existing = await _radioRepository.GetAllAsync();
            HashSet<(string, int)> existingKeys = existing
                .Select(s => (RadioStation.UrlBaseKey(s.UrlBase), s.StationId))
                .ToHashSet();

            UnsubscribeItems();

            var items = stations
                .Select(s => new StationSelectionItem(s, urlBase, existingKeys.Contains((RadioStation.UrlBaseKey(urlBase), s.Id))))
                .ToList();

            foreach (StationSelectionItem item in items)
                item.PropertyChanged += OnItemSelectionChanged;

            AvailableStations = new ObservableCollection<StationSelectionItem>(items);
            HasResults = true;
            UpdateSelectedCount();
        }, LocalizationResourceManager.Instance["Err_SearchStations"]);
    }

    private async Task<List<AzuraCastStationListItem>?> FetchStationsAsync(string urlBase)
    {
        List<AzuraCastStationListItem>? stations = await _stationListService.FetchAsync(urlBase);
        if (stations is not null || !_environment.HasLocalNetworkPrivacy || !PrivateHostDetector.IsPrivateHost(urlBase))
            return stations;

        var elapsed = Stopwatch.StartNew();
        for (int attempt = 2; attempt <= LocalNetworkMaxAttempts && elapsed.Elapsed < LocalNetworkRetryBudget; attempt++)
        {
            await DelayAsync(LocalNetworkRetryDelay);
            Logger.LogInformation("Local network host {UrlBase}: retry {Attempt}/{Max}", urlBase, attempt, LocalNetworkMaxAttempts);

            stations = await _stationListService.FetchAsync(urlBase);
            if (stations is not null)
                return stations;
        }

        return null;
    }

    [RelayCommand]
    private async Task BottomActionAsync()
    {
        if (HasSelections)
            await SaveAsync();
        else
            await _navigation.GoBackAsync();
    }

    private async Task SaveAsync()
    {
        if (SelectedCount == 0 || IsBusy)
            return;

        await SafeExecuteAsync(async () =>
        {
            List<RadioStation> existing = await _radioRepository.GetAllAsync();
            int nextSortOrder = existing.Count > 0 ? existing.Max(s => s.SortOrder) + 1 : 0;

            var toAdd = AvailableStations.Where(i => i.IsSelected && !i.IsAlreadyAdded).ToList();
            foreach (StationSelectionItem item in toAdd)
            {
                await _radioRepository.InsertAsync(new RadioStation
                {
                    StationId = item.Station.Id,
                    UrlBase = item.UrlBase,
                    Name = item.Station.Name,
                    ShortName = item.Station.Shortcode,
                    Description = item.Station.Description,
                    StreamUrl = item.Station.DefaultMountPath,
                    LogoUrl = "",
                    WebsocketUrl = "",
                    IsFavorite = false,
                    SortOrder = nextSortOrder++,
                    IsTest = false
                });
            }

            _ = _catalog.ReloadAsync();
            await _navigation.GoBackAsync();
        }, LocalizationResourceManager.Instance["Err_SaveStations"]);
    }

    private void OnItemSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StationSelectionItem.IsSelected))
            UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = AvailableStations.Count(i => i.IsSelected && !i.IsAlreadyAdded);
    }

    private void UnsubscribeItems()
    {
        foreach (StationSelectionItem item in AvailableStations)
            item.PropertyChanged -= OnItemSelectionChanged;
    }
}
