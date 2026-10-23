using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadioE45.Models;

namespace RadioE45.ViewModels;

public partial class StationSelectionItem : ObservableObject
{
    public AzuraCastStationListItem Station { get; }
    public string UrlBase { get; }
    public bool IsAlreadyAdded { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public StationSelectionItem(AzuraCastStationListItem station, string urlBase, bool isAlreadyAdded)
    {
        Station = station;
        UrlBase = urlBase;
        IsAlreadyAdded = isAlreadyAdded;
        IsSelected = isAlreadyAdded;
    }

    // A row without a checkbox (remote control): OK toggles the selection of a station not yet added.
    [RelayCommand]
    private void ToggleSelected()
    {
        if (!IsAlreadyAdded)
            IsSelected = !IsSelected;
    }
}
