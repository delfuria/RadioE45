using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using RadioE45.Models;
using RadioE45.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace RadioE45.Views;

public sealed partial class RadioListPage : Page
{
    // The dragged station stays in the page (drag and drop never leaves the app), as in MAUI
    // where it travels in the drag properties.
    private AzuraStation? _draggedStation;

    public RadioListPage()
    {
        InitializeComponent();
    }

    public RadioListViewModel ViewModel { get; } = App.Services.GetRequiredService<RadioListViewModel>();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadStationsCommand.ExecuteAsync(null);
    }

    private void OnStationDragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: AzuraStation station })
        {
            _draggedStation = station;
            e.Data.RequestedOperation = DataPackageOperation.Move;
        }
    }

    private void OnStationDragOver(object sender, DragEventArgs e)
    {
        if (_draggedStation is not null)
            e.AcceptedOperation = DataPackageOperation.Move;
    }

    private async void OnStationDrop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: AzuraStation target } || TakeDraggedStation() is not { } dragged)
            return;

        await ViewModel.MoveStationAsync(dragged, target);
    }

    private async void OnDropAtEnd(object sender, DragEventArgs e)
    {
        if (TakeDraggedStation() is not { } dragged)
            return;

        await ViewModel.MoveStationToEndAsync(dragged);
    }

    private AzuraStation? TakeDraggedStation()
    {
        AzuraStation? dragged = _draggedStation;
        _draggedStation = null;
        return dragged;
    }

    private async void OnRefreshAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ViewModel.ForceReloadStationsCommand.ExecuteAsync(null);
    }
}
