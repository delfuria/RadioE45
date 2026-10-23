namespace RadioE45.Controls;

public partial class TvDialogOverlay : ContentView
{
    private TaskCompletionSource<bool>? _completion;

    public TvDialogOverlay()
    {
        InitializeComponent();
    }

    public bool IsOpen => _completion is not null;

    /// <summary>Raised when the dialog opens: the host disables the rest of the page so the remote cannot leave the dialog.</summary>
    public event EventHandler? Opened;

    /// <summary>Raised after the dialog closes, whatever the result.</summary>
    public event EventHandler? Closed;

    public Task<bool> ShowAsync(string title, string message, string accept, string? cancel)
    {
        _completion?.TrySetResult(false);

        TitleLabel.Text = title;
        MessageLabel.Text = message;
        AcceptButton.Text = accept;
        CancelButton.Text = cancel;
        CancelButton.IsVisible = !string.IsNullOrEmpty(cancel);

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _completion = completion;
        IsVisible = true;
        Opened?.Invoke(this, EventArgs.Empty);

        // The main button starts with the focus, so OK on the remote works at once.
        FocusAccept(attempt: 0);
        return completion.Task;
    }

    public void Dismiss(bool result)
    {
        TaskCompletionSource<bool>? completion = _completion;
        _completion = null;
        IsVisible = false;
        Closed?.Invoke(this, EventArgs.Empty);
        completion?.TrySetResult(result);
    }

    // The overlay was invisible a moment ago, so its native view may not be ready: retry a few times,
    // a short while apart, until the focus takes (touch mode is handled by TvFocus).
    private void FocusAccept(int attempt)
    {
        AcceptButton.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(attempt == 0 ? 30 : 150), () =>
        {
            if (!IsOpen || AcceptButton.IsFocused)
                return;

            TvFocus.Request(AcceptButton);
            if (attempt < 5)
                FocusAccept(attempt + 1);
        });
    }

    private void OnAcceptClicked(object? sender, EventArgs e) => Dismiss(true);

    private void OnCancelClicked(object? sender, EventArgs e) => Dismiss(false);
}
