namespace RadioE45.Services.Platform;

public interface INetworkMonitor
{
    bool HasInternet { get; }

    /// <summary>Raised on any connectivity change; the argument is <see cref="HasInternet"/>.</summary>
    event EventHandler<bool>? ConnectivityChanged;
}
