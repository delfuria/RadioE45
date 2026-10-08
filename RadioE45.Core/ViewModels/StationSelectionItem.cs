using CommunityToolkit.Mvvm.ComponentModel;
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
}
