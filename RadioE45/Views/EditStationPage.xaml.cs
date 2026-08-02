using RadioE45.ViewModels;

namespace RadioE45.Views;

public partial class EditStationPage : ContentPage
{
    public EditStationPage(EditStationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
