#if RADIOE45_DESKTOP
using System.Net.NetworkInformation;
using RadioE45.Services.Platform;

namespace RadioE45.Services.Desktop;

/// <summary>
/// <see cref="INetworkMonitor"/> on .NET <see cref="NetworkChange"/> (netlink on Linux): "internet"
/// means at least one non-loopback interface is up, the same approximation MAUI uses on desktop.
/// The event is raised only when the value actually changes.
/// </summary>
public sealed class DesktopNetworkMonitor : INetworkMonitor, IDisposable
{
    private readonly ILogger<DesktopNetworkMonitor> _logger;
    private readonly object _gate = new();
    private bool _hasInternet;

    public DesktopNetworkMonitor(ILogger<DesktopNetworkMonitor> logger)
    {
        _logger = logger;
        _hasInternet = NetworkInterface.GetIsNetworkAvailable();
        NetworkChange.NetworkAvailabilityChanged += OnAvailabilityChanged;
        NetworkChange.NetworkAddressChanged += OnAddressChanged;
    }

    public bool HasInternet
    {
        get { lock (_gate) return _hasInternet; }
    }

    public event EventHandler<bool>? ConnectivityChanged;

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnAvailabilityChanged;
        NetworkChange.NetworkAddressChanged -= OnAddressChanged;
    }

    private void OnAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) => Update(e.IsAvailable);

    // Address changes (e.g. DHCP renewal after the cable comes back) also re-evaluate availability.
    private void OnAddressChanged(object? sender, EventArgs e) => Update(NetworkInterface.GetIsNetworkAvailable());

    private void Update(bool hasInternet)
    {
        lock (_gate)
        {
            if (_hasInternet == hasInternet)
                return;
            _hasInternet = hasInternet;
        }

        _logger.LogInformation("Network {State}", hasInternet ? "available" : "lost");
        ConnectivityChanged?.Invoke(this, hasInternet);
    }
}
#endif
