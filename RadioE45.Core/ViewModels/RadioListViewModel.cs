using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;
using RadioE45.Services.Localization;
using RadioE45.Services.Radio;
using RadioE45.Services.Platform;

namespace RadioE45.ViewModels;

public partial class RadioListViewModel : BaseViewModel
{
    private readonly IAzuraStationCatalog _catalog;
    private readonly OnAirViewModel _onAirViewModel;
    private readonly IRadioRepository _radioRepository;
    private readonly IDatabaseService _databaseService;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _dispatcher;

    [ObservableProperty]
    public partial ObservableCollection<AzuraStation> Stations { get; set; } = [];

    public OnAirViewModel OnAirViewModel => _onAirViewModel;

    public RadioListViewModel(
        IAzuraStationCatalog catalog,
        OnAirViewModel onAirViewModel,
        IRadioRepository radioRepository,
        IDatabaseService databaseService,
        INavigationService navigation,
        IDialogService dialogs,
        IUiDispatcher dispatcher,
        ILogger<RadioListViewModel> logger)
    {
        Logger = logger;
        _navigation = navigation;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        _catalog = catalog;
        _onAirViewModel = onAirViewModel;
        _radioRepository = radioRepository;
        _databaseService = databaseService;
        Title = LocalizationResourceManager.Instance["Tab_Radio"];

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
                bool accepted = await _dialogs.ConfirmAsync(
                    "Radio E45",
                    LocalizationResourceManager.Instance["RadioList_SeedPrompt"],
                    LocalizationResourceManager.Instance["Common_Yes"],
                    LocalizationResourceManager.Instance["Common_No"]);

                if (!accepted)
                {
                    await _navigation.GoToOnAirAsync();
                    return;
                }

                await _databaseService.SeedStationsAsync();
                await _navigation.GoToOnAirAsync();
                _ = _catalog.ReloadAsync(); // dopo navigazione: StationsRefreshed → auto-play, senza bloccare la transizione Android
                return;
            }

            // Ricarica solo se i dati sono vecchi (> 20s) o ci sono stazioni offline.
            // Evita di colpire il rate limit subito dopo il caricamento iniziale di InitializeAsync.
            bool stale = DateTimeOffset.UtcNow - _catalog.LastLoadedAt > TimeSpan.FromSeconds(20);
            bool hasOffline = _catalog.Stations.Any(s => !s.IsOnline);
            if (stale || hasOffline)
                await _catalog.ReloadAsync();

            RefreshStationsFromCatalog();
        }, LocalizationResourceManager.Instance["Err_LoadStations"]);
    }

    [RelayCommand]
    private async Task ForceReloadStationsAsync()
    {
        await SafeExecuteAsync(async () =>
        {
            await _catalog.ReloadAsync();
            RefreshStationsFromCatalog();
        }, LocalizationResourceManager.Instance["Err_RefreshStations"]);
    }

    private void OnStationsRefreshed()
    {
        _dispatcher.Post(RefreshStationsFromCatalog);
    }

    private void RefreshStationsFromCatalog()
    {
        int? currentId = _onAirViewModel.CurrentStation?.Id;
        var updated = new ObservableCollection<AzuraStation>(_catalog.Stations);
        foreach (AzuraStation s in updated)
        {
            s.IsActive = s.Id == currentId;
            s.DeleteCommand = DeleteStationCommand;
            s.EditCommand = EditStationCommand;
            s.PlayCommand = SelectAndPlayCommand;
        }
        Stations = updated;
    }

    [RelayCommand]
    private async Task DeleteStationAsync(AzuraStation station)
    {
        bool confirmed = await _dialogs.ConfirmAsync(
            LocalizationResourceManager.Instance["RadioList_DeleteStation"],
            LocalizationResourceManager.Instance.Format("RadioList_ConfirmDeleteMessage", station.Name),
            LocalizationResourceManager.Instance["Common_Delete"],
            LocalizationResourceManager.Instance["Common_Cancel"]);

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
            ErrorMessage = $"{LocalizationResourceManager.Instance["Err_DeleteStation"]}: {ex.Message}";
            return;
        }

        await _dispatcher.InvokeAsync(RefreshStationsFromCatalog);
    }

    public async Task MoveStationToEndAsync(AzuraStation dragged)
    {
        if (Stations.Count == 0)
            return;

        await MoveStationAsync(dragged, Stations[^1]);
    }

    public async Task MoveStationAsync(AzuraStation dragged, AzuraStation target)
    {
        int oldIndex = Stations.IndexOf(dragged);
        int newIndex = Stations.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
            return;

        Stations.Move(oldIndex, newIndex);

        try
        {
            await _catalog.ReorderAsync(Stations.Select(s => s.Id).ToList());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[RadioListViewModel] Riordino stazioni: {Message}", ex.Message);
            ErrorMessage = $"{LocalizationResourceManager.Instance["Err_ReorderStations"]}: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task NavigateToAddStationAsync()
    {
        await _navigation.GoToAddStationAsync();
    }

    [RelayCommand]
    private async Task EditStationAsync(AzuraStation station)
    {
        await _navigation.GoToEditStationAsync(station.Id);
    }

    [RelayCommand]
    private async Task SelectAndPlayAsync(AzuraStation station)
    {
        if (station == null || !station.IsOnline)
            return;

        foreach (AzuraStation s in Stations)
            s.IsActive = s.Id == station.Id;

        await _onAirViewModel.SelectStationCommand.ExecuteAsync(station);
        await _navigation.GoToOnAirAsync();
    }
}
