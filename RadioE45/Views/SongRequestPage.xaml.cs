using RadioE45.ViewModels;

namespace RadioE45.Views;

public partial class SongRequestPage : ContentPage
{
    private readonly SongRequestViewModel _viewModel;

    public SongRequestPage(SongRequestViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadSongsCommand.ExecuteAsync(null);
    }
}
