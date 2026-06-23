using RadioE45.ViewModels;

namespace RadioE45.Views;

public partial class AddStationPage : ContentPage
{
    public AddStationPage(AddStationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
