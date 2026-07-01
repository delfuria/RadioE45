# RadioE45 — Piano di Azione Fase 3: Migrazione a Media3 (Android Auto)

**Branch di riferimento:** `Android-Auto-Fase3`
**Basato su:** `docs/carplay/ImplementationPlan_EN.md` (Fase 3, righe 223-231) + `docs/carplay/CarAppQuality.md` (checklist ufficiale Google, aggiornata a giugno 2026)
**Redatto:** 2026-07-01
**Stato di partenza verificato nel codice:** Fase 0 e Fase 1 (1.A/1.B/1.C/1.D) **completate** su questo branch. **Fase 2 non è stata implementata** (nessuna traccia di `EnsureForegroundStarted`, `SharedMediaSessionHolder`, `_pendingStation`, allowlist estesa). Il branch `Android-Auto-Fase3` indica una decisione esplicita di **saltare la Fase 2** e affrontare direttamente la Fase 3 (Media3), che secondo il piano originale "risolve architetturalmente" gli stessi bug che la Fase 2 avrebbe patchato (#1, #4, #5, #6, #11).

Questo documento è il **piano operativo di riferimento** per l'implementazione: ogni sessione futura di lavoro su questo branch deve partire da qui.

---

## 1. Perché si salta la Fase 2 e si va dritti alla Fase 3

Dal piano originale (righe 223-225):

> "Migrating to `androidx.media3.session.MediaLibraryService` with ExoPlayer in the service definitively resolves bugs #3, #4, #5, #6, #11. [...] the Phase 2 bridges are removed and replaced."

Implementare la Fase 2 ora significherebbe scrivere codice (bridge `_pendingStation`, `SharedMediaSessionHolder`) che la Fase 3 getterebbe via. Ha senso solo se si ha bisogno di uno stabilizzatore rapido prima di un rilascio; qui si è deciso di investire direttamente nell'architettura definitiva.

**Conseguenza pratica:** i bug #1, #4, #5, #11 vanno **verificati come risolti dalla nuova architettura**, non patchati. Il bug #6 (API deprecata) si risolve per definizione abbandonando `android.media.session.MediaSession`. Le fix di Fase 1 (#2, #9, #10, #14) restano valide ma vanno **riportate/adattate** nella nuova architettura (vedi §6).

---

## 2. Stato attuale del codice (audit — verificato leggendo i file, non assunto)

| Componente | File | Ruolo attuale | Destino in Fase 3 |
|---|---|---|---|
| Motore di playback condiviso | `Services/Audio/AudioService.cs` | Wrapper su `CommunityToolkit.Maui.Views.MediaElement`, usato su **tutte** le piattaforme | Resta invariato su iOS/macOS/Windows. **Su Android va sostituito** da un backend nativo Media3 (vedi §4) |
| Contratto condiviso | `Services/Audio/IAudioService.cs` | `Initialize(MediaElement)`, `PlayAsync`, `PauseAsync`, `ResumeAsync`, `StopAsync`, `StopImmediate`, `SetVolume`, `UpdateMetadata`, `Shutdown` + eventi | Il metodo `Initialize(MediaElement)` è specifico di CommunityToolkit e va reso no-op su Android (vedi §4.2) |
| Punto di attacco UI→motore | `Views/OnAirPage.xaml.cs:33` | `_audioService.Initialize(AudioPlayer)` — l'unico punto in cui il motore viene "agganciato" | Root cause del bug #4: senza questa chiamata (UI mai apertA) il motore Android non esiste |
| Notifica media legacy | `Platforms/Android/Services/AndroidMediaNotificationService.cs` | `MediaSession` #1 ("RadioE45Playback"), notifica custom, gestione `StartForegroundService`, `BecomingNoisyReceiver` | **Da eliminare**, sostituito dal `MediaLibraryService` + notifica automatica di Media3 |
| Browsing/Android Auto legacy | `Platforms/Android/Services/RadioMediaBrowserService.cs` | `MediaBrowserService` classico, `MediaSession` #2 ("RadioE45Auto"), `AllowedCallers` con solo 2 entry, nessun content-style hint, nessun `onPlayFromSearch` | **Da eliminare**, sostituito da `MediaLibraryService` (unica sessione) |
| Cleanup su swipe | `Platforms/Android/AudioLifecycleService.cs` | Service separato che intercetta `OnTaskRemoved` per fermare lo stream | Funzionalità da assorbire nel nuovo `MediaLibraryService` (che ha già il proprio ciclo di vita/`onTaskRemoved`) |
| Focus audio | `Platforms/Android/Services/AudioFocusManager.cs` + `IAudioFocusManager` + `NullAudioFocusManager` | Gestione manuale di `AudioFocusRequest`, ducking, `_userVolume` | ExoPlayer gestisce il focus **nativamente** se configurato con `setAudioAttributes(attrs, /*handleAudioFocus=*/true)`. Da valutare se il livello custom diventa ridondante (vedi §6.1) |
| BT/cuffie disconnesse | `Platforms/Android/Services/BecomingNoisyReceiver.cs` | `BroadcastReceiver` custom per `ACTION_AUDIO_BECOMING_NOISY` | ExoPlayer lo gestisce nativamente con `setHandleAudioBecomingNoisy(true)` (vedi §6.2) |
| Thread-safety CTS | `Services/Audio/AudioService.cs` (`_ctsLock`, `RenewReconnectCts`, ecc.) | Protegge `_reconnectCts` da race condition multi-thread | Resta nel codice condiviso (usato da iOS/macOS/Windows); il nuovo backend Android avrà una gestione errori/retry propria, da progettare con lo stesso rigore |
| Manifest | `Platforms/Android/AndroidManifest.xml` | Dichiara 3 service (`AudioLifecycleService`, `AndroidMediaNotificationService`, `RadioMediaBrowserService`) + `meta-data com.google.android.gms.car.application` | Da consolidare in **1 solo service** Media3 |
| Descrittore Auto | `Platforms/Android/Resources/xml/automotive_app_desc.xml` | `<automotiveApp><uses name="media" /></automotiveApp>` | **Resta invariato** — è il descrittore corretto per Android Auto (progettato, non Android Automotive OS embedded) |
| DI | `MauiProgram.cs:109-122` | `IAudioService` registrato **senza** switch per piattaforma (unica implementazione condivisa); `IAudioFocusManager`/`IPlatformNowPlayingService` già seguono il pattern condizionale `#if ANDROID` | Il pattern condizionale esiste già in codebase — va estenso a `IAudioService` (vedi §4.2) |
| Package NuGet | `RadioE45.csproj` | Nessun riferimento esplicito a `Xamarin.AndroidX.Media3.*` | **Scoperta importante**: `CommunityToolkit.Maui.MediaElement 10.0.0` dipende già transitivamente da `Xamarin.AndroidX.Media3.Common/ExoPlayer/Session/ExoPlayer.Hls/ExoPlayer.Dash/ExoPlayer.Rtsp/UI` versione **1.8.0** (verificato in `~/.nuget/packages`). Le librerie Media3 **sono già nell'albero delle dipendenze**: il prerequisito "aggiungere i NuGet Media3" indicato nel piano originale (riga 228) è in gran parte già soddisfatto. Resta comunque buona norma **pinnare esplicitamente** le versioni usate direttamente (vedi §5.1) |

---

## 3. Cross-reference con CarAppQuality.md (checklist ufficiale Google)

RadioE45 è un'app categoria **Media (non templated)** distribuita via **Android Auto proiettato** (non Android Automotive OS embedded — nessun target automotive nel manifest, solo `com.google.android.gms.car.application`). Questo restringe l'applicabilità dei criteri: i criteri "Android Automotive OS only" (AR-1, AR-2, DO-1, PE-1, DD-2/3/4 lato automotive) **non si applicano** allo scope attuale.

### 3.a Criteri applicabili a RadioE45 (Media, Android Auto) — stato attuale

| ID | Descrizione | Stato attuale | Azione richiesta in Fase 3 |
|---|---|---|---|
| EP-1 | L'app funziona come descritto; al relancio ripristina lo stato precedente | ✅ plausibile (stazione/stato salvati) | Verificare con test manuale dopo migrazione |
| EP-2 | (implicito con EP-1 per categoria Media) | ✅ | — |
| EP-4 | Screenshot in-car non alterato caricato su Play Console | ❌ non applicabile ora (nessuna submission) | Checklist di pubblicazione, non di codice |
| MC-1 | L'app integra una MediaSession; supporta play/pause o stop; fornisce titolo e thumbnail per ogni media item. Rif. esplicito a *"Control and advertise playback using a MediaSession"* (`/media/media3/session/control-playback`) | ⚠️ oggi soddisfatto ma con **3 MediaSession concorrenti** (bug #5/#11) — ambiguo per il sistema | **Diventa il cuore della Fase 3**: un'unica `MediaLibrarySession` Media3 |
| MA-1 | Nessun autoplay senza azione utente | ✅ verificato: `OnPlay()` richiede comando esplicito | Preservare lo stesso comportamento nel nuovo `MediaLibrarySession.Callback` |
| VD-1/VD-2/VD-3 | Icone/colori con contrasto adeguato, set di icone bianche colorizzabili dal sistema | ⚠️ da verificare: le icone stazione (`station.LogoUrl`) e le icone di trasporto (`IcMediaPause`/`IcMediaPlay` di sistema) — quelle di sistema sono OK, gli **artwork remoti** delle stazioni vanno verificati per contrasto minimo quando renderizzati su sfondo scuro/chiaro auto | Non bloccante per il codice, ma da annotare come check manuale pre-submission |
| GB-1 | Elementi disabilitati devono essere non funzionanti | ⚠️ da verificare nel nuovo `Player.Commands` (Media3 gestisce automaticamente l'enable/disable dei bottoni lato host in base ai comandi disponibili sul `Player`) | Configurare correttamente `Player.AvailableCommands` |
| IN-1 | Notifiche solo se rilevanti | ✅ (solo notifica di playback) | Il `DefaultMediaNotificationProvider` di Media3 rispetta questo di default |
| NA-1 | Nessuna pubblicità nelle notifiche | ✅ | — |
| **VC-1** | **L'app deve supportare comandi vocali Gemini e Google Assistant** (aggiornato maggio 2026: prima era solo "Google Assistant") | ❌ **GAP non coperto né da Fase 0-2 né esplicitamente da Fase 3 nel piano originale** — `AutoMediaCallback` attuale non implementa `OnPlayFromSearch` | **Nuovo requisito da aggiungere esplicitamente al piano di Fase 3** (vedi §5.7) — è un criterio Tier 2 "Car optimized", **obbligatorio** per categorie che vogliono essere accettate su Play Store guidando |
| DR-1 | Risposta ai bottoni app-specific ≤ 2s | ⚠️ da testare con DHU dopo la migrazione | Test da eseguire in §7 |
| DR-2 | Avvio app ≤ 10s | ⚠️ da testare | Test da eseguire in §7 |
| DR-3 | Caricamento contenuto ≤ 10s | ⚠️ Il probing attuale (3s timeout per URL, in parallelo) dovrebbe rientrare, ma va **misurato**, non assunto | Test da eseguire in §7 |
| ST-1 | Nessun testo che scorre automaticamente | ✅ (nessun testo scrollante custom, il testo lo renderizza l'host AA) | — |
| SA-1 / AD-1 / IU-1 | Nessuna animazione, nessuna pubblicità testuale, immagini limitate all'artwork | ✅ per costruzione (RadioE45 non disegna Activity nella UI dell'auto: l'host Android Auto renderizza tutto da `MediaSession`/`MediaBrowser`) | — |
| VI-1 | Se l'utente deve tornare al telefono, mostrare un messaggio "guarda lo schermo solo se sicuro" | ❌ non implementato (nessun permesso runtime richiesto oggi lato Android Auto, ma da verificare se in futuro serviranno permessi) | Basso rischio ora, da tenere a mente se si aggiungono permessi runtime |

**Nota importante:** questa tabella è il "confronto" richiesto tra il piano di implementazione e la checklist Google. Il gap più significativo emerso è **VC-1 (voice commands)**, non menzionato esplicitamente nel piano originale ma **obbligatorio** secondo la checklist ufficiale — va trattato come parte integrante della Fase 3, non come "nice to have".

---

## 4. Decisione architetturale

### 4.1 Le due opzioni valutate

**Opzione A — ExoPlayer nativo dentro il service (raccomandata, è quella descritta nel piano originale)**
Un `androidx.media3.session.MediaLibraryService` possiede direttamente un'istanza `ExoPlayer`, indipendente da qualunque `Activity`/`Page` MAUI. Il service diventa l'unica fonte di verità per la riproduzione su Android, sia che l'app sia in foreground, sia che sia stata avviata da Android Auto con la UI mai apperta.

- ✅ Risolve **strutturalmente** il bug #4 (Play da AA con UI chiusa): la riproduzione non dipende più dall'esistenza di un `MediaElement` creato da `OnAirPage`.
- ✅ Un'unica `MediaSession` per costruzione (risolve #5/#11).
- ✅ Ducking, audio focus e "becoming noisy" gestiti nativamente da ExoPlayer (righe di codice custom in meno, superficie di bug minore).
- ✅ Aderente alla direzione indicata da Google in CarAppQuality.md (MC-1 cita esplicitamente `media3.session`).
- ⚠️ Comporta la **sostituzione del motore di playback su Android**: il codice della classe condivisa `AudioService.cs` (basata su `MediaElement`) non verrà più usato su Android. iOS/macOS/Windows restano invariati.
- ⚠️ Difficoltà ★★★★★, richiede un service Android completamente nuovo e test approfonditi (buffering, riconnessione stream live, gestione errori — tutta logica che oggi vive in `AudioService.cs` e che va **riprodotta con lo stesso rigore** nel nuovo backend).

**Opzione B — Adapter Player sopra l'`AudioService` condiviso esistente (scartata)**
Si implementa un `Player` Media3 (es. estendendo `SimpleBasePlayer`) che si limita a *inoltrare* i comandi (`play`, `pause`, `stop`) all'`AudioService` condiviso esistente (che continua a usare `MediaElement`), e a rispecchiarne lo stato per notificarlo alla sessione.

- ✅ Rischio più basso, riusa la logica di streaming/watchdog già testata.
- ❌ **Non risolve il bug #4**: la riproduzione resta comunque legata a `_mediaElement`, che è `null` finché `OnAirPage` non viene creata. Un `Player` adapter che inoltra a un motore inesistente non farebbe nulla di diverso da oggi.
- ❌ Contraddice l'affermazione esplicita del piano originale ("ExoPlayer *in the service*").

**Decisione: si procede con l'Opzione A.** L'opzione B viene scartata perché non raggiunge l'obiettivo dichiarato della Fase 3. Va tenuta in mente solo come eventuale *fallback* temporaneo se l'Opzione A si rivelasse troppo rischiosa a metà lavoro (vedi §8 Rollback).

### 4.2 Impatto sul contratto condiviso `IAudioService`

`Initialize(MediaElement mediaElement)` (in `Services/Audio/IAudioService.cs:15` circa) è specifico di CommunityToolkit e non ha senso per un backend Android nativo Media3.

**Scelta a basso rischio (raccomandata):** non toccare l'interfaccia condivisa. La nuova implementazione Android (`AndroidMedia3AudioService`) espone `Initialize(MediaElement mediaElement)` come **no-op**: la riproduzione Android non dipende più da questa chiamata, che resta per compatibilità con `OnAirPage.xaml.cs:33` (che continua a compilare invariato su tutte le piattaforme, dato che `OnAirPage.xaml` è condiviso). Il controllo `AudioPlayer` (MediaElement) nella XAML diventa "morto" su Android (non decodifica più nulla) ma non causa doppia riproduzione perché il nuovo backend non lo usa: da verificare comunque che il rendering del `MediaElement` senza `Source` non generi overhead o richieste di permessi indesiderate.

**Alternativa scartata:** rimuovere `Initialize` dall'interfaccia e introdurre un'interfaccia opzionale `IMediaElementHost`. Scartata per ora perché aumenta la superficie di refactoring senza beneficio immediato; da riconsiderare solo se il no-op risultasse fragile in pratica.

### 4.3 Registrazione DI (pattern già in uso nel codebase, da estendere)

```csharp
// MauiProgram.cs — pattern coerente con quello già usato per IAudioFocusManager/IPlatformNowPlayingService
#if ANDROID
builder.Services.AddSingleton<IAudioService, AndroidMedia3AudioService>();
#else
builder.Services.AddSingleton<IAudioService, AudioService>();
#endif
```

---

## 5. Sotto-fasi operative

Ordine vincolante: **3.1 → 3.2 → 3.3 → 3.4 → 3.5**, poi 3.6/3.7/3.8 in parallelo, poi 3.9 (rimozione codice legacy) **solo dopo** che 3.1-3.8 sono validati su device fisico, infine 3.10 (voice commands) può procedere in parallelo a 3.6-3.8.

### 3.1 — Pin espliciti delle dipendenze Media3 (15 min)
- **File:** `RadioE45.csproj`
- **Azione:** aggiungere riferimenti espliciti (non solo transitivi) a:
  - `Xamarin.AndroidX.Media3.Session` (1.8.0, per coerenza con quanto già tirato da `CommunityToolkit.Maui.MediaElement`)
  - `Xamarin.AndroidX.Media3.ExoPlayer` (1.8.0)
  - `Xamarin.AndroidX.Media3.ExoPlayer.Hls` (1.8.0) — necessario per lo streaming HLS (`station.HlsUrl`)
  - `Xamarin.AndroidX.Media3.Common` (1.8.0)
- **Motivazione:** un riferimento solo transitivo può cambiare versione "a sorpresa" se in futuro si aggiorna `CommunityToolkit.Maui.MediaElement`; pinnare esplicitamente rende la dipendenza Media3 di prima classe, visibile e controllata.
- **Rischio:** 🟢 (solo aggiunta di riferimenti già presenti nell'albero delle dipendenze) · **Difficoltà:** ★☆☆☆☆
- **Verifica:** `dotnet restore` + build Android pulita, controllare che non ci siano conflitti di versione.

### 3.2 — Costruire il `Player` (ExoPlayer) e la fabbrica di `MediaSource` (2-3h)
- **Nuovo file:** `Platforms/Android/Services/Media3/RadioPlayerFactory.cs`
- **Contenuto atteso:**
  - Costruzione di `ExoPlayer.Builder` con:
    - `SetAudioAttributes(new AudioAttributes.Builder().SetUsage(C.UsageMedia).SetContentType(C.AudioContentTypeMusic).Build(), handleAudioFocus: true)` → sostituisce la gestione manuale del focus (assorbe #9)
    - `SetHandleAudioBecomingNoisy(true)` → sostituisce `BecomingNoisyReceiver` (assorbe #10)
    - `SetWakeMode(C.WakeModeNetwork)` → mantiene la CPU/rete attiva durante lo streaming in background
  - `DefaultMediaSourceFactory` con `DefaultHttpDataSource.Factory` configurato con timeout di connessione/lettura coerenti con l'attuale probing (oggi 3s per il probe HTTP, 12s per il watchdog di buffering — questi numeri vanno *portati* nel nuovo componente, non reinventati arbitrariamente)
  - Logica di selezione URL: riusare la stessa strategia di `AudioService.TryOpenStreamAsync` (candidati in ordine `OnAirStreamUrl → HlsUrl → StreamUrl → StreamUrlFallback`, probe in parallelo, primo che risponde vince) — **da riportare 1:1**, è logica di business testata, nonва reinventata.
- **Rischio:** 🟠 (componente nuovo, core della riproduzione) · **Difficoltà:** ★★★★☆
- **Test:** unit test se possibile isolando la logica di scelta URL dalla dipendenza `ExoPlayer`; altrimenti test manuale con log su emulatore.

### 3.3 — `RadioLibrarySessionCallback` (gestione comandi sessione) (2-3h)
- **Nuovo file:** `Platforms/Android/Services/Media3/RadioLibrarySessionCallback.cs`
- Implementa `MediaLibraryService.MediaLibrarySession.Callback`:
  - `OnGetLibraryRoot` → equivalente dell'attuale `OnGetRoot`, con lo stesso allowlist (ma esteso, vedi 3.6)
  - `OnGetChildren` → porta la logica di `SendStationsAsync` (lettura da `IAzuraStationCatalog`)
  - `OnAddMediaItems` / `OnSetMediaItems` (Media3 richiede la conversione `MediaItem → MediaItem con LocalConfiguration/Uri` prima di passare a `Player.SetMediaItems`) — qui va mappato lo `AzuraStation.Id` al `MediaItem.RequestMetadata.MediaUri` o mantenuto un lookup per `mediaId` come oggi
  - `OnPlaybackResumption` (Media3-specific: gestisce la ripresa quando il sistema richiede "riprendi l'ultimo contenuto" — rilevante per la certificazione, assente nel vecchio codice)
- **Rischio:** 🟠 · **Difficoltà:** ★★★★☆
- **Nota:** questa classe **assorbe** interamente `RadioMediaBrowserService.AutoMediaCallback` — copiare la logica di business (`OnPlay`, `OnPlayFromMediaId`, fallback su prima stazione), non reinventarla.

### 3.4 — `RadioLibraryService` (il service Android, unico) (2h)
- **Nuovo file:** `Platforms/Android/Services/Media3/RadioLibraryService.cs`
- Estende `MediaLibraryService`:
  - `OnCreate`: crea `Player` (da 3.2), crea `MediaLibrarySession.Builder(this, player).SetCallback(callback).Build()`
  - `OnGetSession(ControllerInfo)`: ritorna la sessione (con eventuale controllo allowlist per client non fidati)
  - `OnTaskRemoved(Intent)`: assorbe la logica di `AudioLifecycleService` — oggi ferma lo stream e azzera `CurrentStation`; **da decidere consapevolmente**: Media3 di default, se configurato con `player.stop()` + `player.release()` qui, termina la riproduzione allo swipe. Verificare che questo comportamento coincida con quello attuale (voluto: comportamento identico a `AudioLifecycleService.OnTaskRemoved`).
  - Notifica: **non serve codice custom** — `MediaLibraryService` fornisce di default un `DefaultMediaNotificationProvider` che costruisce la notifica MediaStyle da `Player`/`MediaMetadata`. Personalizzazioni (icona app, canale di notifica con importanza `Low`, ecc.) vanno fatte tramite `setMediaNotificationProvider` con un provider custom **solo se** il default non basta esteticamente — da valutare *dopo* aver visto il risultato di default, per non riscrivere codice che Media3 già fa bene.
- **Rischio:** 🟠 · **Difficoltà:** ★★★☆☆
- **Nota critica sul foreground:** Media3's `MediaSessionService`/`MediaLibraryService` gestisce autonomamente `startForeground`/notifica in risposta ai cambi di stato del `Player`, il che **per costruzione risolve il bug #1** (niente più `ForegroundServiceDidNotStartInTimeException` per notifica-vuota, perché il service non promuove sé stesso a foreground finché il player non ha uno stato riproducibile). Va comunque **verificato con un test esplicito** (stessa idea del test previsto in Fase 2A: avviare lo stream su una stazione priva di metadati "now playing" nei primi istanti).

### 3.5 — `AndroidMedia3AudioService : IAudioService` (bridge verso il resto dell'app) (2-3h)
- **Nuovo file:** `Platforms/Android/Services/Media3/AndroidMedia3AudioService.cs`
- Implementa `IAudioService` mantenendo la superficie usata da `OnAirViewModel` e dagli altri consumer (`IsPlaying`, `IsBuffering`, `CurrentStation`, eventi `PlaybackStateChanged`/`ErrorOccurred`/`StreamOpened`, metodi `PlayAsync`/`PauseAsync`/`ResumeAsync`/`StopAsync`/`StopImmediate`/`SetVolume`/`UpdateMetadata`/`Shutdown`).
- Comunica con `RadioLibraryService` tramite un **holder statico** (stesso pattern proposto in Fase 2B con `SharedMediaSessionHolder`, qui applicato al `Player`/alla sessione invece che al vecchio `MediaSession`), oppure — più idiomatico per Media3 — tramite un `MediaController` connesso in-process alla sessione (`MediaController.Builder(context, sessionToken).buildAsync()`), che è il modo raccomandato da Google per un client applicativo che vuole controllare un `MediaLibraryService` in esecuzione nello stesso processo.
- `Initialize(MediaElement)`: no-op (vedi §4.2).
- **Rischio:** 🟠 · **Difficoltà:** ★★★☆☆
- **Punto delicato:** il ciclo di vita — il service deve essere avviato (foreground) alla prima `PlayAsync()` chiamata dalla UI **così come** da un comando remoto di Android Auto, senza duplicare istanze. Usare `ContextCompat.StartForegroundService` con lo stesso pattern try/catch di Fase 1.A (il fix #2 resta necessario qui).

### 3.6 — Allowlist estesa + content style hints (30 min, in parallelo a 3.3-3.5)
Riporta qui il contenuto della Fase 2D del piano originale, che va integrato comunque:
- `OnGetLibraryRoot`: aggiungere `com.google.android.carassistant`, `com.android.bluetooth`, `com.google.android.wearable.app` all'allowlist (oggi solo `com.google.android.projection.gearhead` e `com.google.android.mediasimulator`)
- Impostare `LibraryParams` con hint `CONTENT_STYLE_BROWSABLE_HINT`/`CONTENT_STYLE_PLAYABLE_HINT` nel `Bundle` extra della root (equivalente Media3 dell'API vista in Fase 2D)
- **Rischio:** 🟡/🟢 · **Difficoltà:** ★☆☆☆☆

### 3.7 — Voice commands (Gemini / Google Assistant) — VC-1 (2-4h, NUOVO rispetto al piano originale)
- **Motivazione:** emerso dal confronto con `CarAppQuality.md` (§3.a di questo documento) — criterio Tier 2 obbligatorio, non coperto da nessuna fase precedente.
- **File:** `RadioLibrarySessionCallback.cs` (da 3.3)
- **Azione:** implementare `OnPlayFromSearch(session, controller, query, extras)`:
  - Se `query` è vuota → comportamento attuale di `OnPlay()` (riprende/riproduce la prima stazione)
  - Se `query` non è vuota → cercare in `IAzuraStationCatalog.Stations` una corrispondenza per nome (case-insensitive, contains) e riprodurla; se nessuna corrispondenza, fallback sulla prima stazione con log di warning
- **Rischio:** 🟡 (nuova funzionalità isolata) · **Difficoltà:** ★★☆☆☆
- **Test:** Android Auto Media Simulator / DHU supportano l'invio di comandi `PlayFromSearch` da riga di comando (`adb shell am broadcast` verso l'intent di test vocale, o tramite il simulatore stesso) — verificare la procedura esatta nella documentazione DHU al momento del test.

### 3.8 — Icone e contrasto (VD-1/VD-2/VD-3) (1h, verifica non bloccante)
- Verificare che le icone di trasporto usino risorse di sistema tintabili (`Icon.CreateWithResource`, già fatto oggi in `AndroidMediaNotificationService.CreateAction`) — Media3's `DefaultMediaNotificationProvider` usa comunque icone di sistema per play/pause/stop di default, quindi il rischio è basso.
- Verificare che gli artwork remoti delle stazioni (`station.LogoUrl`) risultino leggibili quando mostrati come `MediaItem` icon nel browse tree — se alcuni loghi hanno sfondo trasparente con dettagli scuri, possono risultare illeggibili su temi scuri auto. Azione: nessun codice necessario ora, annotare come check visivo da fare con DHU prima della submission.

### 3.9 — Rimozione del codice legacy (solo dopo validazione completa su device fisico) (1h)
Da eliminare, **in questo ordine e non prima di aver validato 3.1-3.8**:
1. `Platforms/Android/Services/RadioMediaBrowserService.cs`
2. `Platforms/Android/Services/AndroidMediaNotificationService.cs`
3. `Platforms/Android/AudioLifecycleService.cs`
4. Le tre dichiarazioni `<service>` corrispondenti in `AndroidManifest.xml`, sostituite da una sola:
   ```xml
   <service
       android:name="com.radioe45.app.Media3.RadioLibraryService"
       android:exported="true"
       android:foregroundServiceType="mediaPlayback">
       <intent-filter>
           <action android:name="androidx.media3.session.MediaLibraryService" />
           <action android:name="android.media.browse.MediaBrowserService" />
       </intent-filter>
   </service>
   ```
5. **Da valutare separatamente (non eliminare d'impulso):** `Platforms/Android/Services/AudioFocusManager.cs`, `IAudioFocusManager`, `NullAudioFocusManager`, `Platforms/Android/Services/BecomingNoisyReceiver.cs`. Se il nuovo `ExoPlayer` gestisce focus e "becoming noisy" nativamente (3.2), questi diventano codice morto **solo su Android**; ma `IAudioFocusManager` è referenziato anche dal costruttore di `AudioService.cs` condiviso (usato su iOS/macOS/Windows tramite `NullAudioFocusManager`). Non rimuovere l'interfaccia: rimuovere solo l'implementazione Android (`AudioFocusManager.cs`) e verificare che nessun altro punto Android la registri più in DI.

**Rischio della rimozione:** 🟠 (rimozione di codice che funziona, va fatta solo con rete di sicurezza — branch dedicato, possibilità di rollback, vedi §8).

---

## 6. Mappa "vecchia fix → nuovo assorbimento"

| Bug/Fix Fase 1 | Come viene gestito in Fase 3 | Azione |
|---|---|---|
| #2 try/catch `StartForegroundService` | Va **ripetuto** nel nuovo `RadioLibraryService`/`AndroidMedia3AudioService`: Media3 non esenta dall'eccezione `ForegroundServiceStartNotAllowedException` se il service viene avviato da background senza le condizioni giuste | Riportare lo stesso pattern try/catch di `AndroidMediaNotificationService.StartService` (righe 99-120) nel nuovo componente che avvia il service |
| #9 AudioFocusManager | **Assorbito nativamente** da `ExoPlayer.Builder.SetAudioAttributes(attrs, handleAudioFocus:true)` | Rimuovere gradualmente il livello custom (§3.9 punto 5), solo dopo aver verificato che il ducking reale su chiamate/navigazione funzioni come prima (richiede device fisico) |
| #10 BecomingNoisyReceiver | **Assorbito nativamente** da `ExoPlayer.Builder.SetHandleAudioBecomingNoisy(true)` | Idem — rimuovere solo dopo verifica su device fisico (BT disconnesso realmente, non solo `adb shell input keyevent`) |
| #14 Thread-safety `_reconnectCts` | Non si applica 1:1 (il nuovo backend non usa `_reconnectCts`), ma **il principio resta**: qualunque logica di retry/riconnessione nel nuovo `RadioPlayerFactory`/`AndroidMedia3AudioService` deve essere thread-safe rispetto a chiamate concorrenti da UI-thread e da comandi Android Auto in arrivo su thread diversi | Applicare lo stesso rigore (lock espliciti o `Interlocked`) in fase di scrittura del nuovo codice, non aggiungerlo a posteriori |
| #1 EnsureForegroundStarted (Fase 2A, mai implementata) | Risolto architetturalmente da Media3 (vedi nota in 3.4) | Verificare con test esplicito, non assumere |
| #5/#11 SharedMediaSessionHolder (Fase 2B, mai implementata) | Risolto per costruzione: **una sola** `MediaLibrarySession` | — |
| #4 `_pendingStation` bridge (Fase 2C, mai implementata) | Risolto architetturalmente: il `Player` vive nel service, non serve più alcun bridge verso una UI che potrebbe non esistere | Test dedicato: comando Play da Android Auto/DHU **senza mai aver aperto l'app** |
| #7/#8 allowlist/content-style (Fase 2D, mai implementata) | Riportato in §3.6 | — |

---

## 7. Piano di test (mappato a CarAppQuality.md)

| Livello | Strumento | Cosa verificare |
|---|---|---|
| Emulatore Android + AA Media Simulator | `adb`, Android Auto Desktop Head Unit (DHU) | Browse tree, Play/Pause/Stop da simulatore, `OnPlayFromMediaId`, allowlist (3.6), voice search simulato (3.7) se il simulatore lo supporta |
| Emulatore | — | **Test esplicito bug #1**: avviare stream su stazione senza metadata "now playing" nei primi ~500ms — verificare nessun crash `ForegroundServiceDidNotStartInTimeException` |
| Emulatore | — | **Test esplicito bug #5/#11**: verificare (via `adb shell dumpsys media_session`) che esista **una sola** sessione attiva `com.radioe45.app` |
| Device fisico | Cavo USB + Android Auto reale (developer mode, "Unknown sources" abilitato — vedi nota finale del piano originale) | **Test esplicito bug #4**: swipe-kill dell'app dai recenti, poi avviare la riproduzione **solo** da Android Auto → deve funzionare senza mai aver aperto l'Activity |
| Device fisico | Auto reale o cuffie BT | Ducking reale su chiamata in arrivo/navigazione GPS (assorbimento #9); disconnessione BT reale a volume alto (assorbimento #10) |
| Device fisico | Timer manuale | DR-1 (risposta bottoni ≤2s), DR-2 (avvio ≤10s), DR-3 (caricamento contenuto ≤10s) |
| Manuale/visivo | DHU o auto reale | VD-1/VD-2/VD-3 (contrasto icone/artwork), GB-1 (bottoni disabilitati coerenti) |
| Manuale | Google Assistant su device reale con AA collegato | VC-1: "Ok Google, riproduci [nome stazione] su RadioE45" |

---

## 8. Rischi e piano di rollback

- **Branch dedicato:** tutto il lavoro di Fase 3 resta su `Android-Auto-Fase3` fino a validazione completa su device fisico. Nessun merge su `main`/`Android-Auto` finché §7 non è superato almeno sui livelli "Emulatore".
- **Rimozione codice legacy solo a fine percorso** (§3.9): fino a quel momento, se un sotto-step Media3 si blocca, è possibile tornare a un manifest che dichiara ancora i vecchi service (basta non aver ancora eliminato i file).
- **Rischio maggiore:** la riscrittura del motore di streaming (probing URL, watchdog, gestione riconnessione — oggi in `AudioService.TryOpenStreamAsync`/`OnWatchdogElapsed`) dentro `RadioPlayerFactory`. Se questa parte richiede più tempo del previsto, valutare uno **stop intermedio**: tenere `RadioLibraryService` con Media3 Session ma delegare temporaneamente la selezione URL a una copia semplificata della logica esistente, rimandando le ottimizzazioni.
- **Rischio "cold start da Android Auto":** è lo scenario più difficile da testare (richiede device fisico + swipe reale dai recenti) — non dichiarare la Fase 3 "completa" senza aver eseguito questo test almeno una volta su hardware reale.

---

## 9. Prerequisiti pratici (invariati dal piano originale, confermati)

- **Device fisico Android** per: audio focus reale, BT reale, cold-start da Android Auto, voice commands reali.
- **Google Play Console**: pubblicazione richiesta per la distribuzione in produzione su Android Auto (in developer mode con "Unknown sources" funziona anche senza, per i test descritti in §7).
- **Build firmata**: oggi debug build non firmata con chiave di produzione — motivo per cui Android Auto non appare su device utente standard. Non bloccante per i test di questa fase, ma da tenere presente per la submission finale.

---

## 10. Definition of Done — Fase 3

La Fase 3 si considera completa quando:
1. Un'unica `MediaLibrarySession` gestisce tutta la riproduzione Android (verificato con `dumpsys media_session`).
2. Play/Pause/Stop/PlayFromMediaId funzionano da DHU e da device fisico.
3. Il test "cold start da Android Auto senza mai aprire l'app" (bug #4) passa su device fisico.
4. Il test "stazione senza metadata nei primi 500ms" (bug #1) non causa crash.
5. Ducking e gestione BT disconnesso verificati su device fisico (assorbimento #9/#10).
6. `OnPlayFromSearch` implementato e testato (VC-1).
7. Allowlist e content-style hint aggiornati (§3.6).
8. Codice legacy (`RadioMediaBrowserService`, `AndroidMediaNotificationService`, `AudioLifecycleService`) rimosso e manifest consolidato su un solo service.
9. Tutti i criteri applicabili della tabella §3.a sono ✅ o esplicitamente derogati con motivazione scritta.
10. Nessuna regressione su iOS/macOS/Windows (che non toccano il codice Android — verificare comunque una build pulita su tutte le piattaforme).
