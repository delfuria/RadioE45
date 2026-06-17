# RadioE45 — Piano di Implementazione: Android Auto, Bluetooth, Background Service

**Basato su:** `ResumeRadioE45_EN.md` (analisi tecnica esterna, 2026-06-14)
**Redatto:** 2026-06-17
**Scope:** Fasi 0–2 (Fase 3 / migrazione Media3 rinviata a release futura)
**Piattaforma primaria:** Android · **Altre piattaforme:** iOS, macOS, Windows (impatto verificato)

---

## Stato attuale

Tutti i bug descritti nell'analisi esterna sono **confermati presenti** nel codice alla data di redazione di questo documento.

| Area | Rating attuale |
|---|:---:|
| Background service (foreground) | 🟠 Medio |
| Android Auto | 🔴 Basso / rischioso |
| Bluetooth / media button | 🔴 Basso |
| Core audio (qualità codice) | 🟢 Alto |

---

## Legenda

| Simbolo | Significato |
|---|---|
| 🟢 | Rischio nullo |
| 🟡 | Rischio basso — file isolato o logica additiva |
| 🟠 | Rischio medio — tocca path critici ma con perimetro delimitato |
| 🔴 | Rischio alto — cambiamento architetturale |
| ★☆☆☆☆ | Difficoltà minima |
| ★★★★★ | Difficoltà massima |

---

## Considerazioni cross-platform

Prima di entrare nel dettaglio delle fasi, un riepilogo dell'impatto sulle piattaforme non-Android.

I file sotto `Platforms/Android/` sono compilati **esclusivamente** per Android — iOS, macOS e Windows non li vedono. Le uniche eccezioni sono i file condivisi:

| Fix | File condiviso | Impatto su iOS/macOS/Windows |
|---|---|---|
| #12 | `AudioService.cs` | Toccato — benefico (timeout meno aggressivo ovunque) |
| #14 | `AudioService.cs` | Toccato — benefico (thread safety su tutte le piattaforme) |
| #4  | `AudioService.cs` | Toccato — no-op (nessun caller esterno su desktop/iOS) |
| #9  | `AudioService.cs` + `MauiProgram.cs` | **Richiede pattern IAudioFocusManager** (vedi Fase 1.C) |

**Soluzione per Bug #9:** aggiungere `IAudioFocusManager` + `NullAudioFocusManager` (sempre-granted) con registrazione DI condizionale in `MauiProgram.cs` — identico al pattern già usato per `IPlatformNowPlayingService`. Nessun `#if` in `AudioService.cs`.

---

## Fase 0 — Pulizia (stima: 15–30 min)

Modifiche di 1–2 righe, zero impatto su logica runtime. Tutti in un unico commit.

### #12 — Watchdog timeout troppo aggressivo
- **File:** `Services/Audio/AudioService.cs:19`
- **Modifica:** `BufferingTimeoutSeconds = 1.0` → `12.0`
- **Motivazione:** 1 secondo causa reconnect continui su connessione variabile in auto
- **Piattaforme:** tutte (benefico ovunque)
- **Difficoltà:** ★☆☆☆☆ · **Rischio:** 🟢

### #13 — Permessi storage non utilizzati
- **File:** `Platforms/Android/AndroidManifest.xml:31-32`
- **Modifica:** rimuovere `READ_EXTERNAL_STORAGE` e `WRITE_EXTERNAL_STORAGE`
- **Motivazione:** inutilizzati, ignorati su API 30+, segnalati dalla Play Console
- **Piattaforme:** Android only
- **Difficoltà:** ★☆☆☆☆ · **Rischio:** 🟢

### #15 — README: versione API errata
- **File:** `README.md`
- **Modifica:** "Android 8.0+ (API 21)" → "Android 8.0+ (API 26)"
- **Piattaforme:** documentazione
- **Difficoltà:** ★☆☆☆☆ · **Rischio:** 🟢

---

## Fase 1 — Fix standalone Android (stima: 2–3 ore)

Ogni fix è indipendente dagli altri, committabile separatamente. L'ordine consigliato è A → B → D → C (C per ultima perché richiede device fisico per test completo).

