using Android.Content;
using Android.Media;
using RadioE45.Services.Audio;

namespace RadioE45;

internal sealed class BecomingNoisyReceiver : BroadcastReceiver
{
    private readonly IAudioService _audio;

    public BecomingNoisyReceiver(IAudioService audio) => _audio = audio;

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action == AudioManager.ActionAudioBecomingNoisy)
            _ = _audio.PauseAsync();
    }
}
