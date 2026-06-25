using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;
using RadioE45.Services.Radio;

namespace RadioE45.ViewModels;

public partial class RadioListViewModel : BaseViewModel
{
    private readonly IAzuraStationCatalog _catalog;
    private readonly OnAirViewModel _onAirViewModel;
    private readonly IRadioRepository _radioRepository;
    private readonly IDatabaseService _databaseService;

    [ObservableProperty]
    public partial ObservableCollection<AzuraStation> Stations { get; set; } = [];

    [ObservableProperty]
    public partial AzuraStation? SelectedStation { get; set; }

    public OnAirViewModel OnAirViewModel => _onAirViewModel;

    public RadioListViewModel(
        IAzuraStationCatalog catalog,
        OnAirViewModel onAirViewModel,
        IRadioRepository radioRepository,
        IDatabaseService databaseService,
        ILogger<RadioListViewModel> logger)
    {
        Logger = logger;
        _catalog = catalog;
        _onAirViewModel = onAirViewModel;
        _radioRepository = radioRepository;
        _databaseService = databaseService;
        Title = "Canali Radio";

        _catalog.StationsRefreshed += OnStationsRefreshed;
    }

    [RelayCommand]
    private async Task LoadStationsAsync()
    {
        await SafeExecuteAsync(async () =>
        {
            bool hasStations = await _radioRepository.HasStationsAsync();
            if (!hasStations)
            {
                bool accepted = await Shell.Current.DisplayAlertAsync(
                    "Radio E45",
                    "Nessuna stazione selezionata. Vuoi aggiungerne qualcuna di prova ?",
                    "Si",
                    "No");

                if (!accepted)
                {
                    await Shell.Current.GoToAsync("//OnAirPage");
                    return;
                }

                await _databaseService.SeedStationsAsync();
                await _catalog.ReloadAsync(); // seed appena fatto: reload sempre
                RefreshStationsFromCatalog();

                AzuraStation? first = _catalog.Stations.OrderBy(s => s.SortOrder).FirstOrDefault();
                if (first != null)
                    await _onAirViewModel.SelectStationCommand.ExecuteAsync(first);

                await Shell.Current.GoToAsync("//OnAirPage");
                return;
            }

            // Ricarica solo se i dati sono vecchi (> 20s) o ci sono stazioni offline.
            // Evita di colpire il rate limit subito dopo il caricamento iniziale di InitializeAsync.
            bool stale = DateTimeOffset.UtcNow - _catalog.LastLoadedAt > TimeSpan.FromSeconds(20);
            bool hasOffline = _catalog.Stations.Any(s => !s.IsOnline);
            if (stale || hasOffline)
                await _catalog.ReloadAsync();

            RefreshStationsFromCatalog();
        }, "Caricamento stazioni");
    }

    [RelayCommand]
    private async Task ForceReloadStationsAsync()
    {
        await SafeExecuteAsync(async () =>
        {
            await _catalog.ReloadAsync();
            RefreshStationsFromCatalog();
        }, "Aggiornamento stazioni");
    }

    private void OnStationsRefreshed()
    {
        MainThread.BeginInvokeOnMainThread(RefreshStationsFromCatalog);
    }

    private void RefreshStationsFromCatalog()
    {
        int? currentId = _onAirViewModel.CurrentStation?.Id;
        var updated = new ObservableCollection<AzuraStation>(_catalog.Stations);
        foreach (AzuraStation s in updated)
        {
            s.IsActive = s.Id == currentId;
            s.DeleteCommand = DeleteStationCommand;
        }
        Stations = updated;
    }

    [RelayCommand]
    private async Task DeleteStationAsync(AzuraStation station)
    {
        bool confirmed = await Shell.Current.DisplayAlertAsync(
            "Elimina stazione",
            $"Vuoi eliminare \"{station.Name}\"?",
            "Elimina",
            "Annulla");

        if (!confirmed)
            return;

        try
        {
            await _radioRepository.DeleteAsync(station.Id);
            _catalog.RemoveStation(station.Id);

            if (_onAirViewModel.CurrentStation?.Id == station.Id)
                await _onAirViewModel.ClearStationAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[RadioListViewModel] Eliminazione stazione: {Message}", ex.Message);
            ErrorMessage = $"Eliminazione stazione: {ex.Message}";
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(RefreshStationsFromCatalog);
    }

    [RelayCommand]
    private async Task NavigateToAddStationAsync()
    {
        await Shell.Current.GoToAsync("AddStationPage");
    }

    [RelayCommand]
    private async Task SelectAndPlayAsync()
    {
        var station = SelectedStation;
        if (station == null || !station.IsOnline)
            return;

        SelectedStation = null;

        foreach (AzuraStation s in Stations)
            s.IsActive = s.Id == station.Id;

        await _onAirViewModel.SelectStationCommand.ExecuteAsync(station);
        await Shell.Current.GoToAsync("//OnAirPage");
    }
}
