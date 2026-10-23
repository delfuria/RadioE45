using RadioE45.Controls;
using RadioE45.Models;
using RadioE45.ViewModels;

namespace RadioE45.Views.Tv;

public partial class TvStationsView : ContentView, ITvSection
{
    private readonly RadioListViewModel _viewModel;

    public TvStationsView(RadioListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    // First visit: proposes the default stations when there are none (the ViewModel asks through the
    // dialog service, which on TV is the host's own dialog), otherwise refreshes the catalog.
    public Task OnShownAsync() => _viewModel.LoadStationsCommand.ExecuteAsync(null);

    // The remote starts from the station that is playing, else from the first one it can select.
    public void FocusDefault() => Dispatcher.Dispatch(() =>
    {
        Button? target = null;
        foreach (View child in StationsLayout.Children.OfType<View>())
        {
            if (child is not Button { IsEnabled: true } button)
                continue;

            if (button.BindingContext is AzuraStation { IsActive: true })
            {
                target = button;
                break;
            }

            target ??= button;
        }

        if (target is not null)
            TvFocus.Request(target);
    });
}
