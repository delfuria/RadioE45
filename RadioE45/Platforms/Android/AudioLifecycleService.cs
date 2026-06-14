using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Extensions.DependencyInjection;
using RadioE45.Services.Audio;

namespace RadioE45;

// stopWithTask="false" in the manifest keeps this service alive after the user swipes
// the app from recents, so OnTaskRemoved fires and we can stop the audio stream.
[Service(Name = "com.radioe45.app.AudioLifecycleService")]
public class AudioLifecycleService : Service
{
    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        => StartCommandResult.NotSticky;

    public override void OnTaskRemoved(Intent? rootIntent)
    {
        base.OnTaskRemoved(rootIntent);

        // Ferma la riproduzione e azzera CurrentStation (necessario per il rilevamento
        // del riavvio in OnAirPage.OnAppearing). La notifica nativa Android viene
        // rimossa dal nostro AndroidMediaNotificationService quando AudioService.Clear()
        // viene eseguito durante lo stop.
        var audio = IPlatformApplication.Current?.Services?.GetService<IAudioService>();
        audio?.StopImmediate();

        StopSelf();
    }
}
