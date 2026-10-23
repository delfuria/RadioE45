using RadioE45.Controls;
using RadioE45.ViewModels;

namespace RadioE45.Views.Tv;

public partial class TvNowPlayingView : ContentView, ITvSection
{
    private readonly OnAirViewModel _viewModel;

    public TvNowPlayingView(OnAirViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    // As OnAirPage.OnAppearing on the phone: coming back to On Air restarts the station that a podcast episode
    // (or anything else) stopped. The root page calls it once at launch; every later visit comes through here.
    public Task OnShownAsync() => _viewModel.ActivateAsync();

    public void FocusDefault() => PlayPause.Dispatcher.Dispatch(() => TvFocus.Request(PlayPause));
}
