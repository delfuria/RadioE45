using RadioE45.ViewModels;
using RadioE45.Services.Data;

namespace RadioE45.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        DiagnosticaBorder.IsVisible = false;
    }

    private void OnDiagnosticaLabelTapped(object sender, TappedEventArgs e)
    {
        DiagnosticaBorder.IsVisible = true;
    }

    private async void OnSiteUrlTapped(object sender, TappedEventArgs e)
    {
        await Launcher.Default.OpenAsync("https://radioe45.it");
    }
}