### 1.A — #2: try/catch su `StartForegroundService`
- **File:** `Platforms/Android/Services/AndroidMediaNotificationService.cs` — metodo `StartService` (riga 82)
- **Modifica:** wrappare in `try/catch` le chiamate `StartForegroundService`/`StartService` per gestire `ForegroundServiceStartNotAllowedException` (Android 12+, API 31+)
- **Motivazione:** un avvio da background senza catch causa crash non gestito
- **Piattaforme:** Android only
- **Difficoltà:** ★☆☆☆☆ · **Rischio:** 🟢
- **Test su emulatore:** ✅ verificabile

```csharp
// Patch da applicare in StartService():
try
{
    if (preferForeground && OperatingSystem.IsAndroidVersionAtLeast(26))
        context.StartForegroundService(intent);
    else
        context.StartService(intent);
}
catch (Exception ex) when (
    ex is Java.Lang.IllegalStateException ||
    ex is Android.App.ForegroundServiceStartNotAllowedException)
{
    System.Diagnostics.Debug.WriteLine($"FGS start blocked: {ex.Message}");
}
```

---

### 1.B — #10: `BecomingNoisyReceiver` (BT disconnect / cuffie staccate)
- **File nuovo:** `Platforms/Android/Services/BecomingNoisyReceiver.cs`
- **File modificato:** `AndroidMediaNotificationService.cs` (solo `OnCreate`/`OnDestroy`)
- **Modifica:** nuovo `BroadcastReceiver` per `ACTION_AUDIO_BECOMING_NOISY` → chiama `PauseAsync()`; registrato/deregistrato nel ciclo di vita del servizio
- **Motivazione:** senza questo fix, disconnettere BT o cuffie fa saltare l'audio sullo speaker a pieno volume
- **Piattaforme:** Android only
- **Difficoltà:** ★★☆☆☆ · **Rischio:** 🟡
- **Test su emulatore:** ⚠️ parziale (simulabile via ADB: `adb shell input keyevent KEYCODE_HEADSETHOOK`); test completo richiede device fisico

---

### 1.C — #9: `AudioFocusManager` (audio focus / ducking)
- **File nuovo:** `Platforms/Android/Services/AudioFocusManager.cs`
- **File nuovo:** `Services/Audio/IAudioFocusManager.cs` (interfaccia condivisa)
- **File nuovo:** `Services/Audio/NullAudioFocusManager.cs` (no-op per iOS/macOS/Windows)
- **File modificato:** `MauiProgram.cs` (registrazione DI condizionale `#if ANDROID`)
- **File modificato:** `AudioService.cs` (inject `IAudioFocusManager`, chiamare `RequestFocus` prima di aprire lo stream, `AbandonFocus` in `StopAsync`/`Shutdown`, save/restore volume per ducking)
- **Modifica:** gestire `RequestAudioFocus` / `AbandonAudioFocus`, reagire a `AudioFocus.Loss` / `LossTransient` / `LossTransientCanDuck` / `Gain`
- **Motivazione:** senza audio focus, navigazione GPS e chiamate non ducking/pausano la radio — causa comune di rejection nel review AA
- **Piattaforme:** Android only per implementazione; interfaccia su tutte (null-safe)
- **Difficoltà:** ★★★☆☆ · **Rischio:** 🟠
- **Test su emulatore:** ⚠️ limitato (focus grant/deny verificabile con AA Media Simulator); ducking reale su chiamate/navigazione richiede device fisico
- **Nota:** memorizzare il volume utente in un campo `_userVolume` invece di ripristinare hardcoded `1.0` dopo il duck

---

### 1.D — #14: Thread-safety di `_reconnectCts`
- **File:** `Services/Audio/AudioService.cs`
- **Modifica:** aggiungere `_ctsLock`, tre metodi helper (`RenewReconnectCts`, `CancelReconnect`, `CurrentReconnectToken`), sostituire 4 accessi diretti a `_reconnectCts`
- **Motivazione:** `Cancel()` dopo `Dispose()` da thread diversi può causare `ObjectDisposedException`
- **Piattaforme:** tutte (migliora thread safety ovunque)
- **Difficoltà:** ★★☆☆☆ · **Rischio:** 🟠
- **Test su emulatore:** ✅ verificabile — testare pause/resume rapidi e cambio rete

