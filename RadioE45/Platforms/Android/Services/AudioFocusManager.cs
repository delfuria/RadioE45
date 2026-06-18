using Android.Content;
using Android.Media;
using Android.OS;
using RadioE45.Services.Audio;

namespace RadioE45;

/// <summary>
/// Manages Android audio focus for the radio stream.
/// Handles ducking (e.g. GPS navigation), transient loss (e.g. notification), and permanent loss (e.g. phone call).
/// Uses the IAudioService service-locator pattern (same as BecomingNoisyReceiver) to avoid a circular DI dependency.
/// </summary>
internal sealed class AudioFocusManager : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener, IAudioFocusManager
{
    private readonly AudioManager? _androidAudioManager;
    private AudioFocusRequestClass? _focusRequest;
    private bool _hasFocus;
    private bool _isDucking;
    private double _userVolume = 1.0;

    private IAudioService? Audio => IPlatformApplication.Current?.Services?.GetService<IAudioService>();

    public AudioFocusManager()
    {
        _androidAudioManager = Android.App.Application.Context.GetSystemService(Context.AudioService) as AudioManager;
    }

    public bool RequestFocus()
    {
        if (_androidAudioManager is null)
            return true;

        AudioFocusRequest result;

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            AudioAttributes attributes = new AudioAttributes.Builder()
                .SetUsage(AudioUsageKind.Media)!
                .SetContentType(AudioContentType.Music)!
                .Build()!;

            _focusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
                .SetAudioAttributes(attributes)!
                .SetAcceptsDelayedFocusGain(false)!
                .SetOnAudioFocusChangeListener(this)!
                .Build()!;

            result = _androidAudioManager.RequestAudioFocus(_focusRequest);
        }
        else
        {
#pragma warning disable CA1422
            result = _androidAudioManager.RequestAudioFocus(this, Android.Media.Stream.Music, AudioFocus.Gain);
#pragma warning restore CA1422
        }

        _hasFocus = result == AudioFocusRequest.Granted;
        return _hasFocus;
    }

    public void AbandonFocus()
    {
        if (!_hasFocus || _androidAudioManager is null)
            return;

        _hasFocus = false;

        if (OperatingSystem.IsAndroidVersionAtLeast(26) && _focusRequest is not null)
        {
            _androidAudioManager.AbandonAudioFocusRequest(_focusRequest);
            _focusRequest = null;
        }
        else
        {
#pragma warning disable CA1422
            _androidAudioManager.AbandonAudioFocus(this);
#pragma warning restore CA1422
        }
    }

    public void NotifyVolumeChanged(double volume)
    {
        // While ducking, the volume change comes from us — don't overwrite the user's intended level.
        if (!_isDucking)
            _userVolume = volume;
    }

    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        switch (focusChange)
        {
            case AudioFocus.Loss:
                // Permanent loss (e.g. another media app took over) — stop and release.
                _isDucking = false;
                _hasFocus = false;
                IAudioService? audioLoss = Audio;
                if (audioLoss is not null)
                    _ = audioLoss.StopAsync();
                break;

            case AudioFocus.LossTransient:
                // Temporary loss (e.g. phone call, alarm) — pause; will resume on Gain.
                _isDucking = false;
                IAudioService? audioTransient = Audio;
                if (audioTransient is not null)
                    _ = audioTransient.PauseAsync();
                break;

            case AudioFocus.LossTransientCanDuck:
                // Another app needs brief foreground audio (e.g. GPS turn-by-turn) — duck to 20%.
                _isDucking = true;
                Audio?.SetVolume(0.2);
                break;

            case AudioFocus.Gain:
                _hasFocus = true;
                IAudioService? audioGain = Audio;
                if (audioGain is null)
                    break;

                if (_isDucking)
                {
                    // Restore from duck — NavigationApp has finished speaking.
                    _isDucking = false;
                    audioGain.SetVolume(_userVolume);
                }
                else if (!audioGain.IsPlaying)
                {
                    // Restore from transient pause (e.g. call ended).
                    _ = audioGain.ResumeAsync();
                }
                break;
        }
    }
}
