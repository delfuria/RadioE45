namespace RadioE45.Services.Audio;

public interface IAudioFocusManager
{
    /// <summary>Returns true if focus was granted (or accepted for delayed grant).</summary>
    bool RequestFocus();
    void AbandonFocus();
    /// <summary>Keeps the manager's stored user-volume in sync so ducking can restore correctly.</summary>
    void NotifyVolumeChanged(double volume);
}
