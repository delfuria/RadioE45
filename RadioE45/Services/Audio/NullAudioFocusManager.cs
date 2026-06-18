namespace RadioE45.Services.Audio;

/// <summary>No-op implementation for iOS, macOS, and Windows — those platforms handle audio focus natively.</summary>
public sealed class NullAudioFocusManager : IAudioFocusManager
{
    public bool RequestFocus() => true;
    public void AbandonFocus() { }
    public void NotifyVolumeChanged(double volume) { }
}
