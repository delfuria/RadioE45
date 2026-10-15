using RadioE45.Services.Platform;

namespace RadioE45.Services.Shared;

/// <summary>
/// <see cref="IUrlLauncher"/> with the WinRT launcher: system handler on desktop (xdg-open on
/// Linux), new browser tab on the web.
/// </summary>
public sealed class UnoUrlLauncher(ILogger<UnoUrlLauncher> logger) : IUrlLauncher
{
    public async Task OpenAsync(Uri uri)
    {
        if (!await Windows.System.Launcher.LaunchUriAsync(uri))
            logger.LogWarning("No handler opened {Uri}", uri);
    }
}
