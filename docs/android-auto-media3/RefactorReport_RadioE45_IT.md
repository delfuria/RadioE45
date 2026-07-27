# RadioE45 — Report di ricostruzione del livello di riproduzione (Android Auto / Bluetooth / servizio in background)

**Destinatario:** autore dell'app RadioE45
**Oggetto:** ricostruzione da zero del livello audio su Android — migrazione ad AndroidX **Media3 / ExoPlayer**
**Branch:** `android-auto-media3` (creato da `main`)
**Piattaforma:** .NET MAUI 10, `net10.0-android` (Android; minSdk 26 / targetSdk 36), pacchetto `com.radioe45.app`
**Punto di partenza:** versione 0.20 (versionCode 20)
**Data del report:** 2026-07-20

> Questo documento descrive **cosa è stato cambiato e perché**, rispetto alla versione originale dell'app. Il punto di partenza è stata la precedente analisi tecnica (`ResumeRadioE45_IT.md`), che ha individuato le cause profonde dei problemi con Android Auto, Bluetooth e riproduzione in background. Questo report ne è il seguito: la descrizione della correzione realizzata.

---

## 1. Sintesi esecutiva

Il livello di riproduzione su Android è stato **riprogettato da zero**. Il design precedente si basava su un player (`CommunityToolkit.Maui.MediaElement`) **che viveva dentro la UI** (`OnAirPage`) più **due–tre `MediaSession` indipendenti** con sincronizzazione manuale dello stato tramite uno store statico. Era questa la causa profonda della maggior parte dei sintomi: pulsante Play "muto" all'avvio dall'auto, assenza di next/prev, niente copertine in AA, routing ambiguo dei pulsanti Bluetooth.

La nuova architettura è **un unico servizio Media3 (`MediaLibraryService`) con un solo `ExoPlayer` e una sola `MediaLibrarySession`** come unica fonte di verità. Android Auto, Bluetooth (AVRCP), la notifica media e la schermata di blocco si collegano tutti **alla stessa sessione**. La UI del telefono **non possiede più un player** — pilota la sessione tramite un `MediaController`, esattamente come fa l'auto.

Risultato: play/pause/stop, **next/prev tra le stazioni**, l'albero di navigazione (root → stazioni), le copertine e i metadati "now playing" sono ora coerenti su ogni superficie (UI, AA, BT, schermata di blocco, notifica).

| Area | Prima | Dopo |
|--------|-------|-----|
| Player | `MediaElement` nella UI (`OnAirPage`) | un solo `ExoPlayer` nel servizio |
| Sessioni media | 2–3 `MediaSession` in conflitto | una `MediaLibrarySession` |
| Play dall'auto con UI chiusa | `return` silenzioso (non suona nulla) | funziona — il player vive indipendentemente dalla UI |
| Next / Prev | assenti | coda completa delle stazioni, seek-to-next/prev |
| Copertine in AA | assenti | `ArtworkUri` nei metadati |
| Sync dello stato | store statico manuale | nativa, tramite un'unica sessione |
| API | framework `Android.Media.Session` | **AndroidX Media3** (raccomandazione Google per AA) |

---

## 2. Perché la ricostruzione (riepilogo della diagnosi)

La diagnosi completa è in `ResumeRadioE45_IT.md`. Le cause profonde principali:

1. **Player legato alla UI.** `AudioService` richiedeva un `MediaElement` vivo, impostato solo in `OnAirPage.OnAppearing()`. Android Auto si collega al servizio **senza avviare la UI del telefono** → i comandi finivano su `null` → Play non faceva nulla.
2. **Più `MediaSession` in conflitto.** `RadioMediaBrowserService` ("RadioE45Auto"), `AndroidMediaNotificationService` ("RadioE45Playback") e la sessione interna del toolkit. Il sistema instrada i pulsanti BT/AVRCP verso un'unica sessione attiva — con più sessioni attive il comportamento era imprevedibile.
3. **Nessun next/prev** nel codice né nell'interfaccia `IAudioService`; nessuna copertina nell'albero AA; API legacy invece di Media3.

La conclusione dell'analisi (Priorità 0): **spostare il player dalla UI a un servizio e unificare su un'unica sessione Media3.** È esattamente ciò che è stato fatto.

---

## 3. Nuova architettura

