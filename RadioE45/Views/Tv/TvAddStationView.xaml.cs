using RadioE45.Controls;
using RadioE45.ViewModels;

namespace RadioE45.Views.Tv;

public partial class TvAddStationView : ContentView, ITvSection
{
    private readonly AddStationViewModel _viewModel;

    public TvAddStationView(AddStationViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    // The view lives as long as the root page: every visit starts from the empty search.
    public Task OnShownAsync()
    {
        _viewModel.Reset();
        return Task.CompletedTask;
    }

    public void FocusDefault() => Dispatcher.Dispatch(() => TvFocus.Request(UrlEntry));
}
