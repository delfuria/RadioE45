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

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.ClearFilterCommand.Execute(null);
    }

    // Visual only: as many tiles per row as fit, at least two.
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width > 0 && GridCollection.ItemsLayout is GridItemsLayout layout)
            layout.Span = Math.Max(2, (int)(width / 170));
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
