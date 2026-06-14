using CommunityToolkit.Maui.Views;
using RadioE45.Models;

namespace RadioE45.Services.Audio;

public interface IAudioService
{
    bool IsPlaying { get; }
    bool IsBuffering { get; }
    AzuraStation? CurrentStation { get; }

    event EventHandler<bool> PlaybackStateChanged;
    event EventHandler<string?> ErrorOccurred;
    event EventHandler<AzuraStation> StreamOpened;

    /// <summary>
    /// Called once from OnAirPage to attach the MediaElement from the visual tree.
    /// </summary>
    void Initialize(MediaElement mediaElement);

    Task PlayAsync(AzuraStation station);
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    void StopImmediate();
    void Shutdown();
    void SetVolume(double volume);
    void UpdateMetadata(string artist, string title, string? artworkUrl = null, int? elapsedSeconds = null, int? durationSeconds = null);
}
