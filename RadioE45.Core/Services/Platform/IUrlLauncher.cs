namespace RadioE45.Services.Platform;

public interface IUrlLauncher
{
    /// <summary>Opens the URI with the system handler (browser, mail client, dialer).</summary>
    Task OpenAsync(Uri uri);
}
