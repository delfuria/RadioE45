# RadioE45 su Android TV — fattibilità e azioni

Data: 2026-10-18 (aggiornato 2026-10-22). Stato: analisi di fattibilità. Il **piano operativo** con sessioni, parità `tv` e ambiente di prova è in [`ANDROID-TV-ROADMAP.md`](ANDROID-TV-ROADMAP.md); questo documento resta come riferimento per fattibilità, azioni e rischi.

## Risposta breve

**Fattibile.** Stesso pacchetto Android (`com.radioe45.app`) può comparire anche su Android TV / Google TV, con la stessa scheda Play Store. Il lavoro vero non è la pubblicazione: è la **interfaccia**. L'app oggi è pensata per touch; su TV serve navigazione con telecomando (D-pad).

Base già presente nel progetto che aiuta:
- `RadioPlaybackService` (Media3 `MediaLibraryService`) con sessione media: copre play/pause/skip dal telecomando e la notifica di riproduzione.
- Audio live in `LiveStreamAudioService` già nel Core: nessun lavoro audio nuovo.
- ViewModel nel Core (`RadioE45.Core/ViewModels`): la logica è riusabile, va solo fatta una vista TV.

## Stato attuale (verificato nel repo)

| Voce | Valore | Nota |
|---|---|---|
| Target Android | `net10.0-android`, `SupportedOSPlatformVersion` 26.0 | Android TV supporta API 26+: ok |
| `AndroidManifest.xml` | solo `automotive_app_desc`, nessun `leanback`, nessun `uses-feature` | manca tutto il lato TV |
| `MainActivity` | `MainLauncher = true`, nessun intent `LEANBACK_LAUNCHER` | TV non lo mostra nel launcher |
| UI | Shell XAML, touch, nessuna gestione focus | va ripensata per D-pad |
| Media | Media3 `MediaLibraryService` + Android Auto | riusabile per TV |

## Azioni richieste

### 1. Manifest (piccolo, ~30 minuti)
- `<uses-feature android:name="android.software.leanback" android:required="false" />`
  Con `required="false"` l'app resta installabile anche su telefono. Con `true` i telefoni la perderebbero.
- `<uses-feature android:name="android.hardware.touchscreen" android:required="false" />`
- Intent filter `android.intent.category.LEANBACK_LAUNCHER` su una activity (o alias), così l'icona compare nel launcher TV.
- `android:banner="@drawable/..."` su `<application>`: banner 320x180 px, obbligatorio per il launcher TV.

### 2. Interfaccia TV (il lavoro grosso)
Opzioni:

- **A. Stessa UI, con focus.** Minimo sforzo, ma Shell e `CollectionView` su Android TV hanno il focus D-pad debole. Rischio alto di UX scarsa. Serve uno spike di 1–2 giorni per verificarlo.
- **B. Vista TV dedicata** (XAML separato, selezionato a runtime con `UiModeManager.CurrentModeType == UiModeType.Television`). Layout a 10 piedi: lista stazioni a sinistra/griglia, "ora in onda" grande, controlli play/volume. Logica dai ViewModel del Core.
- **C. Solo lettore minimale** (una schermata: stazione corrente + lista stazioni). Il più piccolo, sufficiente per uno scenario radio.

Regole progetto da rispettare:
- Regola 1 (MAUI first) e 7: la vista TV va in `RadioE45/Views/` (o cartella `Tv/`) e va registrata in `parity/`.
- Regola 2: `parity/features.yaml` serve una colonna/piattaforma nuova `tv` oppure una voce `limited`/`todo` dichiarata. Da decidere con `ParityCheck` (oggi gestisce maui, desktop, web).
- Regola 3: nessuna logica nella vista TV, solo layout e binding.

### 3. Gestione input
- Tasti D-pad, OK, Back, Play/Pause, Next/Prev dal telecomando: gestire in `MainActivity` o via `KeyEvent`, e far passare i comandi media alla sessione Media3 (già presente).
- `OnBackPressed` attuale manda l'app in background: su TV è coerente, ma va verificato.

### 4. Build e pacchetto
- Nessun target nuovo: stesso `net10.0-android`. L'AAB/APK caricato serve sia phone sia TV.
- Build: `dotnet build RadioE45 -f net10.0-android`, poi pacchetto come da `docs/STORE-DISTRIBUTION.md`.
- Versione: bump in `RadioE45/RadioE45.csproj` come da `docs/VERSION-BUMP.md`.

