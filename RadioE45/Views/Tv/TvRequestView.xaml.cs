using RadioE45.Controls;
using RadioE45.ViewModels;

namespace RadioE45.Views.Tv;

public partial class TvRequestView : ContentView, ITvSection
{
    private readonly SongRequestViewModel _viewModel;

    public TvRequestView(SongRequestViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    // The view lives as long as the root page: every visit starts from an empty search and a fresh list.
    public Task OnShownAsync()
    {
        _viewModel.SearchText = string.Empty;
        return _viewModel.LoadSongsCommand.ExecuteAsync(null);
    }

    // The remote starts from the first song (the list scrolls from there), else from the search box.
    public void FocusDefault() => Dispatcher.Dispatch(() =>
    {
        View? target = SongsLayout.Children.OfType<Button>().FirstOrDefault();
        TvFocus.Request(target ?? SearchEntry);
    });
}
