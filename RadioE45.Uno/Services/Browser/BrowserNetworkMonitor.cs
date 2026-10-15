#if RADIOE45_BROWSER
using System.Runtime.InteropServices.JavaScript;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Browser;

/// <summary>
/// <see cref="INetworkMonitor"/> on <c>navigator.onLine</c> and the window <c>online</c>/<c>offline</c>
/// events (Platforms/WebAssembly/WasmScripts/RadioE45Network.js).
/// </summary>
public sealed partial class BrowserNetworkMonitor : INetworkMonitor
{
    private readonly ILogger<BrowserNetworkMonitor> _logger;
    private bool _hasInternet;

    public BrowserNetworkMonitor(ILogger<BrowserNetworkMonitor> logger)
    {
        _logger = logger;
        _hasInternet = IsOnline();
        Subscribe(OnChanged);
    }

    public bool HasInternet => _hasInternet;

    public event EventHandler<bool>? ConnectivityChanged;

    private void OnChanged(bool online)
    {
        if (_hasInternet == online)
            return;
        _hasInternet = online;
        _logger.LogInformation("Network {State}", online ? "online" : "offline");
        ConnectivityChanged?.Invoke(this, online);
    }

    [JSImport("globalThis.RadioE45Network.isOnline")]
    private static partial bool IsOnline();

    [JSImport("globalThis.RadioE45Network.subscribe")]
    private static partial void Subscribe([JSMarshalAs<JSType.Function<JSType.Boolean>>] Action<bool> callback);
}
#endif
