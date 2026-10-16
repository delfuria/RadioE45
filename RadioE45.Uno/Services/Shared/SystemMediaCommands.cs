using RadioE45.Services.Audio;
using RadioE45.Services.Platform;
using RadioE45.Services.Radio;
using RadioE45.ViewModels;

namespace RadioE45.Services.Shared;

/// <summary>
/// Commands from the system media controls (MPRIS on Linux, Media Session on the web): play,
/// pause and toggle on the live radio as in the MAUI iOS remote command center
/// (AppDelegate.SetupRemoteCommandCenter); next/previous move between stations as the Android
/// Auto Seek-to-Next/Previous buttons do, through <see cref="OnAirViewModel"/> so the page follows.
/// No seek (live stream).
/// </summary>
public sealed class SystemMediaCommands
{
    private readonly IServiceProvider _services;
    private readonly IUiDispatcher _dispatcher;

    public SystemMediaCommands(IServiceProvider services, IUiDispatcher dispatcher)
    {
        _services = services;
        _dispatcher = dispatcher;
    }

    // Resolved on use: the audio service itself depends on IPlatformNowPlayingService.
    private IAudioService Audio => _services.GetRequiredService<IAudioService>();

    public bool CanPlay => Audio.CurrentStation is not null;

    public bool CanChangeStation => _services.GetRequiredService<IAzuraStationCatalog>().Stations.Count > 1;

    public Task PlayAsync() => _dispatcher.InvokeAsync(async () =>
    {
        if (Audio.CurrentStation is not null && !Audio.IsPlaying)
            await Audio.ResumeAsync();
    });

    public Task PauseAsync() => _dispatcher.InvokeAsync(async () =>
    {
        if (Audio.IsPlaying)
            await Audio.PauseAsync();
    });

    public Task TogglePlayPauseAsync() => _dispatcher.InvokeAsync(async () =>
    {
        if (Audio.IsPlaying)
            await Audio.PauseAsync();
        else if (Audio.CurrentStation is not null)
            await Audio.ResumeAsync();
    });

    public Task StopAsync() => PauseAsync();

    public Task NextStationAsync() =>
        _dispatcher.InvokeAsync(() => _services.GetRequiredService<OnAirViewModel>().NextStationCommand.ExecuteAsync(null));

    public Task PreviousStationAsync() =>
        _dispatcher.InvokeAsync(() => _services.GetRequiredService<OnAirViewModel>().PreviousStationCommand.ExecuteAsync(null));
}
