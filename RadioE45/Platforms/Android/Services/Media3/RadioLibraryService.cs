using Android.App;
using Android.Content;
using Android.Content.PM;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.Session;
using Microsoft.Extensions.Logging;
using static AndroidX.Media3.Session.MediaLibraryService;

namespace RadioE45;

// Fase 3 (Media3/Android Auto) — vedi docs/carplay/Phase3-Media3-ActionPlan.md §3.4.
// Unico service Android per la riproduzione: possiede il Player (RadioPlayerFactory, §3.2)
// e l'unica MediaLibrarySession (RadioLibrarySessionCallback, §3.3). Non serve implementare
// OnBind (MediaSessionService lo gestisce internamente restituendo il binder di sessione) né
// costruire una notifica custom (MediaSessionService avvia/aggiorna automaticamente un
// DefaultMediaNotificationProvider in risposta ai cambi di stato del Player).
[Service(
    Name = "com.radioe45.app.Media3.RadioLibraryService",
    Exported = true,
    ForegroundServiceType = ForegroundService.TypeMediaPlayback)]
public sealed class RadioLibraryService : MediaLibraryService
{
    private IExoPlayer? _player;
    private MediaLibrarySession? _session;
    private ILogger<RadioLibraryService>? _logger;

    public override void OnCreate()
    {
        base.OnCreate();

        _logger = IPlatformApplication.Current?.Services?.GetService<ILogger<RadioLibraryService>>();

        _player = RadioPlayerFactory.CreatePlayer(this);
        _session = new MediaLibrarySession.Builder(this, _player, new RadioLibrarySessionCallback())
            .SetSessionActivity(CreateContentPendingIntent())!
            .Build();
    }

    // In Java, MediaLibraryService.onGetSession(ControllerInfo) esegue un override
    // covariante (ritorna MediaLibrarySession anziché MediaSession) dell'astratto
    // MediaSessionService.onGetSession — covarianza che C# non espone come singolo
    // override. Il binding genera perciò DUE astratti distinti da soddisfare qui
    // (verificato per tentativi con il compilatore, non documentato esplicitamente):
    // OnGetSessionFromMediaLibraryService (quello effettivamente invocato dalla JNI, vedi
    // il Register("onGetSession", ...) sul binding) e OnGetSession (ereditato dalla classe
    // base, mai chiamato a runtime per un MediaLibraryService ma comunque da implementare
    // per compilare). Si delega il secondo al primo per avere un solo punto di verità.
    // Un solo controller alla volta è supportato: si ritorna sempre l'unica sessione;
    // l'allowlist per client non fidati resta responsabilità di
    // RadioLibrarySessionCallback.OnConnect (§3.3), non duplicata qui.
    public override MediaLibrarySession? OnGetSessionFromMediaLibraryService(MediaSession.ControllerInfo? controllerInfo)
        => _session;

    public override MediaSession? OnGetSession(MediaSession.ControllerInfo? controllerInfo)
        => OnGetSessionFromMediaLibraryService(controllerInfo);

    // Assorbe AudioLifecycleService (Fase 1): swipe dai recenti ferma completamente lo
    // stream (non solo pausa), stessa semantica di AudioLifecycleService.OnTaskRemoved.
    // Da verificare su device fisico (§7) che il comportamento percepito sia identico.
    public override void OnTaskRemoved(Intent? rootIntent)
    {
        base.OnTaskRemoved(rootIntent);

        _player?.Stop();
        StopSelf();
    }

    public override void OnDestroy()
    {
        _player?.Release();
        _session?.Release();
        _player = null;
        _session = null;
        base.OnDestroy();
    }

    private PendingIntent? CreateContentPendingIntent()
    {
        Intent intent = new(this, typeof(MainActivity));
        intent.SetAction(Intent.ActionMain);
        intent.AddCategory(Intent.CategoryLauncher);
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        PendingIntentFlags flags = PendingIntentFlags.UpdateCurrent;
        if (OperatingSystem.IsAndroidVersionAtLeast(23))
            flags |= PendingIntentFlags.Immutable;

        return PendingIntent.GetActivity(this, 1, intent, flags);
    }
}
