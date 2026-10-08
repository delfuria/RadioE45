namespace RadioE45.Services.Platform;

public sealed class MauiUrlLauncher : IUrlLauncher
{
    public Task OpenAsync(Uri uri) => Launcher.Default.OpenAsync(uri);
}
