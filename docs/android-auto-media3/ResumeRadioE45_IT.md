# RadioE45 — Analisi tecnica: Android Auto, Bluetooth, servizio in background

**Cliente:** analisi per un'azienda esterna
**Oggetto:** app RadioE45 (.NET MAUI 10, streaming webradio AzuraCast)
**Ambito:** riproduzione in background (foreground service), Android Auto, controllo via Bluetooth
**Piattaforma di test:** Android, emulatore Pixel 9 Pro XL, API 36 (build Debug, `com.radioe45.app`)
**Data:** 2026-06-14
**Versione app:** 0.20 (versionCode 20)
**minSdk:** 26 (Android 8.0) · **targetSdk:** 36

> Il repository è stato compilato ed eseguito sull'emulatore. L'app si compila (0 errori, 2 warning irrilevanti), si installa e funziona — lo stream va e il "now playing" si aggiorna. La valutazione seguente riguarda le tre aree critiche per l'uso in auto.

---

## 1. Sintesi esecutiva

L'app ha un **core di riproduzione solido e ben ragionato** (watchdog di riconnessione, probing di più URL dello stream, gestione consapevole della "pausa" in diretta come chiusura della connessione). Tuttavia, lo **strato di integrazione di sistema (Android Auto / Bluetooth / servizio in background) presenta gravi difetti architetturali e almeno due bug che possono causare un crash o un pulsante Play "muto"**.

| Area | Valutazione | Commento |
|--------|-------|-----------|
| Servizio in background (foreground) | 🟠 **Media** | Funziona, ma architettura non standard + rischio crash `startForeground` |
| Android Auto | 🔴 **Bassa / rischiosa** | 3 MediaSession separate, il Play da avvio a freddo può non fare nulla, API obsolete |
| Bluetooth / pulsanti multimediali | 🔴 **Bassa** | Nessun audio focus, nessuna reazione alla disconnessione BT, routing dei pulsanti ambiguo |
| Qualità del codice del core audio | 🟢 **Alta** | Riconnessione robusta, buoni commenti, decisioni consapevoli |

**Verdetto:** l'app è adatta a un ulteriore sviluppo, ma nello stato attuale **non è pronta per la certificazione Android Auto** né per un uso confortevole in auto. Sono necessarie le correzioni descritte nelle sezioni 5–8.

---

## 2. ⚠️ AVVERTENZA FONDAMENTALE — Android Auto e l'assenza di certificazione Google Play

È il vincolo di business più importante e va comunicato chiaramente al cliente.

**Android Auto non caricherà questa app per un utente comune.** Le app della categoria "media" per Android Auto devono superare una **revisione separata di Google** ed essere distribuite tramite **Google Play**. Un'app installata da file APK (sideload / "origini sconosciute") **non comparirà per impostazione predefinita** sullo schermo dell'auto.

Per eseguirla su Android Auto **nello stato attuale**, l'utente deve manualmente:

1. Nell'app **Android Auto** sbloccare la **modalità sviluppatore** (toccare 10× il numero di versione).
2. Nel menu sviluppatore di AA abilitare **"Unknown sources" / "Origini sconosciute"** (esecuzione di app non autorizzate).
3. Avere l'app installata da APK (sideload), perché non è su Google Play.

**Conseguenze:**

- Senza i passaggi sopra, su qualsiasi telefono l'app **non compare affatto** in Android Auto — non è un bug del codice, ma una policy della piattaforma.
- La distribuzione di produzione richiede: pubblicazione su Google Play **e** invio e accettazione nel programma Android for Cars (categoria media). Google verifica, tra l'altro, la conformità alle linee guida UX per il guidatore.
- La build testata è **Debug e non firmata con chiave di produzione** — un ulteriore motivo per cui AA non la riterrà attendibile.

> **Conclusione per il report:** ogni dimostrazione di Android Auto con questa app avviene esclusivamente in modalità sviluppatore AA con origini sconosciute abilitate. Non è lo stato in cui il cliente finale vedrà l'app. Il percorso di produzione richiede certificazione Google + distribuzione tramite Play.

---

## 3. Come funziona lo strato audio (contesto per la valutazione)

Comprendere l'architettura è la chiave per la maggior parte dei bug seguenti.

```
OnAirPage (UI)  ──contiene──▶  MediaElement (ExoPlayer)   ← il vero player audio
      │  OnAppearing()
      ▼
AudioService (singleton)  ── controlla MediaElement, riconnessione, watchdog
      │  UpdatePlaybackState / Clear
      ▼
IPlatformNowPlayingService → AndroidNowPlayingService
      │  scrive lo stato in ▼
AndroidNowPlayingStateStore (store statico + evento SnapshotChanged)
      ├──▶ AndroidMediaNotificationService  → MediaSession "RadioE45Playback" + notifica foreground
      └──▶ RadioMediaBrowserService         → MediaSession "RadioE45Auto"     + albero per Android Auto
```

