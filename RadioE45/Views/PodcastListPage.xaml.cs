using RadioE45.ViewModels;

namespace RadioE45.Views;

public partial class PodcastListPage : ContentPage
{
    private readonly PodcastListViewModel _viewModel;

    public PodcastListPage(PodcastListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadPodcastsCommand.ExecuteAsync(null);
    }
}
