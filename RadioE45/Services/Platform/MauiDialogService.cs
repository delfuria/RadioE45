#if ANDROID || IOS
using CommunityToolkit.Maui.Alerts;
#endif

using RadioE45.Views.Tv;

namespace RadioE45.Services.Platform;

public sealed class MauiDialogService : IDialogService
{
    private readonly TvHostAccessor _tv;

    public MauiDialogService(TvHostAccessor tv)
    {
        _tv = tv;
    }

    // On TV the system AlertDialog has no button with the initial focus (measured), so the TV host
    // shows its own dialog (docs/ANDROID-TV-ROADMAP.md §2.2).
    public Task AlertAsync(string title, string message, string cancel) =>
        _tv.Current is { } tv
            ? tv.AlertAsync(title, message, cancel)
            : Shell.Current?.DisplayAlertAsync(title, message, cancel) ?? Task.CompletedTask;

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        _tv.Current is { } tv
            ? tv.ConfirmAsync(title, message, accept, cancel)
            : Shell.Current?.DisplayAlertAsync(title, message, accept, cancel) ?? Task.FromResult(false);

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