### 5. Play Console
- Abilitare il form factor **Android TV** nella stessa app (non serve app separata).
- Asset TV: screenshot 16:9 (minimo 1280x720) e banner TV; requisiti esatti da verificare nella console al momento dell'upload.
- Dichiarazioni: l'app non richiede touch; verificare il questionario "app per TV" e le linee guida qualità Android TV (navigazione D-pad completa, nessun elemento non focusabile).
- Testi store in inglese (regola progetto e memoria).
- Amazon Fire TV usa Amazon Appstore, non Play: fuori da questo piano salvo richiesta.

### 6. Test
- Emulatore Android TV (AVD con immagine "Android TV") su Mac: test base D-pad, focus, media keys.
- Dispositivo reale consigliato (Chromecast con Google TV o simile): audio, rete, comportamento standby.
- Test Core: nessun cambio atteso; ParityCheck e `dotnet test` come sempre.

### 7. Manutenzione
- Due UI da tenere allineate: ogni modifica ai ViewModel va verificata su entrambe.
- Ogni release: test su emulatore TV oltre a phone.

## Rischi
- Focus D-pad in MAUI Shell/CollectionView: se debole, opzione B diventa obbligatoria.
- Play Console può richiedere asset o dichiarazioni che cambiano nel tempo: verificare al momento dell'upload.
- Audio in background su TV (standby, auto-off) va provato su device reale.

## Stima indicativa
- Manifest + icona + banner: poche ore.
- Spike focus su UI attuale: 1–2 giorni.
- Vista TV minimale (C): circa una settimana.
- Vista TV completa (B): due o tre settimane, più test.
- Asset store e pubblicazione: mezza giornata.

## Decisioni (2026-10-22)
1. **Opzione C, con podcast.** Schermata minima (stazione corrente + lista stazioni) più elenco podcast e ascolto episodi. Una via di mezzo tra C e B: niente layout a 10 piedi completo, solo le viste già pronte nel Core.
2. **Fire TV: solo se non complica.** Stesso APK, store diverso (Amazon Appstore). Nel codice non risultano librerie Google Play Services: l'unico riferimento è il meta-data Android Auto (`com.google.android.gms.car.application`), che Fire OS ignora. Da verificare sul device Fire TV reale, non solo leggendo il codice.
3. **Dispositivo reale disponibile.** Test su device, emulatore opzionale.

## Fattibilità podcast (verificata nel repo)
- `PodcastListViewModel` (69 righe) e `PodcastEpisodesViewModel` (430 righe) stanno nel Core, senza dipendenze UI. Riusabili così come sono.
- Le pagine MAUI (`PodcastListPage`, `PodcastEpisodesPage`) fanno solo layout: la vista TV usa gli stessi ViewModel.
- Da verificare prima di promettere il player podcast su TV: quale servizio riproduce gli episodi su Android (`Services/Platform`) e se può coesistere con `RadioPlaybackService` e la sessione Media3.
- Parità: le voci `podcastlist.*` e `podcastepisodes.*` in `parity/features.yaml` sono già `done` per maui/desktop/web. La vista TV andrà aggiunta come nuova colonna `tv` o come `todo` dichiarato.

## Piano
Il piano a sessioni (T0–T9) è in [`ANDROID-TV-ROADMAP.md`](ANDROID-TV-ROADMAP.md) §6. Stato al 2026-10-22:
- **Fatto (T0):** manifest `leanback` e `touchscreen` con `required="false"`, `android:banner="@drawable/tv_banner"` (PNG 320x180 in `Platforms/Android/Resources/drawable/`), `LEANBACK_LAUNCHER` come secondo `IntentFilter` su `MainActivity`. Verificato sull'emulatore: l'app compare nella riga Apps col banner.
- **Misurato:** l'opzione A è scartata. Sulla UI telefono il focus D-pad raggiunge solo menu e un tab; play, avanti, volume e Schedule non sono raggiungibili (ROADMAP §3). Si va con viste TV dedicate.
- **Prossimo:** T1, sistema di parità con la piattaforma `tv`.
