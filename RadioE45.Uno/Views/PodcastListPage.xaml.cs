using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using RadioE45.ViewModels;

namespace RadioE45.Views;

public sealed partial class PodcastListPage : Page
{
    public PodcastListPage()
    {
        InitializeComponent();
    }

    // Singleton (App.xaml.cs), like the MAUI tab page: the ViewModel follows the station changes
    // and must not be created again at every visit of the tab.
    public PodcastListViewModel ViewModel { get; } = App.Services.GetRequiredService<PodcastListViewModel>();

    // MAUI OnAppearing.
    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadPodcastsCommand.ExecuteAsync(null);
    }

    private async void OnRefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
    {
        using var deferral = args.GetDeferral();
        await ViewModel.LoadPodcastsCommand.ExecuteAsync(null);
    }

    private void OnRefreshAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        Refresher.RequestRefresh();
    }
}
