using RadioE45.ViewModels;

namespace RadioE45.Views;

public sealed partial class OpenSourceLicensesPage : Page
{
    public OpenSourceLicensesPage()
    {
        InitializeComponent();
    }

    public OpenSourceLicensesViewModel ViewModel { get; } = App.Services.GetRequiredService<OpenSourceLicensesViewModel>();
}
