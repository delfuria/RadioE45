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

    // The filter is for this visit only: leaving the page clears it.
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.ClearFilterCommand.Execute(null);
    }

    // Visual only: the clear button of the toolbar replaces the TextBox's own delete button.
    private void OnFilterBoxLoaded(object sender, RoutedEventArgs e)
    {
        // Width, not Visibility: the focus visual states set Visibility back.
        if (FindDeleteButton(FilterBox) is { } button)
        {
            button.Width = 0;
            button.IsHitTestVisible = false;
        }
    }

    private static FrameworkElement? FindDeleteButton(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement { Name: "DeleteButton" } found)
                return found;
            if (FindDeleteButton(child) is { } nested)
                return nested;
        }
        return null;
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
