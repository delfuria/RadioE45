using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using RadioE45.ViewModels;

namespace RadioE45.Views;

public sealed partial class EditStationPage : Page
{
    public EditStationPage()
    {
        InitializeComponent();
    }

    public EditStationViewModel ViewModel { get; } = App.Services.GetRequiredService<EditStationViewModel>();

    // The station id is the Frame.Navigate parameter (UnoNavigationService.GoToEditStationAsync),
    // forwarded to the ViewModel as the MAUI page does with its "id" query property.
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is int stationId)
            ViewModel.StationId = stationId;
    }

    // Slider.Value is a double and LatencyOffsetSeconds an int: x:Bind cannot convert it back.
    private void OnLatencyChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        ViewModel.LatencyOffsetSeconds = (int)Math.Round(e.NewValue);
    }
}
