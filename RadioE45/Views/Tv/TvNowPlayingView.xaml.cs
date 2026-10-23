using RadioE45.Controls;
using RadioE45.ViewModels;

namespace RadioE45.Views.Tv;

public partial class TvNowPlayingView : ContentView, ITvSection
{
    public TvNowPlayingView(OnAirViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public Task OnShownAsync() => Task.CompletedTask;

    public void FocusDefault() => PlayPause.Dispatcher.Dispatch(() => TvFocus.Request(PlayPause));
}
