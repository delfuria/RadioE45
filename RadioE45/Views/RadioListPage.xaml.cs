using RadioE45.ViewModels;

namespace RadioE45.Views;

public partial class RadioListPage : ContentPage
{
    private readonly RadioListViewModel _viewModel;

    public RadioListPage(RadioListViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadStationsCommand.ExecuteAsync(null);
    }
}