```csharp
// Struttura da implementare in AudioService.cs:
private readonly object _ctsLock = new();

private void RenewReconnectCts() { lock (_ctsLock) { _reconnectCts.Cancel(); _reconnectCts.Dispose(); _reconnectCts = new(); } }
private void CancelReconnect() { lock (_ctsLock) { _reconnectCts.Cancel(); } }
private CancellationToken CurrentReconnectToken() { lock (_ctsLock) { return _reconnectCts.Token; } }
```

---

## Fase 2 — Bridge architetturale (stima: 4–6 ore)

**Questa è la soluzione di produzione** (non un bridge temporaneo), poiché la migrazione Media3 è rinviata. L'ordine è vincolato: **2.A prima di 2.B**, poi 2.C e 2.D in parallelo.

### 2.A — #1: Garantire `startForeground` sempre chiamato ⚠️ Fix più critico
- **File:** `Platforms/Android/Services/AndroidMediaNotificationService.cs`
- **Modifica:** aggiungere `EnsureForegroundStarted()` all'inizio di `OnStartCommand`; se lo snapshot è vuoto, pubblicare una notifica placeholder ("RadioE45") prima di decidere se fermarsi
- **Motivazione:** senza questo fix, se `startForegroundService` viene chiamato con snapshot vuoto (tipico nei primi ~500ms di avvio stream), il sistema lancia `ForegroundServiceDidNotStartInTimeException` — crash del processo
- **Piattaforme:** Android only
- **Difficoltà:** ★★☆☆☆ · **Rischio:** 🟠
- **Test su emulatore:** ✅ — testare esplicitamente: avviare lo stream con stazione senza metadati "now playing"
- **Ordine:** **deve precedere 2.B**

---

### 2.B — #5/#11: `SharedMediaSessionHolder` (sessione unica)
- **File nuovo:** `Platforms/Android/Services/SharedMediaSessionHolder.cs`
- **File modificato:** `AndroidMediaNotificationService.cs` (assegna `Session` in `OnCreate`, null in `OnDestroy`)
- **File modificato:** `RadioMediaBrowserService.cs` (rimuove creazione propria sessione `"RadioE45Auto"`, usa `SessionToken` della sessione condivisa)
- **Motivazione:** tre sessioni MediaSession attive causano routing BT ambiguo e stato inconsistente; AA, lock screen e BT devono vedere una singola fonte di verità
- **Piattaforme:** Android only
- **Difficoltà:** ★★★☆☆ · **Rischio:** 🟠
- **Race condition:** se `RadioMediaBrowserService.OnCreate` gira prima che la sessione esista, sottoscrivere `SessionReady` per assegnare `SessionToken` in modo asincrono
- **Test su emulatore:** ✅ — verificare con AA Media Simulator che Play/Pause dal simulatore controllino la radio

```csharp
// SharedMediaSessionHolder.cs
internal static class SharedMediaSessionHolder
{
    private static MediaSession? _session;
    public static event Action<MediaSession>? SessionReady;

    public static MediaSession? Session
    {
        get => _session;
        set { _session = value; if (value is not null) SessionReady?.Invoke(value); }
    }
}
```

---

### 2.C — #4: `_pendingStation` bridge (Play da AA con UI chiusa)
- **File:** `Services/Audio/AudioService.cs` (campo `_pendingStation`, modifica a `PlayAsync` e `Initialize`)
- **File:** `Platforms/Android/Services/RadioMediaBrowserService.cs` (metodo `EnsureUiLaunched` in `OnPlay`/`OnPlayFromMediaId`)
- **Modifica:** se `PlayAsync` viene chiamato con `_mediaElement == null`, memorizzare la stazione in `_pendingStation`; quando `Initialize()` viene chiamato dalla UI, eseguire il play pendente
- **Motivazione:** senza questo fix, Play da Android Auto con UI non avviata fa silenziosamente nulla
- **Piattaforme:** `AudioService.cs` condiviso — no-op su iOS/macOS/Windows (nessun caller esterno su desktop)
- **Difficoltà:** ★★★☆☆ · **Rischio:** 🟠
- **Limitazione nota:** `EnsureUiLaunched()` (lancio Activity da background) è **inaffidabile su Android 10+** per policy di sistema. L'app funziona correttamente se la UI è già avviata; un cold-start puramente da AA potrebbe non avviare la riproduzione. Soluzione definitiva: migrazione Media3 (Fase 3, fuori scope).
- **Test su emulatore:** ✅ parziale — testare con UI in foreground; cold-start AA non verificabile senza device fisico

