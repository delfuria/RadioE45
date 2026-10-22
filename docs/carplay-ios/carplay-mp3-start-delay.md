# CarPlay: lo stream MP3 dal vivo si sente dopo circa 20 secondi

> Analisi del 22 ottobre 2026. Contiene le misure, le prove escluse, la causa individuata e i testi pronti per segnalare il problema ad Apple e, a titolo informativo, a CommunityToolkit (MAUI).
> Registro delle sessioni: [`piano-operativo.md`](piano-operativo.md) §11 (22 ottobre).

## Indice

- [1. Sintesi](#1-sintesi)
- [2. Ambiente](#2-ambiente)
- [3. Sintomo](#3-sintomo)
- [4. Misure](#4-misure)
- [5. Righe di log decisive](#5-righe-di-log-decisive)
- [6. Ipotesi escluse](#6-ipotesi-escluse)
- [7. Causa](#7-causa)
- [8. Impatto](#8-impatto)
- [9. Soluzione adottata in RadioE45](#9-soluzione-adottata-in-radioe45)
- [10. A chi segnalarlo](#10-a-chi-segnalarlo)
- [11. Issue per Apple (Feedback Assistant)](#11-issue-per-apple-feedback-assistant)
- [12. Issue informativa per CommunityToolkit.Maui (MediaElement)](#12-issue-informativa-per-communitytoolkitmaui-mediaelement)
- [13. Allegati e privacy](#13-allegati-e-privacy)
- [14. Come ripetere l'analisi](#14-come-ripetere-lanalisi)

---

## 1. Sintesi

Con l'iPhone collegato a CarPlay, uno stream radio **MP3 dal vivo** (Icecast/AzuraCast) riprodotto con `AVPlayer` si sente solo **17-22 secondi** dopo il play. L'app considera la riproduzione avviata dopo meno di un secondo. Sull'altoparlante dell'iPhone e su un altoparlante AirPlay l'avvio è immediato. La pausa è immediata anche in CarPlay.

Nelle stesse condizioni partono subito:
- lo **stesso MP3** aperto in **Safari**;
- lo **stesso tipo di stazione in HLS** con `AVPlayer`;
- **Musica** di Apple.

I log di sistema mostrano due percorsi verso CarPlay:
- **Safari** produce l'audio nel proprio processo (`IsPlayingOutput:YES`) e CarPlay lo riceve con uno stream **in tempo reale** (latenza 32 ms).
- **`AVPlayer`** consegna lo stream al percorso **AirPlay bufferizzato** (`IsPlayingOutput:NO`, `APAudioEngineBuffered` in `airplayd`). Quel motore resta in preroll con buffer allo 0% per circa 20 s; un MP3 dal vivo arriva solo in tempo reale, quindi il buffer si riempie lentamente.

Due impostazioni di `AVPlayer` provate una alla volta (`automaticallyWaitsToMinimizeStalling`, `allowsExternalPlayback`) non cambiano nulla.

## 2. Ambiente

| Elemento | Versione |
|---|---|
| iPhone | iPhone 12 mini (iPhone13,1), **iOS 18.7.2 (22H124)** |
| Simulatore auto | **CarPlay Simulator** per Mac (Additional Tools for Xcode), iPhone collegato via cavo USB |
| Mac | macOS 15.8.1 (24H32), Xcode 26.3 (17C529) |
| App | RadioE45 0.47.9, build Debug firmata Development, **senza** entitlement CarPlay (l'audio passa comunque su CarPlay come per ogni app audio) |
| Toolchain | .NET SDK 10.0.203, .NET for iOS 26.2.10233, MAUI 10.0.80 |
| Player | `AVPlayer` + `AVPlayerItem(URL:)`, sessione `AVAudioSession` `.playback` senza opzioni, `MPNowPlayingInfoCenter` e `MPRemoteCommandCenter` attivi |
| Stream | Icecast di AzuraCast, `audio/mpeg` 44,1 kHz stereo (LAN e Internet); HLS di AzuraCast per confronto |

Non verificato su un'auto vera né su iOS 26.

## 3. Sintomo

1. Collegare l'iPhone a CarPlay Simulator.
2. Avviare nell'app una stazione MP3.
3. L'app passa a "in riproduzione" entro 1 s; nel Now Playing di CarPlay compaiono subito titolo e copertina.
4. L'audio si sente dalla "auto" solo dopo circa 20 s.
5. Pausa: immediata. Play di nuovo: altri circa 20 s.
6. Cambio stazione MP3: altri circa 20 s.
7. Scollegando CarPlay, l'audio passa all'altoparlante subito.

## 4. Misure

Metodo: nei log del telefono (`sudo log collect --device-name …`) si misura il tempo tra l'inizio della riproduzione dichiarato dal sistema audio (`audiomxd`, `cmsSetIsPlaying … starting playing`) e il primo `Started` del motore audio CarPlay (`airplayd`, `APAudioEngineBuffered … Starting With Receiver Anchor to Started`).

| Sorgente | Player | Avvii | Inizio → audio in CarPlay |
|---|---|---|---|
| Musica (Apple) | interno | 5 | **0,17-0,69 s** |
| Safari, MP3 di Radio Casa (LAN) | WebKit | osservazione utente | **immediato**; nessun `Started` del motore bufferizzato: stream in tempo reale |
| RadioE45, HLS (`demo.azuracast.com/hls/azuratest_radio/live.m3u8`) | `AVPlayer` | 1 | **1,16 s** |
| RadioE45, HLS (Radio Casa, LAN) | `AVPlayer` | 1 | **3,0 s** |
| RadioE45, MP3 (Radio Casa LAN, Radio Cartoni LAN, Radio E45 e `radioe45.ddns.net` via Internet) | `AVPlayer` | 16 | **17,0-22,0 s** |
| RadioE45, MP3, uscita altoparlante | `AVPlayer` | 6 | `Playing` 0,56-0,74 s dopo l'apertura; percepito circa 1 s |

Lato app, con uscita `CarAudio`, l'`AVPlayer` arriva a `timeControlStatus = .playing` 0,55-1,26 s dopo `replaceCurrentItem` + `play()`. La pausa (`pause()` + rimozione dell'item) ferma il motore CarPlay in 12 ms.

Archivi di log (non nel repository): `radioe45-prove.logarchive`, `radioe45-prove2.logarchive`, `radioe45-prova1.logarchive`, `radioe45-prova2.logarchive`.

## 5. Righe di log decisive

**Stessa sessione audio, due modi di uscita** (`audiomxd`, all'inizio della riproduzione):

```
12:16:41.555 'sid:0x6df96, com.apple.WebKit(19182), 'prim'' with [MediaPlayback/Default] [NonMixable] [System Music] starting playing to output VAD: VirtualAudioDevice_Default. … NowPlayingApp:YES IsPlayingOutput:YES IsSharedAVAudioSessionInstance:YES
12:19:30.923 'sid:0x6df9b, RadioE45(19261), 'prim'' with [MediaPlayback/Default] [NonMixable] [System Music] starting playing to output VAD: VirtualAudioDevice_Default. … NowPlayingApp:YES IsPlayingOutput:NO IsSharedAVAudioSessionInstance:YES
```

**Safari: stream CarPlay in tempo reale** (`airplayd`):

```
12:16:41.623 [com.apple.airplay:APEndpointStreamCarPlayAudio] AudioMain: Setup succeeded for
Request: { "audioFormat" : 2048, "audioType" : "media", "audioLatencyMs" : 32, … }
Response: { "type" : 100, … }
```

**AVPlayer: client AirPlay bufferizzato e preroll** (`airplayd`):

```
12:19:31.045 <<<< FigBufferedAirPlayClientRoutingRegistry >>>> …RegisterClientWithRemoteID: Registering client …
11:40:17.173 [APSAudioFormatDescriptionUtils] Source Content ASBD: .mp3, 44100 Hz, Stereo, 1152 samples/packet
11:40:17.173 [APSAudioFormatDescriptionUtils] … transportASBD = AAC, 48000 Hz, Stereo, 1024 samples/packet  format = AAC-LC/48000/2
11:40:23.920 [APAudioEngineBuffered] BAE … Buffer fullness count 0, Capacity 8192, Fullness percent 0.00%, bufferLevelBytes 0, …
11:40:33.910 [APAudioEngineBuffered] BAE … Buffer fullness count 0, Capacity 8192, Fullness percent 0.00%, bufferLevelBytes 0, …
11:40:37.889 [APAudioEngineBuffered] BAE … updating playback state from Prerolling to Stopped
11:40:37.892 [APAudioEngineBuffered] BAE … updating playback state from Stopped to Priming Before Start
11:40:38.054 [APAudioEngineBuffered] BAE … updating playback state from Starting With Receiver Anchor to Started
```

L'app, nello stesso intervallo:

```
11:40:16.990 Resume station streaming...
11:40:17.071 AudioSession open | … outputs=CarAudio
11:40:17.809 AvStreamPlayer: playing 738 ms after open
```

**La registrazione come client bufferizzato avviene anche con uscita altoparlante** (12:21:31, `outputs=Speaker`), quindi è legata al player, non alla presenza di CarPlay.

## 6. Ipotesi escluse

| Ipotesi | Come è stata verificata | Esito |
|---|---|---|
| Rete o scelta dell'URL lenta | Log dell'app: scelta URL 10-406 ms; `Playing` entro 1,3 s | esclusa |
| Ritardo generico di CarPlay Simulator o del cavo | Musica 0,2-0,7 s; Safari immediato; HLS 1-3 s | esclusa |
| MP3 dal vivo lento su CarPlay in generale | Safari con lo stesso MP3 parte subito | esclusa |
| `AVPlayer.automaticallyWaitsToMinimizeStalling` (default `true`) | Impostato `false`, build verificata nel log: +20 / +22 / +22 s | esclusa |
| `AVPlayer.allowsExternalPlayback` (default `true`) | Impostato `false`, build verificata: ancora `Registering client` bufferizzato; +20 / +22 / +22 / +22 s | esclusa |
| Sessione audio mixabile o diversa | Stessa categoria e flag di Safari (`MediaPlayback/Default`, `NonMixable`, `System Music`, `NowPlayingApp:YES`) | esclusa |

## 7. Causa

**Misurato:**
- `AVPlayer` non produce l'audio nell'uscita del dispositivo (`IsPlayingOutput:NO`) ma lo consegna al percorso AirPlay bufferizzato di `airplayd` (`FigBufferedAirPlay…`, `APAudioEngineBuffered`). Verso CarPlay l'MP3 viene transcodificato in AAC-LC 48 kHz.
- Il motore bufferizzato resta in `Prerolling` con buffer allo 0% e parte dopo 17-22 s.
- Safari produce l'audio nel proprio processo (`IsPlayingOutput:YES`) e CarPlay usa uno stream in tempo reale (`type 100`, 32 ms).

**Dedotto, non verificato:** il percorso bufferizzato vuole accumulare in anticipo una quantità di audio pari a circa 20 s. Un file o un HLS la forniscono subito (download più veloce del tempo reale); uno stream MP3 dal vivo solo in tempo reale. Non è stato trovato un modo documentato per far scegliere ad `AVPlayer` il percorso in tempo reale.

**Domanda per Apple:** il comportamento è voluto per i contenuti "infiniti" in tempo reale? Esiste un'impostazione supportata di `AVPlayer`/`AVAudioSession` per usare il percorso in tempo reale verso CarPlay, o per ridurre il preroll del percorso bufferizzato con uno stream dal vivo?

## 8. Impatto

- Utenti RadioE45 in auto: 20 s di silenzio a ogni play, pausa/play e cambio stazione con stazioni solo MP3 (oggi Radio E45, Radio Cartoni).
- **Versione in produzione (0.47, CommunityToolkit `MediaElement`):** usa anch'essa `AVPlayer`, quindi con ogni probabilità ha lo stesso comportamento in CarPlay. **Non verificato.**
- Qualunque app MAUI o .NET che riproduce radio Icecast/SHOUTcast con `MediaElement` o `AVPlayer` su iOS.

## 9. Soluzione adottata in RadioE45

Decisa il 22 ottobre 2026: **motore MP3 nel processo dell'app** per iOS. Lo stream viene scaricato con `NSUrlSession`, diviso in pacchetti con `AudioFileStream` e riprodotto con `AudioQueue`, che decodifica l'MP3 e produce l'audio nel processo, come Safari. HLS resta su `AVPlayer`, dove l'avvio misurato è di 1-3 s.

Stato: implementato il 22 ottobre 2026 (`Platforms/iOS/AudioQueueStreamPlayer.cs`, `Platforms/iOS/IosStreamPlayer.cs`); sul simulatore l'audio parte in 0,3-0,4 s. Previsioni da verificare sul device con lo stesso metodo del §4:
- `audiomxd` riporta `IsPlayingOutput:YES` per RadioE45;
- `airplayd` apre "AudioMain: Setup succeeded … type 100";
- l'audio si sente in CarPlay entro 1-2 s.

## 10. A chi segnalarlo

| Destinatario | Pertinenza | Raccomandazione |
|---|---|---|
| **Apple** (Feedback Assistant) | Comportamento di `AVPlayer`/`airplayd`/CarPlay: è codice di sistema, riproducibile con un'app nativa in Swift | **Sì**, è il destinatario principale |
| **CommunityToolkit.Maui** (`MediaElement`) | `MediaElement` su iOS usa `AVPlayer`, quindi ne eredita il comportamento; non è un loro difetto | Facoltativa, **informativa**: utile ad altri sviluppatori e per una nota nella documentazione |
| **dotnet/macios** (.NET for iOS) | I binding funzionano; il comportamento è identico al nativo | **No**, salvo che serva un binding mancante |
| **dotnet/maui** | Il player audio non fa parte di MAUI | **No** |

---

## 11. Issue per Apple (Feedback Assistant)

Dove: app **Feedback Assistant** sul Mac o su <https://feedbackassistant.apple.com>.
Tipo: **Incorrect/Unexpected Behavior**. Area: **CarPlay** (in alternativa **AVFoundation**). Piattaforma: **iOS**.
Allegati: §13.

**Title**

```
AVPlayer live MP3 (Icecast) stream starts ~20 s late on CarPlay because it goes through buffered AirPlay; Safari plays the same URL in real time
```

**Description**

```
Summary
When an iPhone is connected to CarPlay, a live, endless MP3 stream (Icecast, audio/mpeg) played with AVPlayer is heard in the car only 17-22 seconds after play(). The same URL played in Safari, the same station in HLS with AVPlayer, and Apple Music all start in under 1-3 seconds. Pausing is immediate. Every play, resume or station change waits again ~20 s.

Environment
- iPhone 12 mini, iOS 18.7.2 (22H124)
- CarPlay Simulator (Additional Tools for Xcode) on macOS 15.8.1, iPhone connected by USB cable
- AVAudioSession category .playback, no options, mode default; MPNowPlayingInfoCenter and MPRemoteCommandCenter in use
- AVPlayer + AVPlayerItem(url:), live Icecast MP3 44.1 kHz stereo (AzuraCast)
- Not tested yet in a real car or on iOS 26

Steps to reproduce
1. Run the attached sample app (or any app that plays https://demo.azuracast.com/listen/azuratest_radio/radio.mp3 with AVPlayer and a .playback audio session).
2. Connect the iPhone to CarPlay Simulator.
3. Tap Play. Note the time until audio is heard from CarPlay.
4. Pause, then Play again.
5. Compare with the HLS URL https://demo.azuracast.com/hls/azuratest_radio/live.m3u8 in the same app, and with the MP3 URL opened in Safari.

Expected
Audio starts in CarPlay within 1-2 seconds, as with Safari, HLS and Apple Music.

Actual
AVPlayer reports timeControlStatus == .playing after ~0.6-1.3 s, but CarPlay plays audio only after 17-22 s (16 measurements). Pause is immediate (12 ms).

Measurements (from device logs)
- Apple Music: 0.17-0.69 s (5 starts)
- AVPlayer + HLS: 1.2-3.0 s
- AVPlayer + live MP3: 17.0-22.0 s (16 starts)
- Safari + same live MP3: immediate

What the logs show
- audiomxd, at "starting playing": Safari's session has IsPlayingOutput:YES, our AVPlayer session has IsPlayingOutput:NO. Everything else is identical ([MediaPlayback/Default] [NonMixable] [System Music] NowPlayingApp:YES).
- airplayd: for Safari, APEndpointStreamCarPlayAudio "AudioMain: Setup succeeded" with audioType "media", audioLatencyMs 32, type 100 (real-time stream).
- airplayd: for AVPlayer, FigBufferedAirPlayClientRoutingRegistry "Registering client" and APAudioEngineBuffered stays in "Prerolling" with "Buffer fullness ... 0.00%, bufferLevelBytes 0" for ~20 s, then "Prerolling to Stopped", "Priming Before Start", "Started". Source .mp3 44100 Hz is transported as AAC-LC 48000 Hz.
- The buffered-client registration also happens when the output is the built-in speaker.

Already tried (no change)
- AVPlayer.automaticallyWaitsToMinimizeStalling = false
- AVPlayer.allowsExternalPlayback = false

Questions
1. Is it expected that AVPlayer uses the buffered AirPlay path to CarPlay for a live, endless progressive stream, so that the start waits for a ~20 s preroll?
2. Is there a supported AVPlayer or AVAudioSession setting to use the real-time CarPlay audio path (as Safari does), or to shorten the buffered preroll for live content?
3. If not, is the recommended approach for live radio apps to use HLS or to render audio in-process (AudioQueue / AVAudioEngine)?
```

**App di esempio da allegare** (Swift, una schermata, nessun entitlement CarPlay richiesto per il percorso audio):

```swift
import SwiftUI
import AVFoundation
import MediaPlayer

@main
struct LiveMp3CarPlayDelayApp: App {
    var body: some Scene { WindowGroup { ContentView() } }
}

final class Radio: ObservableObject {
    let mp3 = URL(string: "https://demo.azuracast.com/listen/azuratest_radio/radio.mp3")!
    let hls = URL(string: "https://demo.azuracast.com/hls/azuratest_radio/live.m3u8")!
    private let player = AVPlayer()
    private var started = Date()
    private var observation: NSKeyValueObservation?
    @Published var status = "idle"

    init() {
        try? AVAudioSession.sharedInstance().setCategory(.playback)
        try? AVAudioSession.sharedInstance().setActive(true)
        UIApplication.shared.beginReceivingRemoteControlEvents()
        observation = player.observe(\.timeControlStatus) { [weak self] p, _ in
            guard let self, p.timeControlStatus == .playing else { return }
            let ms = Int(Date().timeIntervalSince(self.started) * 1000)
            DispatchQueue.main.async { self.status = "AVPlayer playing after \(ms) ms (listen for the real start in CarPlay)" }
        }
    }

    func play(_ url: URL) {
        started = Date()
        player.replaceCurrentItem(with: AVPlayerItem(url: url))
        player.play()
        MPNowPlayingInfoCenter.default().nowPlayingInfo = [MPMediaItemPropertyTitle: "Live test", MPNowPlayingInfoPropertyPlaybackRate: 1.0]
        status = "opening \(url.lastPathComponent)"
    }

    func stop() {
        player.pause()
        player.replaceCurrentItem(with: nil)
        status = "stopped"
    }
}

struct ContentView: View {
    @StateObject private var radio = Radio()
    var body: some View {
        VStack(spacing: 20) {
            Button("Play live MP3") { radio.play(radio.mp3) }
            Button("Play live HLS") { radio.play(radio.hls) }
            Button("Stop") { radio.stop() }
            Text(radio.status).font(.footnote).multilineTextAlignment(.center)
        }
        .padding()
    }
}
```

`Info.plist` dell'esempio: `UIBackgroundModes` = `audio`.
**Da fare prima di inviare:** compilare l'esempio e confermare che riproduce il ritardo con lo stream della demo AzuraCast. Il ritardo è stato misurato con stream MP3 di AzuraCast su LAN e Internet, non ancora con l'URL della demo.

## 12. Issue informativa per CommunityToolkit.Maui (MediaElement)

Dove: <https://github.com/CommunityToolkit/Maui/issues/new/choose>, tipo **Bug report** (o **Discussion**, se preferisci non aprire un bug). Prima cercare se esiste già con "CarPlay MediaElement delay" o "AirPlay buffered".

**Title**

```
[iOS] MediaElement: live MP3 (Icecast) streams start ~20 s late when the iPhone is connected to CarPlay
```

**Body**

```
Description
On iOS, when the iPhone is connected to CarPlay, live endless MP3 streams (Icecast/SHOUTcast) are heard in the car only ~20 seconds after playback starts. MediaElement (and AVPlayer underneath) reports Playing after < 1 s. HLS streams start in 1-3 s. Pausing is immediate.

This is not a MediaElement bug: we reproduced it with a plain AVPlayer and filed Apple Feedback FB<number>. Device logs show that AVPlayer audio goes through the buffered AirPlay path (airplayd APAudioEngineBuffered), which waits for a ~20 s preroll that a live stream can only fill in real time. Safari plays the same URL through a real-time CarPlay stream and starts immediately.

We are opening this so that other MediaElement users building radio/CarPlay apps know about it, and in case the toolkit wants to document it.

Steps to reproduce
1. MediaElement with Source = https://demo.azuracast.com/listen/azuratest_radio/radio.mp3, ShouldAutoPlay = true.
2. Connect the iPhone to CarPlay (or CarPlay Simulator for Mac).
3. Play. Audio is heard in CarPlay after ~20 s.

Expected behavior
Audio starts within 1-2 s, as with Safari or HLS.

Workarounds
- Prefer HLS when the server offers it (AzuraCast: per-station HLS setting).
- Render live MP3 in-process (AudioFileStream + AudioQueue), which uses the real-time CarPlay path.

Environment
- iOS 18.7.2, iPhone 12 mini; CarPlay Simulator on macOS 15.8.1
- .NET 10.0.203, .NET for iOS 26.2.10233, MAUI 10.0.80, CommunityToolkit.Maui.MediaElement 10.0.0
- Not verified in a real car.
```

Nota: l'ultima prova con `MediaElement` (non `AVPlayer` diretto) non è stata fatta dopo il passaggio ad `AvStreamPlayer`. Prima di aprire questa issue conviene una prova con la 0.47 dell'App Store su CarPlay Simulator: se il ritardo c'è, la frase "reproduced with MediaElement" diventa verificata.

## 13. Allegati e privacy

**Per Apple:**
1. **sysdiagnose** del telefono subito dopo aver riprodotto il problema: premere e rilasciare insieme entrambi i tasti volume e il tasto laterale (vibrazione), attendere circa 10 minuti, poi *Impostazioni → Privacy e sicurezza → Analisi e miglioramenti → Dati analisi*, file `sysdiagnose_…`. Apple lo chiede quasi sempre.
2. In alternativa o in aggiunta, un archivio log mirato: `sudo log collect --device-name <nome iPhone> --last 15m --output prova.logarchive`.
3. Video breve (schermo del Mac con CarPlay Simulator e audio) con un cronometro visibile.
4. L'app di esempio del §11, in uno zip con il progetto Xcode.

**Privacy:**
- Gli archivi contengono indirizzi della rete locale (per esempio `192.168.1.71`), nomi di app installate e, se si fanno prove con chiamate, metadati delle chiamate. Il numero di telefono nei log di `callservicesd` compare come `<private>`, ma conviene fare le prove da allegare **senza telefonate**.
- Feedback Assistant è privato tra te e Apple. GitHub è pubblico: lì niente archivi di log, solo estratti.

## 14. Come ripetere l'analisi

Nella shell zsh di questo Mac `log` è una funzione: usare **`/usr/bin/log`**. `--start` non accetta i decimali dei secondi.

```bash
A=~/Desktop/prova.logarchive

# Inizio riproduzione (audiomxd) e partenza del motore CarPlay (airplayd)
/usr/bin/log show "$A" --style compact --predicate '(process == "audiomxd" AND eventMessage CONTAINS "cmsSetIsPlaying:" AND eventMessage CONTAINS "starting playing") OR (process == "airplayd" AND eventMessage CONTAINS "Starting With Receiver Anchor to Started")'

# Percorso usato: tempo reale (Safari) o bufferizzato (AVPlayer)
/usr/bin/log show "$A" --style compact --predicate 'process == "airplayd" AND (eventMessage CONTAINS "AudioMain: Setup succeeded" OR eventMessage CONTAINS "RegisterClientWithRemoteID: Registering")'

# Lato app (RadioE45): scelta URL, uscita audio, tempo fino a Playing
/usr/bin/log show "$A" --style compact --predicate 'process == "RadioE45" AND (eventMessage CONTAINS "Stream URL probe" OR eventMessage CONTAINS "AudioSession open" OR eventMessage CONTAINS "ms after open")'
```
