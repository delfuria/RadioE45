using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using RadioE45.ViewModels;

namespace RadioE45.Views;

public sealed partial class SongRequestPage : Page
{
    public SongRequestPage()
    {
        InitializeComponent();
    }

    public SongRequestViewModel ViewModel { get; } = App.Services.GetRequiredService<SongRequestViewModel>();

    // MAUI OnAppearing: the catalog of the current station is reloaded at every visit.
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadSongsCommand.ExecuteAsync(null);
    }

    private async void OnRefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
    {
        using var deferral = args.GetDeferral();
        await ViewModel.LoadSongsCommand.ExecuteAsync(null);
    }

    private void OnRefreshAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        Refresher.RequestRefresh();
    }
}