**L'osservazione architetturale più importante:** il vero player (ExoPlayer dentro `MediaElement`) **vive nello strato UI** — viene creato e agganciato solo in `OnAirPage.OnAppearing()` tramite `AudioService.Initialize(AudioPlayer)` (`Views/OnAirPage.xaml.cs:34`, `Services/Audio/AudioService.cs:41`). Il foreground service della libreria CommunityToolkit è **disabilitato di proposito**:

```csharp
.UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false)  // MauiProgram.cs:36
```

Al suo posto il team ha scritto il proprio `AndroidMediaNotificationService`. **Questo servizio NON è il proprietario del player** — gestisce solo la notifica e la MediaSession. Il player resta agganciato all'activity/pagina. È l'origine della maggior parte dei problemi di AA e background.

---

## 4. Valutazione: servizio in background (foreground service) — 🟠 Media

### Cosa è buono
- Tipo di servizio `mediaPlayback` corretto e set completo di permessi (`FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_MEDIA_PLAYBACK`, `POST_NOTIFICATIONS`, `WAKE_LOCK`) — `AndroidManifest.xml:16,27-30`.
- `StartForeground` invocato con il tipo `TypeMediaPlayback` su API 29+ (`AndroidMediaNotificationService.cs:242`).
- Gestione consapevole dello swipe dai recenti: `AudioLifecycleService` con `stopWithTask="false"` ferma lo stream in `OnTaskRemoved` (`AudioLifecycleService.cs:19`). Soluzione buona e matura.
- Canale di notifica con `LockscreenVisibility.Public` e `MediaStyle` — corretto per la schermata di blocco.

### 🔴 BUG CRITICO #1 — possibile crash "startForeground not called"
`RequestRefresh` avvia il servizio tramite `StartForegroundService` (`AndroidMediaNotificationService.cs:78,87-88`), che **obbliga** il sistema a chiamare `startForeground()` entro 5 s. Ma in `PublishNotification`:

```csharp
if (string.IsNullOrWhiteSpace(snapshot.Title) && string.IsNullOrWhiteSpace(snapshot.Artist))
{
    StopServiceNotification();   // <-- NON chiama StartForeground!
    return;
}
```
(`AndroidMediaNotificationService.cs:99-103`)

Se `UpdateMetadata`/`UpdatePlaybackState` arriva quando lo snapshot ha **titolo e artista vuoti** (es. una stazione senza dati "now playing" nel primo secondo, o uno stato transitorio), il servizio viene avviato come foreground-in-attesa ma **termina senza `startForeground()`** → il sistema lancia `ForegroundServiceDidNotStartInTimeException` / `RemoteServiceException`. **Questo fa crashare il processo.** Rischio reale, soprattutto all'avvio della riproduzione.
**Correzione:** non usare `StartForegroundService` per un percorso che potrebbe non pubblicare una notifica; oppure pubblicare sempre una notifica minima (un placeholder "RadioE45") prima di decidere lo stop.

### 🟠 BUG ALTO #2 — avvio del foreground service dal background (Android 12+)
Su targetSdk 36, `StartForegroundService` invocato mentre l'app è **in background** (es. un comando Play da Android Auto con l'app chiusa) lancia `ForegroundServiceStartNotAllowedException`. La chiamata in `StartService` (`cs:82-91`) **non ha blocco try/catch** → eccezione non gestita. Esiste un'esenzione per `mediaPlayback` legata a una MediaSession, ma qui il percorso è fragile e non garantito.
**Correzione:** racchiudere l'avvio in try/catch e — a tendere — spostare il player nel servizio stesso (Media3 `MediaSessionService`), che ha un'esenzione nativa da questa restrizione.

### 🟠 Nota #3 — la riproduzione in background dipende dal ciclo di vita della UI
Poiché ExoPlayer vive nel `MediaElement` della pagina e il servizio del toolkit è disabilitato, la continuità audio in background si basa unicamente sul fatto che **il processo ha priorità foreground grazie al servizio personalizzato**. Funziona finché la pagina è apparsa almeno una volta e il servizio è partito. È però una costruzione fragile — con una gestione aggressiva di memoria/activity il rischio di rilascio del player cresce. Raccomandazione: a tendere il player nel servizio, non nella UI.

---

## 5. Valutazione: Android Auto — 🔴 Bassa / rischiosa

### Cosa è buono
- È presente un `MediaBrowserService` con dichiarazione nel manifest e `automotive_app_desc.xml` con `<uses name="media"/>` — scheletro AA corretto (`AndroidManifest.xml:8-23`, `Resources/xml/automotive_app_desc.xml`).
- Validazione del chiamante in `OnGetRoot` (allowlist di pacchetti) — buona pratica di sicurezza (`RadioMediaBrowserService.cs:66-71`).
- Caricamento asincrono della lista stazioni con `result.Detach()` — pattern corretto (`cs:82-83`).
- Metadati e `PlaybackState` costruiti con le azioni dei transport control.