```
                    ┌──────────────────────────────────────────────┐
                    │ RadioPlaybackService : MediaLibraryService     │
   Android Auto ───▶│                                                │
   Bluetooth   ───▶│   un ExoPlayer  +  una MediaLibrarySession      │
   Notifica    ───▶│                                                │
   Sch. blocco ───▶│   • albero di navigazione (root → stazioni)    │
                    │   • coda = elenco completo stazioni (next/prev)│
   UI telefono ──┐  │   • fallback URL dello stream                  │
                 │  │   • copertina / metadati                       │
                 │  └──────────────────────────────────────────────┘
                 │                     ▲
                 └──── MediaController ─┘   (Media3AudioService : IAudioService)
```

**Principio:** esattamente un player e una sessione. Tutto — inclusa la UI del telefono — è solo un **controller** di quella sessione.

---

## 4. Cosa è stato implementato (dettagli)

### 4.1 `RadioPlaybackService` — il nuovo servizio Media3 *(file nuovo)*
`Platforms/Android/Services/RadioPlaybackService.cs`

Il cuore della ricostruzione. Un `MediaLibraryService` che contiene:

- **Un `ExoPlayer`** costruito in `OnCreate` con `AudioAttributes` (usage=media, content=music), `SetHandleAudioBecomingNoisy(true)` (pausa allo scollegamento di BT/cuffie — realizza la raccomandazione #10 dell'analisi) e `SetWakeMode(WakeModeNetwork)`.
- **Una `MediaLibrarySession`** con `SetSessionActivity(...)` → toccando la notifica / la scheda AA / la schermata di blocco si apre l'app.
- **Albero di navigazione** per Android Auto/Assistant tramite `LibraryCallback` (`OnGetLibraryRoot`, `OnGetChildren`, `OnGetItem`): root → l'elenco stazioni dal catalogo.
- **Next/Prev.** `OnSetMediaItems` espande la selezione di una singola stazione nella **coda completa di tutte le stazioni** posizionata su quella scelta → Seek-to-Next/Previous dall'auto o dal volante si sposta tra le stazioni.
- **Risoluzione dell'URI.** Media3 "perde" l'URI quando i media item attraversano il binder, perciò `OnAddMediaItems` lo ricava di nuovo dal catalogo **preservando** i metadati "now playing" inviati dal controller.
- **Fallback URL dello stream.** Elenco di candidati nell'ordine `OnAirStreamUrl → StreamUrlFallback → HlsUrl → StreamUrl`. Il `StreamUrlFallback` pubblico (`https://{UrlBase}{StreamUrl}`) ha priorità sull'`ListenUrl` dell'API, che restituisce un indirizzo LAN (`192.168.1.100`) raggiungibile solo dentro la rete della stazione. In caso di errore di riproduzione il servizio passa al candidato successivo e riprova; al successo (`StateReady`) azzera il contatore.
- **Riproduzione in background.** `OnTaskRemoved`: se l'utente rimuove l'app dai recenti mentre la riproduzione è in corso — **continua a suonare**; il servizio si ferma solo quando non c'è riproduzione.

### 4.2 `Media3AudioService` — implementazione di `IAudioService` per la UI *(file nuovo)*
`Platforms/Android/Services/Media3AudioService.cs`

Un livello sottile che collega la UI alla sessione tramite un **`MediaController`**:

- `Initialize(MediaElement)` — l'argomento `MediaElement` è ora **inutilizzato su Android** (il player vive nel servizio); il metodo avvia solo la connessione del controller affinché la sessione sia pronta prima del primo Play.
- `PlayAsync` → `SetMediaItem` (il servizio espande alla coda completa), `Prepare`, `Play`. `PauseAsync`/`ResumeAsync`/`StopAsync`/`StopImmediate`/`SetVolume` → comandi del controller.
- **Metadati live** (`UpdateMetadata`) → `ReplaceMediaItem(currentIndex)` mantenendo il media id e lo stream (aggiornamento senza riavviare la riproduzione); cache dei campi per evitare round-trip ridondanti alla sessione.
- Ascolto dello stato tramite `IPlayerListener` (`OnIsPlayingChanged`, `OnPlaybackStateChanged`, `OnPlayerError`).
- **Sincronizzazione con l'auto:** `OnMediaItemTransition` — quando la stazione cambia dall'auto/volante (next/prev), il servizio aggiorna `_currentStation` e solleva il nuovo evento `StationChanged`, così la UI cambia la visualizzazione e il polling "now playing" verso la stazione corretta.

### 4.3 Interfaccia `IAudioService` — nuovo evento *(modifica)*
`Services/Audio/IAudioService.cs`

Aggiunto `event EventHandler<AzuraStation> StationChanged` — un cambio di stazione dall'esterno della UI del telefono (AA / display auto / pulsante al volante). Il resto dell'interfaccia è invariato, quindi `AudioService` su iOS/desktop funziona come prima.

### 4.4 Registrazione DI *(modifica)*
`MauiProgram.cs`

```csharp
#if ANDROID
    builder.Services.AddSingleton<IAudioService, Media3AudioService>();
#else
    builder.Services.AddSingleton<IAudioService, AudioService>();
#endif
```
Su Android è stata rimossa la registrazione `IPlatformNowPlayingService → AndroidNowPlayingService` (Media3 gestisce notifica e "now playing"). iOS/macOS/desktop invariati.

### 4.5 Manifest *(modifica)*
`Platforms/Android/AndroidManifest.xml`

- Rimosse le dichiarazioni `<service>` dei vecchi servizi.
- `RadioPlaybackService` dichiarato tramite attributi `[Service]` + `[IntentFilter]` con le azioni `androidx.media3.session.MediaLibraryService` e (compatibilità all'indietro) `android.media.browse.MediaBrowserService`; `ForegroundServiceType = mediaPlayback`.

### 4.6 File eliminati *(eliminazioni)*
L'intero vecchio livello di integrazione è stato rimosso — Media3 ne ha assunto il ruolo:
- `Platforms/Android/Services/RadioMediaBrowserService.cs` (sessione "RadioE45Auto")
- `Platforms/Android/Services/AndroidMediaNotificationService.cs` (sessione "RadioE45Playback" + notifica)
- `Platforms/Android/Services/AndroidNowPlayingService.cs` (ponte verso lo store)
- `Platforms/Android/Services/AndroidNowPlayingStateStore.cs` (store statico dello snapshot)
- `Platforms/Android/AudioLifecycleService.cs` (swipe-stop — sostituito da `OnTaskRemoved` nel nuovo servizio)

### 4.7 UI di riproduzione *(modifica)*
`Views/OnAirPage.xaml` + `.xaml.cs`, `ViewModels/OnAirViewModel.cs`

- Riga dei controlli: **⏮ precedente · play/pausa · ⏭ successiva** (il pulsante Stop è stato rimosso).
- `NextStationCommand` / `PreviousStationCommand` scorrono `_catalog.Stations` (tramite `SelectStationAsync`).
- Il `MediaElement` residuo viene rimosso dall'albero visuale su Android nel costruttore di `OnAirPage` (`#if ANDROID`), così non crea una seconda sessione vuota.

### 4.8 Pacchetti NuGet / build *(modifica)*
`RadioE45.csproj`

- Aggiunta la famiglia Media3 (linea coerente 1.10.x): `Xamarin.AndroidX.Media3.Session` 1.10.1.2, `…Media3.ExoPlayer` / `…ExoPlayer.Hls` / `…Media3.Common` 1.10.1.1.
- Media3 porta `Xamarin.AndroidX.Core` a 1.19.x → è necessario un pin di `Xamarin.AndroidX.Core.Core.Ktx` = 1.19.0.1 (altrimenti R8 segnala un duplicato `androidx.core.animation.AnimatorKt`).
- **Nota di build (Windows):** il branch è single-TFM (solo Android) — **non** passare `-p:TargetFrameworks` su `restore`/`build`.

---

## 5. Modifiche aggiuntive (oltre al core Media3)

Insieme alla ricostruzione sono stati introdotti alcuni miglioramenti generali:

- **Localizzazione (RESX).** Nuovi `Resources/Strings/AppResources.resx` (default), `.en`, `.pl` + `Services/Localization/` (`LocalizationResourceManager`, `TranslateExtension`). Stringhe UI tramite `{loc:Translate Key}`. Facilita un rilascio multilingua (incluso l'IT).
- **Pulizie nei ViewModel** (`RadioListViewModel`, `SettingsViewModel`, `AddStationViewModel`, `ScheduleViewModel`): semplificazioni, gestione errori (incl. reload su 429/RateLimiting), stazioni gestite dall'utente.
- **UI/navigazione:** `AppShell.xaml`, `SettingsPage.xaml`, `RadioListPage.xaml` (pulsante azione nel flyout), `AddStationPage.xaml` (pulsante Annulla), stili.
- **Icona dell'app** (`appiconfg.svg`) e correzioni minori.

---

## 6. Mappatura: bug originale → correzione

| # dall'analisi | Problema | Stato su questo branch |
|---|---|---|
| #4 | Play a freddo da AA non fa nulla (player nella UI) | **Corretto** — player nel servizio, indipendente dalla UI |
| #5 / #11 | Più `MediaSession` / pulsanti BT ambigui | **Corretto** — una `MediaLibrarySession` |
| #6 | API legacy invece di Media3 | **Corretto** — migrazione ad AndroidX Media3 |
| next/prev, copertine | Assenti in AA | **Corretto** — coda completa + `ArtworkUri` |
| #1 / #2 | `startForeground` fragile / avvio FGS dal background | **Risolto strutturalmente** — Media3 gestisce da sé foreground e notifica |
| #10 | Nessuna reazione allo scollegamento BT | **Corretto** — `SetHandleAudioBecomingNoisy(true)` |
| #3 | Continuità audio in background legata alla UI | **Corretto** — `OnTaskRemoved` + player nel servizio |
| #9 | Audio focus (ducking/pausa per navigazione/chiamate) | **Parziale** — Media3 gestisce l'audio focus tramite `AudioAttributes`, ma **richiede verifica in auto** (vedi §8) |

---

## 7. Stato di build e test

**Validato su `emulator-5554` (AVD telefono, API 37) — logcat/dumpsys:**
- l'app si avvia senza crash;
- viene creata **esattamente una** sessione Media3 (`ExoPlayerImpl Init` + `MediaSessionImpl Init` + `addSession`);
- il `MediaController` (UI) si collega alla sessione; la riproduzione passa per controller → sessione → player;
- **la coda si espande all'intero catalogo → next/prev** (dimensione coda = numero di stazioni);
- notifica Media3 con le azioni "Seek to previous / Pause / Seek to next" (foreground, categoria transport);
- il livello AVRCP/BT registra il player (`MediaPlayerList: Adding wrapped media player`).

**Validato in auto reale — telefono fisico + Android Auto sull'unità di bordo (aggiornamento 2026-07-27):**
- **interfaccia Android Auto sullo schermo dell'auto**: navigazione della lista stazioni, avvio della riproduzione dal browse, copertine e metadati, comandi play/pausa;
- **next/prev dall'auto** con la UI del telefono che resta sincronizzata (nessuna sovrascrittura dei metadati da parte della stazione precedente);
- **Bluetooth / AVRCP**: display dell'unità di bordo e pulsanti al volante;
- **riproduzione in background** e prosecuzione dopo la chiusura dell'app.

In sintesi: i tre problemi che hanno motivato la ricostruzione — nessun controllo in AA, niente copertine, next/prev assenti — risultano risolti sul campo, non solo in emulatore.

**⚠️ Un crash osservato durante i test in auto — mitigato, non ancora dimostrato risolto.** Il registro di sistema (`adb shell dumpsys activity exit-info com.radioe45.app`) lo colloca il **2026-07-21 alle 07:08:21**, con `reason=5 APP CRASH(NATIVE)` e `status=11` (SIGSEGV): app rimasta inattiva tutta la notte, crash la mattina, al momento di salire in auto. Lo stack trace non è più recuperabile (il tombstone è ruotato fuori dal dropbox dopo ~3 giorni); resta Sentry come fonte.

Un SIGSEGV nativo in un'app MAUI dopo ore di inattività è la firma tipica di un **peer JNI raccolto dal GC** mentre solo il lato Java ne conserva il riferimento. Tre oggetti `Java.Lang.Object` venivano creati inline e passati a Java senza alcuna radice gestita — fra questi il `LibraryCallback` della sessione, cioè proprio l'oggetto su cui Android Auto chiama `OnGetLibraryRoot` **al momento della connessione all'auto**. Ora tutti e tre sono ancorati (campo dedicato o `GCHandle` per la durata della future).

**Onestamente: senza una riproduzione non è dimostrabile che questa fosse la causa** — è stata eliminata una classe di cause compatibile con tutti i fatti osservati. Finché non c'è una conferma sul campo, **il branch non va pubblicato su Google Play**: va considerato pronto per la valutazione e per i test, non per il rilascio.

**Ancora da confermare:**
- audio focus negli scenari di interruzione: prompt di navigazione (ducking) e chiamata in arrivo (pausa + ripresa automatica) — vedi §8.1;
- comportamento su unità di bordo di marche/modelli diversi da quella provata.

> Nota di test: l'`ListenUrl` di default dall'API è l'indirizzo LAN `192.168.1.100:8000` — irraggiungibile dall'emulatore, quindi per i test serve un URL di stream pubblico (da cui la priorità di `StreamUrlFallback`).

---

## 8. Limitazioni note e TODO per l'autore

1. **Audio focus in auto — da confermare.** Media3 gestisce il focus tramite `AudioAttributes`, ma vanno testati gli scenari reali: prompt di navigazione (ducking) e chiamata telefonica (pausa + ripresa automatica) in auto / via BT.
2. **Certificazione Android Auto (indipendente dal codice!).** Le app AA della categoria "media" richiedono **revisione Google e distribuzione tramite Google Play**. Un build sideloaded (APK) appare in AA **solo** in modalità sviluppatore AA con "origini sconosciute" abilitate. Percorso di produzione: pubblicazione su Play + domanda al programma Android for Cars.
3. **Configurazione stazioni / URL stream.** L'`ListenUrl` dell'API può essere un indirizzo LAN — mantenere un `StreamUrlFallback` pubblico corretto nei dati della stazione (c'è un TODO in `AzuraStationCatalog.Map`).
4. **Pausa in diretta.** Pausa = `ExoPlayer.Pause()`; alla ripresa lo stream live salta comunque al live edge (corretto per la radio in diretta, diverso dal vecchio comportamento di chiusura connessione).
5. **Content style / estensioni AA** (layout delle liste in AA) — cosmetico, opzionale prima della certificazione.
6. iOS/macOS/desktop restano sull'esistente `AudioService` (`MediaElement`) — la ricostruzione riguarda solo Android.

---

## 9. File modificati (riepilogo)

**Nuovi:**
- `Platforms/Android/Services/RadioPlaybackService.cs`
- `Platforms/Android/Services/Media3AudioService.cs`
- `Resources/Strings/AppResources{.resx,.en.resx,.pl.resx}`
- `Services/Localization/{LocalizationResourceManager,TranslateExtension}.cs`

**Modificati (chiave):**
- `MauiProgram.cs`, `Services/Audio/IAudioService.cs`
- `Platforms/Android/AndroidManifest.xml`, `Platforms/Android/MainActivity.cs`
- `Views/OnAirPage.xaml(.cs)`, `ViewModels/OnAirViewModel.cs`
- `RadioE45.csproj`
- ViewModel e viste UI (Settings, RadioList, AddStation, Schedule, AppShell)

**Eliminati:**
- `RadioMediaBrowserService.cs`, `AndroidMediaNotificationService.cs`, `AndroidNowPlayingService.cs`, `AndroidNowPlayingStateStore.cs`, `AudioLifecycleService.cs`

Netto rispetto a `HEAD`: circa +372 / −1166 righe (una semplificazione netta del livello Android).

---

## 10. Prossimi passi consigliati

1. **Individuare la causa del crash visto in auto** (§7) — prerequisito a qualsiasi pubblicazione. Sentry è già integrato nell'app: il primo posto dove guardare è l'evento corrispondente, con lo stack trace.
2. **Audio focus negli scenari di interruzione** — prompt di navigazione (ducking) e chiamata in arrivo (pausa + ripresa automatica), in auto e via BT.
3. **Ripulire i dati delle stazioni** — URL di stream pubblici (evitare indirizzi LAN in `ListenUrl`).
4. **Percorso di produzione AA:** rilascio su Google Play + domanda ad Android for Cars (categoria media).
5. Opzionale: content style per AA, completamento della localizzazione IT.

---

*Questo report è disponibile anche in inglese (`RefactorReport_RadioE45_EN.md`).*
