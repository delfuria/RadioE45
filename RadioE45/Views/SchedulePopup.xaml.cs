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
        double windowHeight = Application.Current?.Windows.FirstOrDefault()?.Height ?? 0;
        if (windowHeight > 0)
            SheetGrid.MaximumHeightRequest = windowHeight * 0.7;

        viewModel.LoadScheduleCommand.Execute(null);
    }

    private async void OnCloseClicked(object? sender, EventArgs e) => await CloseAsync();

    private async void OnSwipedDown(object? sender, SwipedEventArgs e) => await CloseAsync();
}