---

### 2.D — #7/#8: Allowlist callers + content style hints (30 min)
- **File:** `Platforms/Android/Services/RadioMediaBrowserService.cs`

**Bug #7 — Allowlist troppo ristretta:**
- Aggiungere a `AllowedCallers`: `com.google.android.carassistant`, `com.android.bluetooth`, `com.google.android.wearable.app`
- Difficoltà: ★☆☆☆☆ · Rischio: 🟡

**Bug #8 — Nessun content style hint:**
- Restituire `extras` con `CONTENT_STYLE_BROWSABLE_HINT` / `CONTENT_STYLE_PLAYABLE_HINT` in `OnGetRoot`
- Migliora il layout lista in AA (cosmetic, impatta rating certificazione)
- Difficoltà: ★☆☆☆☆ · Rischio: 🟢

---

## Fase 3 — Migrazione Media3 (release futura, fuori scope attuale)

La migrazione a `androidx.media3.session.MediaLibraryService` con ExoPlayer nel service risolve definitivamente i bug #3, #4, #5, #6, #11. Le fasi 0–2 non creano debito tecnico bloccante per questa migrazione — i bridge della Fase 2 vengono rimossi e sostituiti.

**Prerequisiti prima di avviare Fase 3:**
- NuGet `Xamarin.AndroidX.Media3.Session`, `Xamarin.AndroidX.Media3.ExoPlayer`
- Device fisico Android per testing
- Pubblicazione su Google Play (requisito per distribuzione AA production)

---

## Ordine di esecuzione raccomandato

```
Fase 0   ──▶  #12, #13, #15                     (1 commit, 15 min)
  │
  ▼
Fase 1A  ──▶  #2  try/catch FGS                 (basso rischio, iniziare qui)
Fase 1B  ──▶  #10 BecomingNoisyReceiver
Fase 1D  ──▶  #14 CTS thread-safety
Fase 1C  ──▶  #9  AudioFocusManager             (ultimo, test device fisico)
  │
  ▼
Fase 2A  ──▶  #1  EnsureForegroundStarted        (PRIMA di 2B — obbligatorio)
Fase 2B  ──▶  #5/#11 SharedMediaSessionHolder
Fase 2C  ──▶  #4  _pendingStation bridge
Fase 2D  ──▶  #7/#8 allowlist + hints            (parallelo a 2C)
  │
  ▼
Fase 3   ──▶  Media3 migration                   (release futura)
```

---

## Riepilogo bug per priorità

| Priorità | Bug | Descrizione | Fase | Rischio |
|---|---|---|---|---|
| P0 | #1 | `startForeground` crash su snapshot vuoto | 2A | 🟠 |
| P0 | #2 | FGS start da background senza catch | 1A | 🟢 |
| P0 | #9 | Nessun audio focus (ducking/pausa) | 1C | 🟠 |
| P0 | #10 | Nessuna reazione a BT disconnect | 1B | 🟡 |
| P1 | #4 | Play da AA con UI chiusa fa nulla | 2C | 🟠 |
| P1 | #5/#11 | Tre MediaSession indipendenti, routing BT ambiguo | 2B | 🟠 |
| P1 | #12 | Watchdog timeout 1s troppo aggressivo | 0 | 🟢 |
| P1 | #13 | Permessi storage inutilizzati | 0 | 🟢 |
| P2 | #6 | API MediaSession deprecata (→ Media3) | Fase 3 | 🔴 |
| P2 | #7 | Allowlist caller AA troppo ristretta | 2D | 🟡 |
| P2 | #8 | Nessun content style hint per AA | 2D | 🟢 |
| P2 | #14 | Thread-safety `_reconnectCts` | 1D | 🟠 |
| P2 | #15 | README dice API 21 invece di 26 | 0 | 🟢 |

---

## Note finali

- **Certificazione AA:** anche dopo le fasi 0–2, Android Auto funzionerà **esclusivamente in developer mode** con "Unknown sources" abilitato. La distribuzione production richiede pubblicazione su Google Play + review nel programma Android for Cars (media category).
- **Device fisico:** i fix #9 e #10 sono implementabili ora ma richiedono hardware reale per validazione completa (ducking su chiamate, BT disconnect fisico).
- **Build corrente:** debug non firmato con chiave production — ulteriore ragione per cui AA non appare su device utente standard.
