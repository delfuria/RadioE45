using RadioE45.Services.Platform;
using Windows.Storage;

namespace RadioE45.Services.Shared;

/// <summary>
/// <see cref="IAppPaths"/> on <see cref="ApplicationData.LocalFolder"/>: on desktop
/// ~/.local/share/RadioE45/com.radioe45.app/LocalState/data (Linux), on the web a folder of the
/// IDBFS file system persisted in the browser. <see cref="InitializeAsync"/> must complete before
/// anything touches the file system (the web needs it to finish the IDBFS initialization).
/// </summary>
public sealed class UnoAppPaths : IAppPaths
{
    private string? _appDataDirectory;

    public string AppDataDirectory =>
        _appDataDirectory ?? throw new InvalidOperationException($"{nameof(UnoAppPaths)}.{nameof(InitializeAsync)} has not completed.");

    public async Task InitializeAsync()
    {
        var folder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("data", CreationCollisionOption.OpenIfExists);
        _appDataDirectory = folder.Path;
    }
}