### 🔴 BUG CRITICO #4 — il Play da avvio a freddo può non fare nulla
Quando Android Auto avvia l'app in background e l'utente preme Play, `AutoMediaCallback.OnPlay` → `audio.PlayAsync(...)`. Ma:

```csharp
public async Task PlayAsync(AzuraStation station)
{
    if (_mediaElement is null)
        return;   // <-- uscita silenziosa
    ...
}
```
(`AudioService.cs:82-85`)

`_mediaElement` viene impostato **solo** in `OnAirPage.OnAppearing()`. All'avvio da Android Auto la pagina UI potrebbe non comparire mai → `_mediaElement == null` → **il Play non riproduce nulla, senza alcun errore per l'utente.** È un classico bug critico di integrazione AA: un player legato alla UI non esiste quando è l'auto a controllarlo.
**Correzione:** il player deve essere indipendente dalla pagina (creato nel servizio/applicazione), non prelevato dal visual tree.

### 🔴 BUG ALTO #5 — tre MediaSession separate
L'app ha **tre** sessioni multimediali indipendenti:
1. `"RadioE45Auto"` in `RadioMediaBrowserService` (`cs:41`),
2. `"RadioE45Playback"` in `AndroidMediaNotificationService` (`cs:38`),
3. la sessione interna ExoPlayer/MediaElement (toolkit).

Android Auto, la schermata di blocco e il Bluetooth si aspettano **una sola** sessione attiva come fonte di verità. Tre sessioni con `Active=true` comportano:
- ambiguità su quale riceva i pulsanti (vedi BT, sezione 6),
- rischio di controlli duplicati/incoerenti,
- sincronizzazione dello stato gestita manualmente tramite lo statico `AndroidNowPlayingStateStore` — un workaround, non una soluzione.
**Correzione:** una sola `MediaSession`/Media3 condivisa per notifica, AA e BT.

### 🟠 BUG MEDIO #6 — API obsolete invece di Media3
Si usano il framework `Android.Media.Session.MediaSession` e `Android.Service.Media.MediaBrowserService` (`RadioMediaBrowserService.cs:20`). Per Android Auto, Google **raccomanda Media3** (`androidx.media3.session.MediaLibraryService` / `MediaSession`). La vecchia API funziona, ma: minore compatibilità con le surface AA, niente funzionalità più recenti, certificazione più difficile. `grep` conferma l'assenza di `Media3`/`MediaSessionCompat`/`MediaButtonReceiver` nel progetto.

### 🟠 BUG MEDIO #7 — allowlist dei chiamanti troppo ristretta
`AllowedCallers` contiene solo `gearhead` e `mediasimulator` (`cs:24-28`). Omette, tra gli altri, `com.google.android.carassistant` (Assistant / modalità Guida), Android Automotive OS, Wear. Alcune surface non potranno sfogliare i contenuti. Valutare di estendere la lista + validazione della firma anziché del solo nome pacchetto.

### 🟡 BASSO #8 — assenza di hint sullo stile dei contenuti (content style)
`OnGetRoot` restituisce un `BrowserRoot` senza extra (`CONTENT_STYLE_*`). AA mostrerà il layout a lista predefinito. Cosmetico, ma incide sulla valutazione UX in fase di certificazione.

---

## 6. Valutazione: Bluetooth / pulsanti multimediali — 🔴 Bassa

In auto il controllo avviene principalmente tramite **AVRCP su Bluetooth** (pulsanti al volante, vivavoce) e tramite il routing audio A2DP.

### 🔴 BUG CRITICO #9 — nessuna gestione dell'audio focus
`grep` su tutto il progetto: **zero** occorrenze di `AudioFocus` / `requestAudioFocus`. Effetti in auto/BT:
- un **prompt di navigazione** in arrivo o una **telefonata** non abbassa/mette in pausa la radio (niente ducking),
- al termine della chiamata la radio non riprende da sola,
- conflitti con altre app audio.
È uno dei motivi più frequenti di rifiuto nella revisione AA e un grave difetto UX in auto.
**Correzione:** implementare `AudioManager.RequestAudioFocus` + reazione a `AudioFocusChange` (pause/duck/resume). Media3 lo fa in gran parte automaticamente.

### 🔴 BUG ALTO #10 — nessuna reazione alla disconnessione BT (ACTION_AUDIO_BECOMING_NOISY)
Nessun `BroadcastReceiver` per `ACTION_AUDIO_BECOMING_NOISY` (confermato da grep). Quando l'utente esce dall'auto / disconnette il BT / scollega le cuffie, **l'audio passa immediatamente all'altoparlante del telefono** a volume massimo. Caso classico previsto dalle linee guida Android (pausa al "becoming noisy").
**Correzione:** un receiver `ACTION_AUDIO_BECOMING_NOISY` → `PauseAsync()`.

