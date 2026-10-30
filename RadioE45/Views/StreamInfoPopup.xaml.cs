using CommunityToolkit.Maui.Views;
using RadioE45.ViewModels;

namespace RadioE45.Views;

public partial class StreamInfoPopup : Popup
{
    public StreamInfoPopup(StreamInfoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Il toolkit applica il proprio sfondo di default al contenitore del popup: lo si allinea al tema corrente.
        if (Application.Current?.Resources.TryGetValue("PageBackground", out object? background) == true
            && background is Color color)
            BackgroundColor = color;

        // Largo quanto serve a un indirizzo lungo ma sempre dentro la finestra; oltre ~85% dell'altezza la lista scorre.
        Window? window = Application.Current?.Windows.FirstOrDefault();
        if (window is { Width: > 0 })
            Panel.WidthRequest = Math.Min(window.Width - 48, 440);
        if (window is { Height: > 0 })
            Panel.MaximumHeightRequest = window.Height * 0.85;

        viewModel.LoadCommand.Execute(null);
    }

    private async void OnCloseClicked(object? sender, EventArgs e) => await CloseAsync();
}
