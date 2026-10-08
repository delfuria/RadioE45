namespace RadioE45.Services.Platform;

public interface IDialogService
{
    Task AlertAsync(string title, string message, string cancel);

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>
    /// Short, non-blocking confirmation message. Heads without a toast surface (MAUI desktop)
    /// complete without showing anything.
    /// </summary>
    Task ShowToastAsync(string message, TimeSpan duration);
}