### 🟠 BUG ALTO #11 — routing ambiguo dei pulsanti BT
Due sessioni attive (`RadioE45Auto` e `RadioE45Playback`) dichiarano `HandlesMediaButtons | HandlesTransportControls`. Il sistema instrada il media button alla sessione "più recente" — con due attive il comportamento è imprevedibile. Il pulsante Play/Pause al volante può finire su una sessione il cui callback in realtà non controlla il player.
**Correzione:** una sola sessione (collegata al punto 5).

### Cosa funziona
- Il **routing audio A2DP** in sé (il segnale arriva agli altoparlanti dell'auto) funziona automaticamente — non richiede codice e andrà bene.
- I metadati (titolo/artista/copertina) vengono scritti in `MediaMetadata`, quindi quando la sessione è selezionata correttamente il display BT/HU mostrerà il "now playing".

---

## 7. Osservazioni aggiuntive (oltre le tre aree principali)

| # | Severità | Osservazione | Posizione |
|---|----------|-----------|-------------|
| 12 | 🟠 Media | **Watchdog di buffering = 1.0 s** — estremamente aggressivo. Su una connessione variabile in auto in movimento, un buffering legittimo >1 s provocherà riconnessioni continue e interruzioni audio. Suggerito 8–15 s. | `AudioService.cs:19` |
| 13 | 🟠 Media | **Eccesso di permessi:** `READ_EXTERNAL_STORAGE` e `WRITE_EXTERNAL_STORAGE` dichiarati ma inutilizzati (copertine via HTTP, DB in storage privato). Ignorati su API 30+, e la Play Console li segnala. | `AndroidManifest.xml:31-32` |
| 14 | 🟡 Basso | **Thread-safety di `_reconnectCts`:** `RenewReconnectCts` fa `Cancel()+Dispose()+new`, mentre `PauseAsync`/`StopAsync`/watchdog/connectivity chiamano `Cancel()` da thread diversi → possibile `ObjectDisposedException` (Cancel dopo Dispose). | `AudioService.cs:448-453, 110, 150` |
| 15 | 🟡 Basso | **Incoerenza nella documentazione:** il README dice "Android 8.0+ (API 21)" — API 21 è Android 5.0; il minSdk reale è **26** (Android 8.0). Fuorviante. | `README.md`, manifest |
| 16 | 🟡 Basso | **Codice morto / TODO:** il consenso al crash reporting ha un `return;` prima del dialog (`App.xaml.cs:81-82`) — da cui il warning del compilatore CS0162. Funzione disabilitata di proposito "per la produzione" ma lascia un percorso morto. | `App.xaml.cs:81` |
| 17 | 🟢 Info | Sentry viene inizializzato solo quando il DSN non è vuoto (`IsConfigured`) — un `AppSecrets.cs` vuoto è sicuro, nessun crash. Buona difensività. | `MauiProgram.cs:38`, `CrashReportingConfiguration.cs` |

---

## 8. Cosa è fatto bene (punti di forza)

Per equilibrio — il progetto ha pregi concreti:

- **Meccanismo di riconnessione robusto:** watchdog ogni 10 s, riconnessione al cambio di connettività, probing di più URL in parallelo con scelta del primo raggiungibile (`ProbeFirstReachableAsync`) e priorità all'ultimo URL funzionante. Approccio maturo allo streaming mobile instabile.
- **Gestione consapevole della "pausa" in diretta:** la pausa chiude la connessione e la riapre alla ripresa, invece di riprodurre da un buffer non più sincronizzato (`AudioService.cs:97-139`) — corretto per la radio in diretta.
- **Separazione pulita delle piattaforme** tramite `IPlatformNowPlayingService` (Android/iOS/Null) e dependency injection (`MauiProgram.cs:108-114`).
- **Gestione corretta dello swipe-to-dismiss** e pulizia dello stato (`AudioLifecycleService`, `StopImmediate`).
- **Buoni commenti sostanziali** nel codice che spiegano le decisioni di design (una rarità).
- **`_reconnectGuard` tramite `Interlocked`** — consapevolezza della concorrenza.
- L'app **funziona davvero** — lo stream va, metadati e copertina si aggiornano (confermato sull'emulatore).

---

## 9. Raccomandazioni (prioritizzate)

