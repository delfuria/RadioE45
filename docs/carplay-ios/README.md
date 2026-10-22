# Apple CarPlay — documentazione tecnica

> Stato al 22 ottobre 2026: **attivo** (iOS). Verificato con CarPlay Simulator per Mac e iPhone 12 mini (iOS 18.7.2); non ancora in un'auto vera.
> Storia, misure e decisioni: [`piano-operativo.md`](piano-operativo.md). Il piano del 31 luglio ([`implementation-plan.md`](implementation-plan.md)) è solo storico.

## Indice

- [1. Cosa fa](#1-cosa-fa)
- [2. Architettura](#2-architettura)
- [3. File coinvolti](#3-file-coinvolti)
- [4. Configurazione Apple e firma](#4-configurazione-apple-e-firma)
- [5. Riproduzione audio su iOS](#5-riproduzione-audio-su-ios)
- [6. Comportamenti e decisioni](#6-comportamenti-e-decisioni)
- [7. Come si prova](#7-come-si-prova)
- [8. Limiti noti e punti aperti](#8-limiti-noti-e-punti-aperti)
- [9. Documenti della cartella](#9-documenti-della-cartella)

---

## 1. Cosa fa

| Funzione | Dettaglio |
|---|---|
| Lista stazioni | `CPListTemplate` "Stazioni" con tutte le stazioni del catalogo; la stazione in onda ha il brano come sottotitolo, le altre la descrizione |
| Riproduzione | Tocco su una stazione: parte l'audio e si apre `CPNowPlayingTemplate` |
| Now Playing | Titolo, artista, copertina, tempo trascorso e durata del brano (da `MPNowPlayingInfoCenter`) |
| Comandi | Play, pausa, stazione successiva e precedente (con giro) da CarPlay, tasti dell'auto, lock screen, Centro di Controllo e cuffie; seek disattivato (stream dal vivo) |
| Avvio a freddo | CarPlay può avviare l'app senza la finestra principale: lista e audio funzionano lo stesso |
| Caricamento | All'avvio la lista mostra "Caricamento stazioni..." e si aggiorna da sola quando il catalogo è pronto |
| Scollegamento | Staccando l'auto (o le cuffie) la radio va in pausa e resta in pausa |

Non c'è avvio automatico della riproduzione all'apertura da CarPlay: si sceglie la stazione dalla lista, come nell'esempio della guida Apple (decisione del 22 ottobre). Al ricollegamento dell'auto iOS può inviare da sé un comando Play all'ultima app audio: l'app lo esegue.

## 2. Architettura

```
CarPlay (auto)                                  iPhone, processo RadioE45
───────────────                                 ───────────────────────────────────────────
scena CPTemplateApplicationScene ─────────────► CarPlaySceneDelegate (Platforms/iOS)
                                                    │  mapping verso i template, nient'altro
                                                    ▼
                                                CarStationList (Core)  ◄── IAzuraStationCatalog
                                                    │  stato Loading/Ready/Empty, voci, testi
                                                    ▼
                         tocco stazione ──────► IAudioService.PlayAsync
                                                    │
MPRemoteCommandCenter ──► AppDelegate ─────────► OnAirViewModel (Core): Next/PreviousStation
(play/pausa/succ./prec.)                            │
                                                    ▼
                                                LiveStreamAudioService (Core): URL, watchdog, riconnessione
                                                    │
                                                AudioService (MAUI, iOS) ── pausa su perdita dell'uscita
                                                    │
                                                IosStreamPlayer (Platforms/iOS)
                                                    ├── .m3u8 → AvStreamPlayer (AVPlayer)
                                                    └── altro → AudioQueueStreamPlayer (AudioQueue in-process)
                                                    │
MPNowPlayingInfoCenter ◄── IosNowPlayingService ◄───┘ metadati e stato
```

Regole di progetto rispettate:
- logica (stato della lista, testi, scelta della stazione adiacente) nel Core;
- `CarPlaySceneDelegate` fa solo il mapping verso i template;
- nessun `#if` di piattaforma nel Core;
- stringhe nei `.resx` (`CarPlay_Title`, `CarPlay_Loading`, `CarPlay_NoStations`).

## 3. File coinvolti

| File | Ruolo |
|---|---|
| `RadioE45/Platforms/iOS/Info.plist` | `UIApplicationSceneManifest`: scena principale (`SceneDelegate`) e scena CarPlay (`CarPlaySceneDelegate`), `UIApplicationSupportsMultipleScenes=true` |
| `RadioE45/Platforms/iOS/Entitlements.plist` | `com.apple.developer.carplay-audio` (build per device e App Store) |
| `RadioE45/Platforms/iOS/Entitlements.Simulator.plist` | Lo stesso entitlement per le build da simulatore (senza profilo) |
| `RadioE45/RadioE45.csproj` | Scelta del file di entitlement per simulatore e device; firma; `AllowUnsafeBlocks` solo per iOS |
| `RadioE45/Platforms/iOS/AppDelegate.cs` | Categoria della sessione audio; `GetConfiguration` per il ruolo CarPlay; `MPRemoteCommandCenter` (play, pausa, successiva, precedente) |
| `RadioE45/Platforms/iOS/SceneDelegate.cs` | Scena principale (`MauiUISceneDelegate`) |
| `RadioE45/Platforms/iOS/CarPlaySceneDelegate.cs` | `DidConnect`/`DidDisconnect`, lista, push del Now Playing |
| `RadioE45.Core/Services/Radio/CarStationList.cs` | Modello della lista per l'auto (Core, testato) |
| `RadioE45/Platforms/iOS/IosStreamPlayer.cs` | Scelta del motore e attivazione della sessione audio |
| `RadioE45/Platforms/iOS/AudioQueueStreamPlayer.cs` | Motore MP3/AAC in-process |
| `RadioE45/Platforms/iOS/AvStreamPlayer.cs` | Motore HLS (`AVPlayer`) |
| `RadioE45/Platforms/iOS/AudioSessionDiagnostics.cs` | Log di sessione, interruzioni e cambi di uscita (solo diagnostica) |
| `RadioE45/Services/Audio/AudioService.cs` | Su iOS collega `IosStreamPlayer` e mette in pausa quando l'uscita sparisce |
| `RadioE45/Services/Audio/IosNowPlayingService.cs` | `MPNowPlayingInfoCenter` (lock screen, Centro di Controllo, CarPlay) |
| `RadioE45/App.xaml.cs` | Su iOS `CreateWindow` non azzera la stazione (la finestra può essere ricreata a processo vivo) |
| `RadioE45.Core/Resources/Strings/AppResources*.resx` | Testi della lista CarPlay |
| `RadioE45.Core.Tests/Services/Radio/CarStationListTests.cs` | Test del modello della lista |

## 4. Configurazione Apple e firma

- Entitlement **CarPlay Audio App** assegnato all'account il 21 ottobre 2026 ([`entitlement-request.md`](entitlement-request.md)).
- Capability abilitata sull'App ID `com.radioe45.app`; profili **Radio E45 Development**, **Radio E45 AppStore**, **Radio E45 Mac AppStore** rigenerati il 22 ottobre ([`provisioning-profiles.md`](provisioning-profiles.md)).
- Verifica della firma di una build per device:

```bash
codesign -d --entitlements - RadioE45/bin/<Config>/net10.0-ios/ios-arm64/RadioE45.app | grep carplay-audio
```

- Simulatore: l'entitlement è nella sezione `__TEXT,__entitlements` dell'eseguibile, non nella firma. Dopo una modifica agli entitlement serve `--no-incremental` ([`simulator-testing.md`](simulator-testing.md) §5).
- Mac Catalyst non ha nulla di CarPlay (né scena né entitlement): CarPlay non esiste su Mac. Il profilo Mac è stato rigenerato solo perché condivide l'App ID.

## 5. Riproduzione audio su iOS

| Stream | Motore | Perché |
|---|---|---|
| HLS (`.m3u8`) | `AvStreamPlayer` (`AVPlayer`) | Su CarPlay parte in 1-3 s |
| MP3/AAC progressivo (Icecast) | `AudioQueueStreamPlayer` | Con `AVPlayer` CarPlay aspettava circa 20 s (percorso AirPlay bufferizzato); in-process parte in meno di 1 s. Analisi: [`carplay-mp3-start-delay.md`](carplay-mp3-start-delay.md) |

`AudioQueueStreamPlayer`:
- `NSUrlSession` → `AudioFileStream` → `OutputAudioQueue`, 16 buffer da 8 KB;
- partenza dopo 2 buffer (circa 1 s di audio);
- `Buffering` solo se non arrivano dati per 2 s;
- fine dello stream → `Ended`; errori di rete o HTTP → `Failed`; interruzione (telefonata) → `Paused`.

Riconnessione e watchdog restano in `LiveStreamAudioService` (Core), condivisi con Mac Catalyst, Windows e Uno.

Sessione audio:
- categoria `Playback` impostata all'avvio;
- attivazione solo all'apertura di uno stream, per non fermare la radio dell'auto all'apertura dell'app (guida CarPlay di Apple).

## 6. Comportamenti e decisioni

| Tema | Decisione | Data |
|---|---|---|
| Root template | Lista stazioni; Now Playing in push dopo il tocco | luglio, confermata |
| Avvio automatico da CarPlay | No: si sceglie dalla lista | 22 ottobre |
| Perdita dell'uscita (cavo, cuffie) | Pausa che resta tale | 22 ottobre |
| Ricollegamento dell'auto | Si esegue il comando Play inviato da iOS | 22 ottobre |
| Successiva/precedente | Cambio stazione con giro, come Android Auto | 22 ottobre |
| Tempi nel widget della dashboard CarPlay | Non mostrati dal sistema (succede anche con Musica) | 22 ottobre |
| iPad | Una sola finestra prevista; con `UIApplicationSupportsMultipleScenes=true` da verificare se iPadOS permette più finestre | aperto |

## 7. Come si prova

- **Simulatore iOS con display CarPlay:** [`simulator-testing.md`](simulator-testing.md). Build, verifica dell'entitlement, click veri sulla finestra CarPlay, log attesi.
- **iPhone reale con CarPlay Simulator per Mac:** [`device-test-plan.md`](device-test-plan.md), parti A (telefono), B (CarPlay), C (TestFlight), D (cosa resta senza auto).
- **Log del telefono:** `sudo log collect --device-name <iPhone> --last 30m --output prova.logarchive`, poi `/usr/bin/log show` (nella shell zsh di questo Mac `log` è una funzione).

Esiti al 22 ottobre:
- A1-A12 superate;
- B1-B4, B8, B9 superate, B5 secondo la decisione presa;
- da completare: B6, B7, B10, B11 e la parte C (TestFlight).

## 8. Limiti noti e punti aperti

- **Auto vera:** mai provata (tasti al volante, CarPlay wireless, spegnimento del motore). Vedi `device-test-plan.md` parte D.
- **TestFlight / firma App Store:** da provare prima dell'invio (parte C).
- **iPad:** verificare che non si possano aprire più finestre.
- **Stazioni offline nella lista:** oggi sono mostrate; toccarle non fa partire nulla. Da decidere.
- **Sottotitolo della lista:** si aggiorna quando cambia il catalogo, non a ogni brano.
- **Errore di riproduzione in CarPlay:** solo log, nessun `CPAlertTemplate` (Fase E del piano).
- **Copertina nella lista:** non mostrata (Fase E).
- **Segnalazione ad Apple:** testo pronto in `carplay-mp3-start-delay.md` §11, non ancora inviato.

## 9. Documenti della cartella

| File | Contenuto |
|---|---|
| `README.md` | Questo documento |
| [`piano-operativo.md`](piano-operativo.md) | Piano, registro delle sessioni, checklist §12 |
| [`simulator-testing.md`](simulator-testing.md) | Procedura di test sul simulatore e diagnostica |
| [`device-test-plan.md`](device-test-plan.md) | Prove su iPhone senza auto |
| [`provisioning-profiles.md`](provisioning-profiles.md) | Copia e rigenerazione dei profili |
| [`entitlement-request.md`](entitlement-request.md) | Richiesta e approvazione dell'entitlement |
| [`carplay-mp3-start-delay.md`](carplay-mp3-start-delay.md) | Ritardo di 20 s con `AVPlayer`, analisi e testi per le issue |
| [`app-store-carplay.md`](app-store-carplay.md) | Testi App Store e note per App Review |
| [`implementation-plan.md`](implementation-plan.md) | Piano del 31 luglio (storico) |
