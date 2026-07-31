using RadioE45.Models;
using RadioE45.ViewModels;

namespace RadioE45.Views;

public partial class RadioListPage : ContentPage
{
    private const string DragStationKey = "station";

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

    private void OnStationDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (sender is Element { BindingContext: AzuraStation station })
            e.Data.Properties[DragStationKey] = station;
    }

    private async void OnStationDrop(object? sender, DropEventArgs e)
    {
        if (sender is not Element { BindingContext: AzuraStation target })
            return;

        if (!e.Data.Properties.TryGetValue(DragStationKey, out object? value) || value is not AzuraStation dragged)
            return;

        await _viewModel.MoveStationAsync(dragged, target);
    }

    private async void OnDropAtEnd(object? sender, DropEventArgs e)
    {
        if (!e.Data.Properties.TryGetValue(DragStationKey, out object? value) || value is not AzuraStation dragged)
            return;

        await _viewModel.MoveStationToEndAsync(dragged);
    }
}
