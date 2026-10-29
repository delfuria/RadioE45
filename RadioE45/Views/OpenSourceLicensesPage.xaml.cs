using RadioE45.ViewModels;

namespace RadioE45.Views;

public partial class OpenSourceLicensesPage : ContentPage
{
    public OpenSourceLicensesPage(OpenSourceLicensesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
