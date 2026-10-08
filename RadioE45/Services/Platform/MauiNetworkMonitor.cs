namespace RadioE45.Services.Platform;

public sealed class MauiNetworkMonitor : INetworkMonitor
{
    public MauiNetworkMonitor()
    {
        Connectivity.ConnectivityChanged += (_, e) =>
            ConnectivityChanged?.Invoke(this, e.NetworkAccess == NetworkAccess.Internet);
    }

    public bool HasInternet => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public event EventHandler<bool>? ConnectivityChanged;
}
