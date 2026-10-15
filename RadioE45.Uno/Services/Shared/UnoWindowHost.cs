using Microsoft.UI.Dispatching;

namespace RadioE45.Services.Shared;

/// <summary>
/// The app's single window, set by <see cref="App"/> at launch. Gives the shared services the
/// UI thread (<see cref="DispatcherQueue"/>), the root element (theme) and the
/// <see cref="Microsoft.UI.Xaml.XamlRoot"/> that ContentDialog needs.
/// </summary>
public sealed class UnoWindowHost
{
    public Window? Window { get; private set; }

    public DispatcherQueue? DispatcherQueue { get; private set; }

    public FrameworkElement? Root => Window?.Content as FrameworkElement;

    public XamlRoot? XamlRoot => Root?.XamlRoot;

    public void Attach(Window window)
    {
        Window = window;
        DispatcherQueue = window.DispatcherQueue;
    }
}
