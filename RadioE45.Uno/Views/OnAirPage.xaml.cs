using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using RadioE45.Services.Shared;
using RadioE45.ViewModels;

namespace RadioE45.Views;

public sealed partial class OnAirPage : Page
{
    private bool _isWideLayout;

    public OnAirPage()
    {
        InitializeComponent();
        SizeChanged += OnSizeChanged;
    }

    public OnAirViewModel ViewModel { get; } = App.Services.GetRequiredService<OnAirViewModel>();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Volume, default station on the first visit, restart of the streaming station on the
        // next ones (e.g. back from Podcast); on the web nothing starts without a click.
        await ViewModel.ActivateAsync();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        double width = e.NewSize.Width;
        double height = e.NewSize.Height;
        if (width <= 0 || height <= 0) return;

        // Web: always portrait, the app is a narrow column whatever the browser size (UnoLayout).
        bool shouldBeWide = width > height && !UnoLayout.IsSingleColumn;

        // Same sizing as the MAUI page: the cover takes the height left by the fixed elements in
        // portrait, and sits beside the controls (bound to the shorter side) in landscape.
        ViewModel.ArtworkHeight = shouldBeWide
            ? Math.Clamp(height - 220, 140, 300)
            : Math.Clamp(height - 458, 150, 300);

        if (shouldBeWide == _isWideLayout) return;
        _isWideLayout = shouldBeWide;

        MainGrid.ColumnDefinitions.Clear();
        MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        if (shouldBeWide)
        {
            MainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(RightPanel, 0);
            Grid.SetColumn(RightPanel, 1);
        }
        else
        {
            Grid.SetRow(RightPanel, 1);
            Grid.SetColumn(RightPanel, 0);
        }
    }

    private void OnVolumeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        ViewModel.SetVolumeCommand.Execute(e.NewValue);
    }

    private async void OnRefreshRequested(RefreshContainer sender, RefreshRequestedEventArgs args)
    {
        using var deferral = args.GetDeferral();
        await ViewModel.RefreshNowPlayingCommand.ExecuteAsync(null);
    }

    private void OnRefreshAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        Refresher.RequestRefresh();
    }
}
