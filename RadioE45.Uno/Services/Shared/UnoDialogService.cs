using RadioE45.Services.Platform;

namespace RadioE45.Services.Shared;

/// <summary>
/// <see cref="IDialogService"/> with ContentDialog (desktop and web). WinUI allows one open
/// ContentDialog per window, so calls are serialized.
/// </summary>
public sealed class UnoDialogService(UnoWindowHost host, ILogger<UnoDialogService> logger) : IDialogService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task AlertAsync(string title, string message, string cancel) =>
        await ShowAsync(title, message, primary: null, close: cancel);

    public async Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        await ShowAsync(title, message, primary: accept, close: cancel) == ContentDialogResult.Primary;

    // Like MAUI on Mac Catalyst/Windows: no toast surface on desktop and web, the confirmation is
    // implicit in the UI.
    public Task ShowToastAsync(string message, TimeSpan duration) => Task.CompletedTask;

    private async Task<ContentDialogResult> ShowAsync(string title, string message, string? primary, string close)
    {
        await _gate.WaitAsync();
        try
        {
            if (host.XamlRoot is not { } xamlRoot)
            {
                logger.LogWarning("Dialog '{Title}' not shown: the window has no content yet", title);
                return ContentDialogResult.None;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords },
                CloseButtonText = close,
                DefaultButton = primary is null ? ContentDialogButton.Close : ContentDialogButton.Primary,
                RequestedTheme = host.Root?.ActualTheme ?? ElementTheme.Default,
            };
            if (primary is not null)
                dialog.PrimaryButtonText = primary;

            return await dialog.ShowAsync();
        }
        finally
        {
            _gate.Release();
        }
    }
}
