namespace RadioE45.Services.Platform;

public sealed class MauiAppPaths : IAppPaths
{
    public string AppDataDirectory => FileSystem.AppDataDirectory;
}
