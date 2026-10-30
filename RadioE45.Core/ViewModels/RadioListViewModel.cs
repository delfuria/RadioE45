using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
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
    private readonly ISettingsStore _settings;

    private const string ViewModeKey = "RadioList.ViewMode";

    [ObservableProperty]
    public partial ObservableCollection<AzuraStation> Stations { get; set; } = [];

    /// <summary>The stations that match <see cref="FilterText"/>, in the order of <see cref="Stations"/>.</summary>
    [ObservableProperty]
    public partial ObservableCollection<AzuraStation> FilteredStations { get; set; } = [];

    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    [ObservableProperty]
    public partial StationListViewMode ViewMode { get; set; }

    public bool HasFilter => !string.IsNullOrWhiteSpace(FilterText);

    public bool IsListMode => ViewMode == StationListViewMode.List;

    public bool IsGridMode => ViewMode == StationListViewMode.Grid;

    /// <summary>Reordering needs the full list: the grid has no drag handle, a filter hides rows.</summary>
    public bool CanReorder => IsListMode && !HasFilter;

    public string EmptyMessage => Stations.Count > 0 && HasFilter
        ? LocalizationResourceManager.Instance["RadioList_NoResults"]
        : LocalizationResourceManager.Instance["RadioList_Empty"];

    public OnAirViewModel OnAirViewModel => _onAirViewModel;

    public RadioListViewModel(
        IAzuraStationCatalog catalog,
        OnAirViewModel onAirViewModel,
        IRadioRepository radioRepository,
        IDatabaseService databaseService,
        INavigationService navigation,
        IDialogService dialogs,
        IUiDispatcher dispatcher,
        ISettingsStore settings,
        ILogger<RadioListViewModel> logger)
    {
        Logger = logger;
        _navigation = navigation;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        _settings = settings;
        _catalog = catalog;
        _onAirViewModel = onAirViewModel;
        _radioRepository = radioRepository;
        _databaseService = databaseService;
        Title = LocalizationResourceManager.Instance["Tab_Radio"];

        int savedMode = settings.Get(ViewModeKey, (int)StationListViewMode.List);
        ViewMode = Enum.IsDefined((StationListViewMode)savedMode) ? (StationListViewMode)savedMode : StationListViewMode.List;

        _catalog.StationsRefreshed += OnStationsRefreshed;
    }

    partial void OnStationsChanged(ObservableCollection<AzuraStation> value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(EmptyMessage));
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
        OnPropertyChanged(nameof(HasFilter));
        OnPropertyChanged(nameof(CanReorder));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    partial void OnViewModeChanged(StationListViewMode value)
    {
        _settings.Set(ViewModeKey, (int)value);
        OnPropertyChanged(nameof(IsListMode));
        OnPropertyChanged(nameof(IsGridMode));
        OnPropertyChanged(nameof(CanReorder));
    }

    private void ApplyFilter()
    {
        string filter = Fold(FilterText.Trim());
        FilteredStations = filter.Length == 0
            ? new ObservableCollection<AzuraStation>(Stations)
            : new ObservableCollection<AzuraStation>(Stations.Where(s =>
                Fold(s.Name).Contains(filter, StringComparison.Ordinal)));
    }

    // Case- and accent-insensitive on every platform: it does not depend on the ICU data (the web
    // and some Android builds run with invariant globalization).
    private static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }

    [RelayCommand]
    private void ClearFilter() => FilterText = "";

    [RelayCommand]
    private void ShowListView() => ViewMode = StationListViewMode.List;

    [RelayCommand]
    private void ShowGridView() => ViewMode = StationListViewMode.Grid;

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
        if (Stations.Count == 0 || !CanReorder)
            return;

        await MoveStationAsync(dragged, Stations[^1]);
    }

    public async Task MoveStationAsync(AzuraStation dragged, AzuraStation target)
    {
        if (!CanReorder)
            return;

        int oldIndex = Stations.IndexOf(dragged);
        int newIndex = Stations.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
            return;

        Stations.Move(oldIndex, newIndex);
        ApplyFilter();

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
