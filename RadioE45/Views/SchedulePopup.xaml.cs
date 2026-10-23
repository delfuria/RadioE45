using CommunityToolkit.Maui.Views;
using RadioE45.ViewModels;

namespace RadioE45.Views;

public partial class SchedulePopup : Popup
{
    public SchedulePopup(ScheduleViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Il toolkit applica il proprio sfondo di default al contenitore del popup: lo si allinea
        // al tema corrente (il pannello vive poco, non serve seguire cambi tema a caldo).
        if (Application.Current?.Resources.TryGetValue("PageBackground", out object? background) == true
            && background is Color color)
            BackgroundColor = color;

        // Il pannello occupa al massimo ~70% dell'altezza della finestra; oltre, la lista scorre.
        Window? window = Application.Current?.Windows.FirstOrDefault();
        double windowHeight = window?.Height ?? 0;
        if (windowHeight > 0)
            SheetGrid.MaximumHeightRequest = windowHeight * 0.7;

        // Il toolkit dimensiona il popup sul contenuto, ignorando HorizontalOptions=Fill: con i caratteri
        // ingranditi il pannello restava stretto e centrato. La larghezza si fissa a quella della finestra.
        double windowWidth = window?.Width ?? 0;
        if (windowWidth > 0)
            WidthRequest = windowWidth;

        viewModel.LoadScheduleCommand.Execute(null);
    }

    private async void OnCloseClicked(object? sender, EventArgs e) => await CloseAsync();

    private async void OnSwipedDown(object? sender, SwipedEventArgs e) => await CloseAsync();
}
