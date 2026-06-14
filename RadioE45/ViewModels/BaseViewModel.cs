using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RadioE45.ViewModels;

public partial class BaseViewModel : ObservableObject
{
    protected ILogger Logger { get; init; } = NullLogger.Instance;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    protected async Task SafeExecuteAsync(Func<Task> action, string errorPrefix = "Errore")
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"{errorPrefix}: {ex.Message}";
            Logger.LogError(ex, "[{ViewModel}] {Message}", GetType().Name, ErrorMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
