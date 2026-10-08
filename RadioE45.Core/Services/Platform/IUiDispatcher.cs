namespace RadioE45.Services.Platform;

/// <summary>
/// UI-thread access for shared ViewModels and services (MAUI: MainThread/IDispatcher,
/// Uno: DispatcherQueue).
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Queues the action on the UI thread without waiting (BeginInvokeOnMainThread).</summary>
    void Post(Action action);

    /// <summary>Runs the action on the UI thread; runs inline when already on it.</summary>
    Task InvokeAsync(Action action);

    /// <summary>Runs the asynchronous action on the UI thread; runs inline when already on it.</summary>
    Task InvokeAsync(Func<Task> action);

    /// <summary>
    /// Creates a stopped repeating timer whose ticks run on the UI thread, or null when no UI
    /// dispatcher is available yet (e.g. during app startup/shutdown).
    /// </summary>
    IUiTimer? CreateTimer(TimeSpan interval, Action tick);
}

public interface IUiTimer
{
    void Start();
    void Stop();
}
