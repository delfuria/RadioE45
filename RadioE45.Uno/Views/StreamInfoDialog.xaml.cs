using RadioE45.ViewModels;

namespace RadioE45.Views;

/// <summary>
/// Stream info panel (MAUI: StreamInfoPopup), shown by <see cref="ShellPage.ShowInfo"/>. A new one
/// for every opening, like the transient MAUI popup.
/// </summary>
public sealed partial class StreamInfoDialog : UserControl
{
    public StreamInfoDialog()
    {
        InitializeComponent();
    }

    public StreamInfoViewModel ViewModel { get; } = App.Services.GetRequiredService<StreamInfoViewModel>();

    /// <summary>Raised by the ✕ button; the shell closes the panel.</summary>
    public event EventHandler? CloseRequested;

    public void Load() => ViewModel.LoadCommand.Execute(null);

    private void OnCloseClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
}
