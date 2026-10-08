#if ANDROID || IOS
using CommunityToolkit.Maui.Alerts;
#endif

namespace RadioE45.Services.Platform;

public sealed class MauiDialogService : IDialogService
{
    public Task AlertAsync(string title, string message, string cancel) =>
        Shell.Current?.DisplayAlertAsync(title, message, cancel) ?? Task.CompletedTask;

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        Shell.Current?.DisplayAlertAsync(title, message, accept, cancel) ?? Task.FromResult(false);

    // Snackbar only on phones; on Mac Catalyst/Windows the confirmation is implicit in the UI.
    public Task ShowToastAsync(string message, TimeSpan duration)
    {
#if ANDROID || IOS
        return Snackbar.Make(message, duration: duration).Show();
#else
        return Task.CompletedTask;
#endif
    }
}
