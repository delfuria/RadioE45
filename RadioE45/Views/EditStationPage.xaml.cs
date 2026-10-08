using RadioE45.ViewModels;

namespace RadioE45.Views;

// Shell query parameters land on the page and are forwarded to the shared ViewModel, which
// lives in RadioE45.Core and cannot carry MAUI's [QueryProperty].
[QueryProperty(nameof(StationId), "id")]
public partial class EditStationPage : ContentPage
{
    private readonly EditStationViewModel _viewModel;

    public EditStationPage(EditStationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public int StationId
    {
        get => _viewModel.StationId;
        set => _viewModel.StationId = value;
    }
}
