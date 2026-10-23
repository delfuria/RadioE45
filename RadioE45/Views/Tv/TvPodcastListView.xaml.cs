using RadioE45.Controls;
using RadioE45.ViewModels;

namespace RadioE45.Views.Tv;

public partial class TvPodcastListView : ContentView, ITvSection
{
    private readonly PodcastListViewModel _viewModel;

    public TvPodcastListView(PodcastListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public Task OnShownAsync() => _viewModel.LoadPodcastsCommand.ExecuteAsync(null);

    public void FocusDefault() => Dispatcher.Dispatch(() =>
    {
        if (PodcastsLayout.Children.OfType<Button>().FirstOrDefault() is { } first)
            TvFocus.Request(first);
    });
}
