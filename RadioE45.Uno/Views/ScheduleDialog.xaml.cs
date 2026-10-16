using Microsoft.UI.Xaml.Input;
using RadioE45.ViewModels;

namespace RadioE45.Views;

/// <summary>
/// Schedule sheet (MAUI: SchedulePopup), shown by <see cref="ShellPage.ShowSheet"/>. A new one
/// for every opening, like the transient MAUI popup.
/// </summary>
public sealed partial class ScheduleDialog : UserControl
{
    // Downward drag on the handle that closes the sheet (MAUI: SwipeGestureRecognizer Down).
    private const double CloseDragDistance = 40;

    public ScheduleDialog()
    {
        InitializeComponent();
    }

    public ScheduleViewModel ViewModel { get; } = App.Services.GetRequiredService<ScheduleViewModel>();

    /// <summary>Raised by the ✕ button and the drag down; the shell closes the sheet.</summary>
    public event EventHandler? CloseRequested;

    public void Load() => ViewModel.LoadScheduleCommand.Execute(null);

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);

    private void OnDragCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
    {
        if (e.Cumulative.Translation.Y > CloseDragDistance)
            CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
