using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;
using RadioE45.Services.Radio;

namespace RadioE45.ViewModels;

public partial class AddStationViewModel : BaseViewModel
{
    private readonly IStationListService _stationListService;
    private readonly IRadioRepository _radioRepository;
    private readonly IAzuraStationCatalog _catalog;

    [ObservableProperty]
    public partial string UrlBase { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveButtonText))]
    [NotifyPropertyChangedFor(nameof(HasSelections))]
    public partial int SelectedCount { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<StationSelectionItem> AvailableStations { get; set; } = [];

    [ObservableProperty]
    public partial bool HasResults { get; set; }

    public bool HasSelections => SelectedCount > 0;
    public string SaveButtonText => $"Aggiungi selezionate ({SelectedCount})";

    public AddStationViewModel(
        IStationListService stationListService,
        IRadioRepository radioRepository,
        IAzuraStationCatalog catalog,
        ILogger<AddStationViewModel> logger)
    {
        Logger = logger;
        _stationListService = stationListService;
        _radioRepository = radioRepository;
        _catalog = catalog;
        Title = "Aggiungi stazione";
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
                .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
                .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                .TrimEnd('/');

            List<AzuraCastStationListItem>? stations = await _stationListService.FetchAsync(urlBase);
            if (stations == null)
            {
                ErrorMessage = "Impossibile raggiungere il server. Controlla l'indirizzo.";
                return;
            }

            List<RadioStation> existing = await _radioRepository.GetAllAsync();
            HashSet<(string, int)> existingKeys = existing
                .Select(s => (s.UrlBase, s.StationId))
                .ToHashSet();

            UnsubscribeItems();

            var items = stations
                .Select(s => new StationSelectionItem(s, urlBase, existingKeys.Contains((urlBase, s.Id))))
                .ToList();

            foreach (StationSelectionItem item in items)
                item.PropertyChanged += OnItemSelectionChanged;

            AvailableStations = new ObservableCollection<StationSelectionItem>(items);
            HasResults = true;
            UpdateSelectedCount();
        }, "Ricerca stazioni");
    }

    [RelayCommand]
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

            // Reload catalog in background, then navigate back
            _ = _catalog.ReloadAsync();
            await Shell.Current.GoToAsync("..");
        }, "Salvataggio stazioni");
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