**Priorità 0 — prima di qualsiasi certificazione AA:**
1. **Spostare il player dalla UI al servizio** e unificare a **una sola** `MediaSession` (idealmente **Media3 `MediaLibraryService`**). Risolve contemporaneamente i bug #4, #5, #6, #11 e la nota #3.
2. **Aggiungere la gestione dell'audio focus** (#9) e **ACTION_AUDIO_BECOMING_NOISY** (#10) — senza questi, l'uso in auto è fastidioso e rischia il rifiuto nella revisione Google.
3. **Correggere il percorso `startForeground`** (#1) — pubblicare sempre una notifica o non avviare come foreground; racchiudere l'avvio in try/catch (#2).

**Priorità 1:**
4. Allentare il timeout del watchdog di buffering a 8–15 s (#12).
5. Rimuovere i permessi storage inutilizzati (#13).
6. Estendere/correggere l'allowlist dei chiamanti AA + validazione della firma (#7).

**Priorità 2 (pulizia):**
7. Migliorare la thread-safety di `_reconnectCts` (#14), correggere il README (#15), rimuovere il codice morto del consenso (#16), aggiungere gli hint di content style per AA (#8).

**Percorso di produzione AA/BT:** dopo quanto sopra — pubblicazione su Google Play + invio al programma Android for Cars (categoria media) e superamento della revisione Google. Finché ciò non avviene, AA funziona esclusivamente in modalità sviluppatore con "origini sconosciute" (sezione 2).

---

## 10. Verdetto finale

| Criterio | Valutazione (1–5) |
|-----------|:-----------:|
| Core di riproduzione / streaming | ★★★★☆ |
| Servizio in background | ★★★☆☆ |
| Android Auto (stato attuale) | ★★☆☆☆ |
| Bluetooth / controllo in auto | ★★☆☆☆ |
| Prontezza alla certificazione AA | ★☆☆☆☆ |
| Qualità e igiene del codice | ★★★★☆ |

**Riepilogo:** un core di riproduzione radio ben scritto con gestione matura delle reti instabili, ma **lo strato di integrazione con l'auto (AA/BT/servizio) necessita di una riprogettazione**. La causa principale della maggior parte dei problemi è un'unica decisione architetturale — **il player agganciato alla UI invece che al servizio** — più **tre MediaSession divergenti** e **l'assenza di audio focus / gestione della disconnessione BT**. Inoltre, indipendentemente dal codice, **l'app non comparirà in Android Auto senza certificazione Google e distribuzione tramite Play** — attualmente solo in modalità sviluppatore AA con origini sconosciute abilitate.

Una correzione P0 (servizio unico + Media3 + audio focus) porterebbe la valutazione AA/BT da 2/5 a un livello idoneo per la revisione di certificazione.

---

## 11. Codice delle correzioni — dove e cosa modificare

Di seguito le patch concrete per ciascun bug delle sezioni 4–7. Le correzioni #1, #2, #9, #10, #12, #13, #14, #15, #16 sono complete e pronte da incollare. Le correzioni #4 (un ponte pragmatico) e #5/#6/#11 (unificazione delle sessioni / Media3) sono indicate come richiedenti una modifica più ampia — vengono forniti una direzione e codice ponte.

### 11.1 🔴 #1 — `startForeground` garantito (eliminazione del crash)
**File:** `Platforms/Android/Services/AndroidMediaNotificationService.cs`
**Cosa fare:** prima che il servizio possa decidere di fermarsi, entrare **sempre** nello stato foreground (placeholder se mancano i metadati). Requisito di sistema: dopo `startForegroundService`, `startForeground()` va chiamato entro 5 s.

```csharp
// 1) All'inizio di OnStartCommand — prima di gestire le azioni:
public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
{
    // GARANZIA: se avviato tramite startForegroundService, DOBBIAMO entrare in foreground
    // entro <5 s, altrimenti il sistema lancia ForegroundServiceDidNotStartInTimeException.
    EnsureForegroundStarted();

    string? action = intent?.Action;
    switch (action)
    {
        // ... invariato ...
    }

    PublishNotification();
    return StartCommandResult.NotSticky;
}

// 2) Nuovo metodo — pubblica una notifica (placeholder se mancano metadati) ed entra in foreground:
private void EnsureForegroundStarted()
{
    if (_isForeground || _mediaSession is null)
        return;

    (PlatformNowPlayingSnapshot snapshot, Android.Graphics.Bitmap? artwork) = AndroidNowPlayingStateStore.GetState();

    Notification notification =
        (string.IsNullOrWhiteSpace(snapshot.Title) && string.IsNullOrWhiteSpace(snapshot.Artist))
            ? BuildPlaceholderNotification()
            : BuildNotification(snapshot, artwork);

    StartForegroundInternal(notification);
    _isForeground = true;
}

private Notification BuildPlaceholderNotification()
{
    Notification.Builder builder = OperatingSystem.IsAndroidVersionAtLeast(26)
        ? new Notification.Builder(this, ChannelId)
        : new Notification.Builder(this);

    return builder
        .SetContentTitle("RadioE45")
        .SetSmallIcon(Resource.Mipmap.appicon)
        .SetOnlyAlertOnce(true)
        .SetOngoing(false)
        .Build()!;
}
```

> Dopo l'ingresso in foreground, `PublishNotification()` può chiamare in sicurezza `StopServiceNotification()` su uno snapshot vuoto — `StopForeground(Remove)` rimuoverà il placeholder senza crash.

### 11.2 🟠 #2 — Avvio FGS sicuro dal background (Android 12+)
**File:** `Platforms/Android/Services/AndroidMediaNotificationService.cs`, metodo `StartService` (`cs:82-91`).
**Cosa fare:** racchiudere l'avvio in `try/catch` — un avvio FGS dal background su API 31+ può lanciare `ForegroundServiceStartNotAllowedException`.

```csharp
private static void StartService(Context context, string action, bool preferForeground)
{
    Intent intent = new(context, typeof(AndroidMediaNotificationService));
    intent.SetAction(action);

    try
    {
        if (preferForeground && OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
    }
    catch (Exception ex) when (
        ex is Java.Lang.IllegalStateException ||           // ForegroundServiceStartNotAllowed (API 31+)
        ex is Android.App.ForegroundServiceStartNotAllowedException)
    {
        // Nessun permesso di avvio dal background — il controllo dovrebbe passare per la MediaSession attiva.
        System.Diagnostics.Debug.WriteLine($"FGS start blocked: {ex.Message}");
    }
}
```

### 11.3 🔴 #4 — Play da Android Auto con la UI chiusa (ponte pragmatico)
**File:** `Services/Audio/AudioService.cs` + `Platforms/Android/Services/RadioMediaBrowserService.cs`
**Cosa fare:** il player è agganciato alla UI, quindi su un comando AA bisogna (a) ricordare la richiesta e riprodurla dopo l'aggancio del `MediaElement`, e (b) forzare l'avvio dell'activity che chiama `Initialize()`. È un **ponte**; a tendere il player appartiene al servizio (Media3, punto 11.5).

```csharp
// AudioService.cs — nuovo campo:
private AzuraStation? _pendingStation;

// AudioService.cs — PlayAsync: gestire il MediaElement mancante invece di un return silenzioso:
public async Task PlayAsync(AzuraStation station)
{
    _currentStation = station;
    _shouldBePlaying = true;
    _bufferingStartedAt = DateTime.MinValue;

    if (_mediaElement is null)
    {
        // Comando da Android Auto / BT con la UI non inizializzata:
        // ricorda la richiesta — Initialize() la completerà.
        _pendingStation = station;
        return;
    }

    RenewReconnectCts();
    Interlocked.Exchange(ref _reconnectGuard, 0);
    TryQueueReconnect();
}

// AudioService.cs — alla fine di Initialize(), dopo l'aggancio degli eventi e il blocco _shouldBePlaying:
if (_pendingStation is not null)
{
    AzuraStation pending = _pendingStation;
    _pendingStation = null;
    _ = PlayAsync(pending);   // ora _mediaElement != null, partirà normalmente
}
```

```csharp
// RadioMediaBrowserService.cs — in AutoMediaCallback forza la creazione della UI (quindi Initialize):
private static void EnsureUiLaunched()
{
    Context ctx = Android.App.Application.Context;
    Intent intent = new(ctx, typeof(MainActivity));
    intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
    try { ctx.StartActivity(intent); } catch { /* l'avvio di activity dal background può essere limitato */ }
}

// all'inizio di OnPlay() e OnPlayFromMediaId():
public override void OnPlay()
{
    EnsureUiLaunched();
    // ... resto invariato ...
}
```

> **Avvertenza:** l'avvio di un'activity dal background è limitato su Android 10+ e non sempre riesce. La piena affidabilità si ottiene solo spostando il player nel servizio (11.5).

### 11.4 🔴 #9 — Audio focus (ducking / pausa su navigazione e chiamate)
**Nuovo file:** `Platforms/Android/Services/AudioFocusManager.cs`
**Cosa fare:** richiedere l'audio focus all'avvio della riproduzione, rilasciarlo allo stop; reagire alla perdita (pausa/ducking) e al recupero (ripresa).

```csharp
using Android.Content;
using Android.Media;
using RadioE45.Services.Audio;

namespace RadioE45;

public sealed class AudioFocusManager : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
{
    private readonly AudioManager _audioManager;
    private readonly IAudioService _audio;
    private AudioFocusRequestClass? _focusRequest;
    private bool _resumeOnFocusGain;

    public AudioFocusManager(Context context, IAudioService audio)
    {
        _audioManager = (AudioManager)context.GetSystemService(Context.AudioService)!;
        _audio = audio;
    }

    public bool RequestFocus()
    {
        AudioFocusRequest result;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            AudioAttributes attributes = new AudioAttributes.Builder()!
                .SetUsage(AudioUsageKind.Media)!
                .SetContentType(AudioContentType.Music)!
                .Build()!;

            _focusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)!
                .SetAudioAttributes(attributes)!
                .SetOnAudioFocusChangeListener(this)!
                .SetWillPauseWhenDucked(false)!   // gestiamo il ducking noi stessi
                .Build()!;

            result = _audioManager.RequestAudioFocus(_focusRequest);
        }
        else
        {
#pragma warning disable CS0618
            result = _audioManager.RequestAudioFocus(this, Stream.Music, AudioFocus.Gain);
#pragma warning restore CS0618
        }
        return result == AudioFocusRequest.Granted;
    }

    public void AbandonFocus()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && _focusRequest is not null)
            _audioManager.AbandonAudioFocusRequest(_focusRequest);
#pragma warning disable CS0618
        else if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            _audioManager.AbandonAudioFocus(this);
#pragma warning restore CS0618
    }

    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        switch (focusChange)
        {
            case AudioFocus.Loss:                  // perdita permanente — stop logico
                _resumeOnFocusGain = false;
                _ = _audio.PauseAsync();
                break;
            case AudioFocus.LossTransient:         // chiamata / breve interruzione
                _resumeOnFocusGain = _audio.IsPlaying;
                _ = _audio.PauseAsync();
                break;
            case AudioFocus.LossTransientCanDuck:  // es. annuncio di navigazione
                _audio.SetVolume(0.2);
                break;
            case AudioFocus.Gain:
                _audio.SetVolume(1.0);             // NOTA: ripristinare al volume dell'utente (vedi sotto)
                if (_resumeOnFocusGain)
                {
                    _resumeOnFocusGain = false;
                    _ = _audio.ResumeAsync();
                }
                break;
        }
    }
}
```

**Collegamento:** registrare in DI e chiamarlo attorno alla riproduzione.

```csharp
// MauiProgram.cs (sezione ANDROID, accanto alla registrazione di IPlatformNowPlayingService):
builder.Services.AddSingleton(sp =>
    new AudioFocusManager(Android.App.Application.Context, sp.GetRequiredService<IAudioService>()));
```

```csharp
// AudioService.cs — iniettare e usare:
//   costruttore: aggiungere un parametro (su Android) o recuperarlo lazy da IPlatformApplication.Current.Services
//   RequestFocus() prima di aprire lo stream (in TryOpenStreamAsync, dopo aver scelto winner):
if (!_focus.RequestFocus())   // _focus = AudioFocusManager
    return;                   // niente focus — non avviare
//   AbandonFocus() in StopAsync()/StopImmediate()/Shutdown().
```

> **Importante:** ducking e `Gain` ripristinano un valore fisso `1.0`. Bisogna ricordare il volume precedente dell'utente (es. un campo `_userVolume` aggiornato in `SetVolume`) e ripristinare quello, invece di `1.0`.

### 11.5 🔴 #10 — Reazione alla disconnessione BT (ACTION_AUDIO_BECOMING_NOISY)
**Nuovo file:** `Platforms/Android/Services/BecomingNoisyReceiver.cs` + registrazione nel servizio di notifica.

```csharp
using Android.Content;
using Android.Media;
using RadioE45.Services.Audio;

namespace RadioE45;

public sealed class BecomingNoisyReceiver : BroadcastReceiver
{
    private readonly IAudioService _audio;
    public BecomingNoisyReceiver(IAudioService audio) => _audio = audio;

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action == AudioManager.ActionAudioBecomingNoisy)
            _ = _audio.PauseAsync();   // BT/cuffie scollegati → pausa, non l'altoparlante del telefono
    }
}
```

```csharp
// AndroidMediaNotificationService.cs — campo + registrazione in OnCreate, deregistrazione in OnDestroy:
private BecomingNoisyReceiver? _noisyReceiver;

public override void OnCreate()
{
    base.OnCreate();
    // ... codice esistente ...

    IAudioService? audio = IPlatformApplication.Current?.Services?.GetService<IAudioService>();
    if (audio is not null)
    {
        _noisyReceiver = new BecomingNoisyReceiver(audio);
        IntentFilter filter = new(AudioManager.ActionAudioBecomingNoisy);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            RegisterReceiver(_noisyReceiver, filter, ReceiverFlags.NotExported);
        else
            RegisterReceiver(_noisyReceiver, filter);
    }
}

public override void OnDestroy()
{
    if (_noisyReceiver is not null)
    {
        UnregisterReceiver(_noisyReceiver);
        _noisyReceiver = null;
    }
    // ... codice esistente ...
}
```

### 11.6 🔴 #5 / #11 — Una MediaSession condivisa (ponte) + #6 Media3 (a tendere)
**File:** `AndroidMediaNotificationService.cs`, `RadioMediaBrowserService.cs`
**Cosa fare (ponte):** eliminare la sessione separata `"RadioE45Auto"` nel browser-service e puntare al token dell'unica sessione `"RadioE45Playback"`. Così AA, schermata di blocco e pulsanti BT usano un'unica fonte di verità.

```csharp
// Nuovo file: Platforms/Android/Services/SharedMediaSessionHolder.cs
using Android.Media.Session;
namespace RadioE45;

internal static class SharedMediaSessionHolder
{
    public static MediaSession? Session { get; set; }
}
```

```csharp
// AndroidMediaNotificationService.cs — in OnCreate dopo la creazione di _mediaSession:
SharedMediaSessionHolder.Session = _mediaSession;
// in OnDestroy prima di Release():
SharedMediaSessionHolder.Session = null;
```

```csharp
// RadioMediaBrowserService.cs — NON creare una sessione propria; usare quella condivisa:
public override void OnCreate()
{
    base.OnCreate();
    _logger = IPlatformApplication.Current?.Services?.GetService<ILogger<RadioMediaBrowserService>>();

    // Assicurarsi che il servizio di notifica (proprietario della sessione) sia attivo e il token disponibile.
    AndroidMediaNotificationService.RequestRefresh(this);

    MediaSession? shared = SharedMediaSessionHolder.Session;
    if (shared is not null)
        SessionToken = shared.SessionToken;

    AndroidNowPlayingStateStore.SnapshotChanged += OnSnapshotChanged;
}
```

> **Avvertenza sull'ordine:** se il browser-service parte prima del servizio di notifica, `SessionToken` può essere temporaneamente `null` — da cui il `RequestRefresh` sopra, e a tendere conviene comunque migrare a Media3.
>
> **#6 — la soluzione definitiva (Media3):** invece del framework `MediaSession` + `MediaBrowserService`, migrare a `androidx.media3.session.MediaLibraryService` con **una sola** `MediaSession` e `ExoPlayer` **nel servizio**. Questo risolve #3, #4, #5, #6, #11 in una volta (player indipendente dalla UI, sessione unica, esenzione nativa dalla restrizione FGS, audio focus corretto). In .NET MAUI richiede i binding `Xamarin.AndroidX.Media3.*`. Sforzo: significativo (riprogettazione dello strato audio su Android), ma è l'unico percorso verso la certificazione AA.

### 11.7 🟠 #12 — Allentare il watchdog di buffering
**File:** `Services/Audio/AudioService.cs:19`

```csharp
// era:  private const double BufferingTimeoutSeconds = 1.0;
private const double BufferingTimeoutSeconds = 12.0;   // tolleranza per una connessione variabile in auto
```

### 11.8 🟠 #13 — Rimuovere i permessi storage inutilizzati
**File:** `Platforms/Android/AndroidManifest.xml:31-32` — rimuovere entrambe le righe:

```xml
<!-- RIMUOVERE — inutilizzati, segnalati dalla Play Console: -->
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
```

### 11.9 🟡 #14 — Thread-safety di `_reconnectCts`
**File:** `Services/Audio/AudioService.cs` — proteggere il CTS con un lock; non leggere il token dopo Dispose.

```csharp
private readonly object _ctsLock = new();
private CancellationTokenSource _reconnectCts = new();

private void RenewReconnectCts()
{
    lock (_ctsLock)
    {
        _reconnectCts.Cancel();
        _reconnectCts.Dispose();
        _reconnectCts = new CancellationTokenSource();
    }
}

private void CancelReconnect()
{
    lock (_ctsLock) { _reconnectCts.Cancel(); }
}

private CancellationToken CurrentReconnectToken()
{
    lock (_ctsLock) { return _reconnectCts.Token; }
}
```

Poi sostituire le chiamate dirette:
- `_reconnectCts.Cancel();` → `CancelReconnect();` (in `PauseAsync` `cs:110`, `StopAsync` `cs:150`, `StopImmediate` `cs:173`),
- la lettura `var ct = _reconnectCts.Token;` → `var ct = CurrentReconnectToken();` (in `TryReconnectAsync` `cs:393`).
(In `Shutdown` mantenere Cancel+Dispose, ma anche sotto `_ctsLock`.)

### 11.10 🟡 #15 — Correggere il README
**File:** `README.md`

```diff
- - Android 8.0+ (API 21)
+ - Android 8.0+ (API 26)
```

### 11.11 🟡 #16 — Rimuovere il codice morto del consenso al crash reporting
**File:** `App.xaml.cs:80-82` — rimuovere il `return;` prematuro (elimina anche il warning CS0162), oppure lasciare di proposito la funzione disabilitata finché non è pronta per la produzione. Se deve funzionare:

```csharp
Page? hostPage = await GetPromptHostPageAsync();
if (hostPage is null)
    return;
// RIMUOVERE la riga "return;" sottostante (e il commento TODO):
// return;
bool enabled = await hostPage.DisplayAlertAsync( ... );
```

---

### Ordine di implementazione (mappatura alle priorità della sezione 9)
1. **P0:** 11.1 (#1), 11.2 (#2), 11.4 (#9), 11.5 (#10) — rapidi, indipendenti, eliminano il crash e la peggiore UX in auto.
2. **P0 (più ampio):** 11.6 — a tendere Media3 (risolve #3/#4/#5/#6/#11). Fino alla migrazione: il ponte 11.3 (#4) + la sessione condivisa 11.6.
3. **P1/P2:** 11.7 (#12), 11.8 (#13), 11.9 (#14), 11.10 (#15), 11.11 (#16) — pulizia, basso rischio.
