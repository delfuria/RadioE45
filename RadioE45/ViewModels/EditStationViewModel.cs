using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RadioE45.Models;
using RadioE45.Services.Data;
using RadioE45.Services.Localization;
using RadioE45.Services.Radio;

namespace RadioE45.ViewModels;

[QueryProperty(nameof(StationId), "id")]
public partial class EditStationViewModel : BaseViewModel
{
    private readonly IRadioRepository _radioRepository;
    private readonly IAzuraStationCatalog _catalog;
    private RadioStation? _station;

    [ObservableProperty]
    public partial int StationId { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string ShortName { get; set; } = "";

    [ObservableProperty]
    public partial string Description { get; set; } = "";

    [ObservableProperty]
    public partial bool HasCustomInfo { get; set; }

    public EditStationViewModel(
        IRadioRepository radioRepository,
        IAzuraStationCatalog catalog,
        ILogger<EditStationViewModel> logger)
    {
        Logger = logger;
        _radioRepository = radioRepository;
        _catalog = catalog;
        Title = LocalizationResourceManager.Instance["EditStation_Title"];
    }

    partial void OnStationIdChanged(int value)
    {
        _ = LoadStationAsync(value);
    }

    private async Task LoadStationAsync(int id)
    {
        await SafeExecuteAsync(async () =>
        {
            _station = await _radioRepository.GetByIdAsync(id);
            if (_station is null)
                return;

            Name = _station.Name;
            ShortName = _station.ShortName;
            Description = _station.Description;
            HasCustomInfo = _station.HasCustomInfo;
        }, LocalizationResourceManager.Instance["Err_LoadStations"]);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_station is null || IsBusy)
            return;

        await SafeExecuteAsync(async () =>
        {
            _station.Name = Name;
            _station.ShortName = ShortName;
            _station.Description = Description;
            _station.HasCustomInfo = HasCustomInfo;

            await _radioRepository.UpdateAsync(_station);
            _ = _catalog.ReloadAsync();

            await Shell.Current.GoToAsync("..");
        }, LocalizationResourceManager.Instance["Err_SaveStations"]);
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
