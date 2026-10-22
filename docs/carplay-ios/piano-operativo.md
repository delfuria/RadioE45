# Apple CarPlay — analisi della situazione e piano operativo

> Data: 18 ottobre 2026
> Stato: bozza da validare. Le domande aperte sono in §9.
> Relazione con i documenti esistenti: questo file aggiorna e corregge [`implementation-plan.md`](implementation-plan.md) (31 luglio 2026). Il piano del 31 luglio resta come storia; in particolare la sua Fase 1 (script post-build) non regge per la distribuzione, vedi §3.2.

---

## 0. Sintesi

- Il codice CarPlay esiste già (`CarPlaySceneDelegate`, routing in `AppDelegate`, `MPRemoteCommandCenter`), ma è **spento**: manifest e entitlement sono commentati, e gli script MSBuild che dovrebbero iniettare il manifest **non esistono più** nel repo.
- Il blocco vero è **esterno**: l'entitlement `com.apple.developer.carplay-audio` non è mai stato richiesto ad Apple. Senza di esso nessun build device o App Store può usare CarPlay. Va richiesto subito, in parallelo a tutto il resto.
- Il secondo blocco è **tecnico**, e non era nel piano del 31 luglio: attivare CarPlay obbliga a passare l'app principale al lifecycle a scene (UIScene). Questo cambia il comportamento dell'app già in produzione, e Apple sta rendendo quel passaggio obbligatorio. Va fatto come step separato e verificato sull'app normale prima di toccare CarPlay.
- Ci sono due bug latenti da correggere prima di qualunque release CarPlay:
  1. Con avvio a freddo da CarPlay, `LiveStreamAudioService.PlayAsync` può non fare nulla in silenzio, perché il player viene creato solo quando `OnAirPage` chiama `Initialize`.
  2. La lista stazioni resta sul placeholder se il catalogo finisce di caricarsi dopo la connessione.
- La scheda `shell.carplay` in `parity/features.yaml` segna `maui: done`, ma il codice è disabilitato. Va corretta a `todo` finché il feature non è attivo.

---

## 1. Situazione attuale (verificata sul codice)

| Elemento | Stato reale | Note |
|---|---|---|
| `Platforms/iOS/CarPlaySceneDelegate.cs` | Presente, compila | Root = `CPListTemplate` "Stazioni". Tap → `PlayAsync` + push `CPNowPlayingTemplate.SharedTemplate`. Placeholder hardcoded `"Caricamento stazioni..."` |
| `Platforms/iOS/AppDelegate.cs` | Presente | `GetConfiguration` instrada il ruolo `CarTemplateApplication`; `SetupRemoteCommandCenter` attivo (play/pause/toggle, seek disabilitati) |
| `Platforms/iOS/SceneDelegate.cs` | Presente | Sottoclasse sottile di `MauiUISceneDelegate`, ma senza manifest non viene usata |
| `Platforms/iOS/Info.plist` | `UIApplicationSceneManifest` **commentato** | Il blocco di scene è scritto dentro un commento XML |
| `Platforms/iOS/Entitlements.plist` | Vuoto | Entitlement CarPlay rimosso |
| `RadioE45.csproj` | `CustomEntitlements` e target `InjectCarPlaySceneManifest` / `ResignAppBundle` **commentati** | Riferiscono a `scripts/inject-carplay-manifest.sh` e `scripts/resign-app-bundle.sh` |
| `scripts/` | **Cartella assente** | Gli script sono stati cancellati nel commit `159dc66` (16 giugno 2026) |
| Entitlement Apple | **Mai richiesto** | Confermato in [`implementation-plan.md`](implementation-plan.md) §2 |
| `IosNowPlayingService` / `RemoteArtworkLoader` | Presenti | Lock screen e Now Playing già gestiti; riutilizzabili per l'artwork in lista |
| `AzuraStationCatalog` (Core) | Solo `LoadAsync()`, **nessun evento** di aggiornamento | Causa del problema del placeholder |
| `App.xaml.cs:71` | Il catalogo viene caricato all'avvio | Sul percorso CarPlay a freddo non è garantito l'ordine |
| `parity/features.yaml` → `shell.carplay` | `maui: done` | Non corrisponde al codice attivo |
| `README.md` | "Apple CarPlay support (iOS)" elencato come feature | Da riformulare finché non è attivo |

---

## 2. Vincoli esterni (Apple)

1. **Entitlement CarPlay Audio.** Va richiesto da [CarPlay Contact Us](https://developer.apple.com/contact/carplay/) nella categoria audio, e l'Account Holder deve accettare il CarPlay Entitlement Addendum. Apple verifica la richiesta con criteri non pubblici. Il tempo di approvazione è variabile e non è controllabile da noi. Dalla documentazione Apple, le app audio devono essere costruite principalmente per il playback audio: una radio in streaming rientra, ma va scritto chiaramente nella richiesta. Fonte: [Requesting CarPlay entitlements](https://developer.apple.com/documentation/carplay/requesting-carplay-entitlements.md).
2. **Provisioning.** Dopo l'approvazione: abilitare la capability "CarPlay Audio" sull'App ID e rigenerare **entrambi** i profili (Development e App Store / Distribution). Un profilo senza la capability fa fallire il code signing.
3. **Solo template.** Le app audio CarPlay non possono disegnare UI propria: si usano solo template (`CPListTemplate`, `CPNowPlayingTemplate`, `CPAlertTemplate`). Questo vincola il design e rende la review Apple più facile.
4. **Limiti di lista.** Il veicolo impone un numero massimo di elementi e sezioni (`CPListTemplate.MaximumItemCount` / `MaximumSectionCount`, da verificare nei valori reali). Se il numero di stazioni AzuraCast supera il limite, serve troncamento, sezioni o ricerca. **Da decidere:** quante stazioni tipicamente ha un utente (vedi §9).
5. **Simulatore CarPlay.** Si apre da Xcode con *I/O → External Displays → CarPlay*. **Da verificare:** se con firma ad-hoc e senza entitlement il simulatore crea davvero la sessione CarPlay. Il piano del 31 luglio lo dà per scontato, ma non è stato provato.
6. **Scene obbligatorie.** Fonti recenti indicano che da iOS 27, con SDK recente, le app devono usare il lifecycle a scene altrimenti non partono. Vedi [Transitioning to the UIKit scene-based life cycle](https://developer.apple.com/documentation/uikit/transitioning-to-the-uikit-scene-based-life-cycle.md) e [Flutter UIScene migration](https://docs.flutter.dev/release/breaking-changes/uiscenedelegate.md) per il contesto. **Da verificare direttamente nella nota TN3187 di Apple** prima di prendere decisioni definitive sulla tempistica.
7. **Mac Catalyst.** CarPlay non esiste su Mac. Il codice resta in `Platforms/iOS/`, quindi non tocca il target `maccatalyst`. Il manifest va verificato anche per l'app Mac, che usa un proprio `Info.plist`.

---

## 3. Limiti e problemi tecnici

### 3.1 Il manifest viene rimosso dal merge MAUI
MAUI rimuove `UIApplicationSceneManifest` durante il merge dei plist. Per questo esisteva lo script di iniezione, che però è sparito dal repo.

### 3.2 Il post-build non funziona per la distribuzione
Il piano del 31 luglio prevede: build → script che modifica `Info.plist` nel bundle → re-firma ad-hoc. Questo va bene per il simulatore, ma **non per device e App Store**:
- Modificare il bundle dopo il codesign con certificato reale invalida la firma, e il bundle non si installa.
- Il file che finisce nell'IPA viene firmato da MAUI durante la build o l'archive. Serve quindi che il manifest sia nel plist **prima** del codesign, cioè dentro il target MSBuild che genera il bundle, non dopo `Build`.
- **Da verificare:** se esiste un hook nei target MAUI per il merge del plist, o se conviene un `PropertyGroup`/target che agisce su `IntermediateOutputPath` prima di `_CodeSign`. Serve uno spike.

### 3.3 Il passaggio a scene cambia l'app principale

**Stato attuale (senza manifest):** iOS non usa scene. L'app ha una sola finestra, creata da MAUI nel ciclo di `AppDelegate`. `Platforms/iOS/SceneDelegate.cs` esiste ma non viene mai chiamato, perché manca il manifest. Il comportamento in produzione è quello classico, già testato per la release.

**Con manifest (scena principale + scena CarPlay)** cambiano i punti seguenti.

1. **Creazione della finestra.** La finestra nasce in `MauiUISceneDelegate.WillConnect`, una volta per scena. Se iOS distrugge la scena e la ricrea (succede spesso quando l'app resta in background a lungo), la finestra viene ricreata.

2. **`CreateWindow` si riesegue e azzera la stazione.** In `RadioE45/App.xaml.cs:79` ad ogni creazione di finestra viene eseguito `_onAirViewModel.CurrentStation = null`. Il commento dice che serve per selezionare di nuovo "preferita/prima" al vero avvio. Con le scene, la ricreazione di finestra può scattare anche quando l'utente torna nell'app dopo una distruzione di scena. Effetto possibile: la radio che stava ascoltando sembra ripartire da zero. **Va reso idempotente o spostato prima di attivare le scene.**

3. **Ciclo di vita diverso.** Foreground, background e ripristino dello stato passano per la scena. Il codice che oggi reagisce agli eventi dell'app (`AppDelegate` e gli eventi di lifecycle MAUI in `RadioE45/MauiProgram.cs:87`) va verificato. Secondo la nota Flutter citata in §10, alcune callback legate allo stato UI sono deprecate e non vengono più chiamate dopo la migrazione.

4. **Audio in background.** La sessione audio è impostata in `AppDelegate.FinishedLaunching`. Non dovrebbe cambiare, ma va provato: riproduzione in background, lock screen, interruzioni (telefonate, Siri), ritorno nell'app.

5. **iPad e più finestre.** `UIApplicationSupportsMultipleScenes = true` (già nel plist commentato) abilita più finestre dell'app su iPad (Stage Manager, split view). `OnAirViewModel` è un singleton: due finestre condividono lo stesso stato audio e possono confondersi. Da decidere se limitare l'app a una finestra, e verificare che questa scelta non comprometta la scena CarPlay.

6. **Disconnessione CarPlay.** Quando CarPlay si disconnette deve chiudersi solo la sua scena, mentre la scena principale resta intatta. Va verificato esplicitamente nei test.

**Impatto:** il rischio riguarda **tutti gli utenti**, anche quelli senza CarPlay. Per questo la Fase A va rilasciata da sola, prima di CarPlay (vedi §5).

**Non verificato:** il comportamento esatto di MAUI nel passaggio da "senza manifest" a "con manifest" (quando viene chiamato `CreateWindow` e come viene gestita una scena già esistente). Va provato sul simulatore con un test di distruzione e ricreazione della scena prima di considerare chiuso il punto.

### 3.4 Avvio a freddo da CarPlay (bug latente)
- `LiveStreamAudioService.PlayAsync` ritorna subito se `_player is null`, senza eccezione né log di errore.
- `_player` viene impostato solo da `IMediaElementHost.Initialize(...)`, chiamato in `Views/OnAirPage.xaml.cs:37`.
- Se CarPlay si connette e l'app non ha ancora creato la pagina (o la finestra principale non esiste, nel caso di sola scena CarPlay), la riproduzione non parte. Il Now Playing viene comunque mostrato.
- Serve che il player sia creato dal Core/Platforms in modo indipendente dalla pagina.

### 3.5 Catalogo senza notifica
`AzuraStationCatalog` non emette eventi. Se `LoadAsync` finisce dopo `DidConnect`, la lista resta sul placeholder fino a una riconnessione. Serve un evento nel Core (`StationsChanged` o equivalente) e l'aggiornamento della lista con `CPListTemplate.UpdateSections`.

### 3.6 Stringhe fuori dai `.resx`
`"Caricamento stazioni..."` e `"Stazioni"` sono hardcoded. Violano la regola 6 di `CLAUDE.md`: vanno in `RadioE45.Core/Resources/Strings/` (neutro, `en`, `pl`).

### 3.7 Errori silenziosi
`PlayStationAsync` logga e basta. Se la riproduzione fallisce, l'utente in auto non vede nulla. Serve un `CPAlertTemplate` o un messaggio nel Now Playing.

### 3.8 Regole di progetto
- Logica di selezione, stato e fallback va nel Core (regola 3). Il delegate CarPlay può solo fare il mapping verso i template.
- Niente sync-over-async (regola 5). Il codice attuale usa `ContinueWith` con `t.Result` dentro un callback: va verificato che non blocchi.

---

## 4. Rischi

| # | Rischio | Prob. | Impatto | Mitigazione |
|---|---|---|---|---|
| R1 | Entitlement negato o molto ritardato | Media | Alto | Richiesta subito; descrizione chiara di app audio/radio; nessuna dipendenza del calendario store da CarPlay |
| R2 | Regressione dell'app principale con il passaggio a scene | Media | Alto | Step A separato, rilasciabile da solo, con test regressione su iPhone e iPad |
| R3 | Firma invalida in Release/App Store per la modifica post-build | Alta (se si segue il piano del 31 luglio) | Alto | Iniezione prima del codesign (§3.2); guard su configurazione e SDK nel target |
| R4 | Riproduzione muta a freddo da CarPlay | Alta | Alto | Fase C prima di qualunque test CarPlay esteso |
| R5 | Placeholder "Caricamento" permanente | Alta | Medio | Fase D |
| R6 | Lista stazioni oltre il limite del veicolo | Media | Medio | Verificare limiti reali; troncamento o sezioni |
| R7 | Rejection in App Review (UI non conforme, test non riproducibile) | Media | Alto | Video demo, note per il reviewer, account di test, rispetto delle linee guida CarPlay |
| R8 | Simulatore non crea la sessione senza entitlement | Media | Medio | Spike in Fase B; se fallisce, il test passa al device con entitlement |
| R9 | Test su auto reale impossibile (nessun veicolo o head unit compatibile) | Media | Medio | Verificare alternative ("CarPlay for iPhone" su secondo dispositivo, accessorio aftermarket) |
| R10 | Rigenerazione profili rompe la build Development esistente | Bassa | Medio | Backup dei profili attuali; provare prima su branch |
| R11 | Divergenza tra `parity/features.yaml` e realtà | Certa (oggi) | Basso | Correggere ora a `todo`; aggiornare solo a `done` dopo il test |
| R12 | Immagini stazione lente o non caricate in lista | Media | Basso | Usare `RemoteArtworkLoader` con cache; fallback su icona generica |

---

## 5. Piano a fasi

Ordine e dipendenze:

```
Fase 0 (entitlement, utente)  ───────────────────────────────►  Fase F (device/App Review)
Fase A (scene app principale) → Fase B (spike manifest) → Fase C (audio a freddo)
                                                        → Fase D (catalogo) → Fase E (stringhe, errori)
                                                                      → Fase F → Fase G (store)
```

### Fase 0 — Richiesta entitlement (bloccante, azione manuale, subito)
1. Compilare la richiesta su CarPlay Contact Us, categoria audio.
2. Il modulo non ha campo di descrizione: developer information, tipo di app Audio, termini.
3. Accettare l'addendum come Account Holder.
4. Registrare la data di invio in questo documento.

### Fase A — Scene per l'app principale (senza CarPlay)
Obiettivo: l'app gira con UIScene e nient'altro cambia.
1. Aggiungere `UIApplicationSceneManifest` con solo la scena principale (`__MAUI_DEFAULT_SCENE_CONFIGURATION__`) e `SceneDelegate`.
2. Spostare su `SceneDelegate` ciò che oggi sta in `AppDelegate` per la finestra e lo stato UI.
3. Test: avvio a freddo, background/foreground, rotazione, ripristino dopo kill, audio che continua in background, lock screen, Android non toccato.
   Test specifici per §3.3: distruzione e ricreazione della scena con radio in riproduzione (la stazione non deve ripartire da zero); più finestre su iPad; interruzioni audio.
4. Rilasciabile da sola come versione store, se i test passano. Questo riduce il rischio di avere tutto insieme.

### Fase B — Spike iniezione manifest (solo build, niente CarPlay)
1. Verificare nei target MAUI dove viene fatto il merge del plist e se esiste un punto di estensione prima del codesign.
2. Implementare `scripts/inject-carplay-manifest.sh` (o un target MSBuild equivalente) con idempotenza e errore esplicito se il plist non è trovato.
3. Guard: eseguire solo per iOS, e per simulatore/Debug nella prima versione. Per Release/Distribution il manifest deve entrare nel plist sorgente o nel plist intermedio, **prima** della firma.
4. Verificare con `codesign --verify --deep --strict` e con il contenuto dell'IPA (`PlistBuddy`) che il manifest sia presente e la firma valida.
5. Su simulatore, provare se la scena CarPlay si connette con firma ad-hoc (R8).

### Fase C — Audio pronto a freddo
1. Spostare la creazione del player fuori da `OnAirPage`: il player deve esistere quando il servizio audio è risolto dal DI, con un punto di inizializzazione nel Core o nel layer piattaforma iOS.
2. `PlayAsync` senza player non deve più tornare in silenzio: deve restituire o loggare un errore.
3. Verificare che il percorso non cambi Android (il player Android passa per `RadioPlaybackService`).
4. Test: da app chiusa, avvio da CarPlay → audio parte.

### Fase D — Catalogo reattivo
1. In `AzuraStationCatalog` (Core) aggiungere un evento `StationsChanged` emesso dopo `LoadAsync`.
2. `CarPlaySceneDelegate` si iscrive e aggiorna la root con `UpdateSections`; si disiscrive in `DidDisconnect`.
3. Gestire: catalogo vuoto con errore di rete (stato con retry), catalogo con più stazioni del limite del veicolo.
4. Test unitari nel Core per l'evento; nessun riferimento UI nel Core (regola 4).

### Fase E — Stringhe, errori, artwork
1. Spostare placeholder, titolo e messaggi di errore nei `.resx` del Core (`en`, `it`, `pl`); vedi `docs/LOCALIZATION.md`.
2. Errore di riproduzione → `CPAlertTemplate` breve.
3. Artwork in `CPListItem` via `RemoteArtworkLoader` con cache; fallback su immagine generica dell'app (già presente per il lock screen).
4. Indicatore "in riproduzione" nella lista, solo se l'API supportata lo consente.

### Fase F — Test
**Simulatore (dopo B):**
- Connessione CarPlay, lista visibile, tap avvia riproduzione, push Now Playing.
- Metadata e controlli dal Now Playing e dal Control Center.
- Disconnessione e riconnessione, l'app principale resta utilizzabile.

**Device reale (richiede entitlement e profili):**
- Build Release firmata con profilo che include CarPlay Audio.
- Test in auto compatibile o accessorio (R9).
- Regressione completa dell'app principale con CarPlay connesso/disconnesso.
- Test cold start da CarPlay con app chiusa e telefono bloccato.

Ogni test va registrato (screenshot della finestra app o del simulatore, secondo la regola del progetto; per la finestra CarPlay usare l'ID finestra, non lo schermo intero).

### Fase G — Parity, documentazione e store
1. Subito: `parity/features.yaml` → `shell.carplay` a `maui: todo` con nota "in attesa entitlement Apple", e `README.md` riformulato. Da correggere subito, indipendentemente dal resto.
2. Dopo il test: aggiornare a `done`, rigenerare con `tools/ParityCheck`, eseguire `dotnet build` iOS.
3. Documentazione: `docs/carplay-ios/README.md` tecnico (architettura, file coinvolti), sezione README bilingue, come per Android Auto.
4. Store: testi in inglese (regola del progetto), note per il reviewer con istruzioni per provare CarPlay, video demo. Versione: nuova `ApplicationDisplayVersion` (oggi `0.46`), seguendo `docs/VERSION-BUMP.md`.
5. Commit: non eseguire `git commit`; proporre messaggio in inglese con trailer `Parity:` o `Parity-Debt:` secondo la regola 9 di `CLAUDE.md`.

---

## 6. Decisioni già prese (da confermare)
- **Root = lista stazioni**, Now Playing in push dopo il tap: si mantiene, come suggerito nel piano del 31 luglio.
- **Ordine delle fasi**: scene (A) prima di CarPlay, perché tocca l'app già in produzione.
- **Niente UI propria**: solo template, come richiesto da Apple.

---

## 7. File coinvolti (previsione)

| File | Fase | Modifica |
|---|---|---|
| `RadioE45/Platforms/iOS/Info.plist` | A, B | Manifest scene (principale + CarPlay) |
| `RadioE45/Platforms/iOS/Entitlements.plist` | F | `com.apple.developer.carplay-audio` |
| `RadioE45/RadioE45.csproj` | B | Target di iniezione prima del codesign, guard |
| `scripts/inject-carplay-manifest.sh` | B | Nuovo (se si sceglie lo script) |
| `RadioE45/Platforms/iOS/SceneDelegate.cs` | A | Gestione finestra e stato UI |
| `RadioE45/Platforms/iOS/AppDelegate.cs` | A | Rimuovere logica di finestra |
| `RadioE45/Platforms/iOS/CarPlaySceneDelegate.cs` | D, E | Aggiornamento lista, errori, stringhe |
| `RadioE45/Views/OnAirPage.xaml.cs` | C | Non più unica porta di inizializzazione del player |
| `RadioE45.Core/Services/Audio/LiveStreamAudioService.cs` | C | Player indipendente dalla pagina; niente silenzio su `_player is null` |
| `RadioE45.Core/Services/Radio/AzuraStationCatalog.cs` | D | Evento `StationsChanged` |
| `RadioE45.Core/Resources/Strings/*.resx` | E | Nuove stringhe |
| `parity/features.yaml` | G | `shell.carplay` |
| `README.md`, `docs/carplay-ios/README.md` | G | Documentazione |

---

## 8. Checklist azioni manuali (utente)
- [x] Inviare la richiesta entitlement CarPlay Audio (Fase 0): inviata e approvata il 21 ottobre 2026.
- [x] Dopo approvazione: capability sull'App ID; rigenerare i profili (sono tre: Development, App Store iOS, **Mac App Store**, stesso App ID). Procedura: [`provisioning-profiles.md`](provisioning-profiles.md).
- [ ] Prove su device: [`device-test-plan.md`](device-test-plan.md) (senza auto: CarPlay Simulator del Mac con iPhone via cavo).
- [ ] Preparare video demo e note per il reviewer.

---

## 9. Domande aperte

1. **Entitlement**: la richiesta è già partita? Se no, la invii tu (serve l'Account Holder) oppure preferisci che prepari il testo della richiesta in inglese?
2. **Numero di stazioni**: quante stazioni può avere un utente tipico? Serve la lista completa, sezioni, o una selezione di preferite?
3. **Test su auto**: hai accesso a un veicolo compatibile o a un accessorio CarPlay?
4. **Scene per l'app principale**: sei d'accordo a rilasciare prima la migrazione a scene come versione separata, senza CarPlay, per ridurre il rischio?
5. **Versione target**: quale versione store vuoi usare per il rilascio con CarPlay (es. `0.47`)?
6. **Priorità dell'artwork**: va incluso nella prima release o può rimandarsi?
7. **Mac**: confermi che l'app Mac Catalyst non deve avere nulla di CarPlay (nessun manifest o entitlement)?

---

## 10. Fonti
- Apple, [Requesting CarPlay entitlements](https://developer.apple.com/documentation/carplay/requesting-carplay-entitlements.md)
- Apple, [Transitioning to the UIKit scene-based life cycle](https://developer.apple.com/documentation/uikit/transitioning-to-the-uikit-scene-based-life-cycle.md)
- Flutter, [UIScene migration](https://docs.flutter.dev/release/breaking-changes/uiscenedelegate.md) (fonte secondaria, usata solo per il contesto del cambio di lifecycle)
- Apple, CarPlay Contact Us: <https://developer.apple.com/contact/carplay/>
- Codice: `RadioE45/Platforms/iOS/*`, `RadioE45/RadioE45.csproj` (blocchi commentati), `RadioE45.Core/Services/Audio/LiveStreamAudioService.cs`, `RadioE45.Core/Services/Radio/AzuraStationCatalog.cs`, `RadioE45/Views/OnAirPage.xaml.cs`

---

## 11. Registro avanzamento

### 21 ottobre 2026
- **G.1 fatto:** `shell.carplay` a `maui: todo`, README riformulato.
- **Fase 0 (inviata il 21 ottobre 2026):** il modulo Apple ha solo developer information, tipo di app (Audio) e termini: nessun campo di descrizione. [`entitlement-request.md`](entitlement-request.md) riassume i passi e i punti da tenere per App Review.
- **Risposte alle domande aperte:** iPad con una sola finestra (§3.3 punto 5); circa 4 stazioni per utente, quindi lista semplice, niente sezioni né troncamento (§3.5, R6).
- **Fase A, in corso:**
  - `Platforms/iOS/Info.plist`: attivo `UIApplicationSceneManifest` con la sola scena principale, `UIApplicationSupportsMultipleScenes=false`.
  - `App.xaml.cs` `CreateWindow`: su iOS non azzera più `CurrentStation` (con le scene la finestra può essere ricreata a processo vivo). Android, Windows e Mac invariati.
  - Verificato: build `net10.0-ios` Debug simulatore 0 errori 0 avvisi; il manifest è presente nel bundle; l'app parte su iPhone 16e (iOS 26.2) e mostra la finestra principale.
  - Test sul simulatore (iPhone 16e, iPad Air 13" M3, iOS 26.2):
    - avvio a freddo: finestra principale visibile, la radio parte e il Now Playing si popola;
    - background 20 s con Radio E45 in riproduzione e ritorno in primo piano: stesso processo, stessa stazione, pulsante ancora su pausa, nessun passaggio a `Stopped` nei log;
    - iPad: una sola finestra a tutto schermo, avvio corretto.
  - **Non verificato:** distruzione e ricreazione della scena a processo vivo (il simulatore non offre un comando per forzarla; resta il caso che la modifica a `CreateWindow` dovrebbe coprire), lock screen, interruzioni audio (telefonata, Siri), rotazione, ripristino dopo kill. Da provare su device reale.
  - Errori HTTP nei log sulla stazione demo (`demo.azuracast.com`): non legati alle scene.
- **Correzione a §3.1 e §3.2:** MAUI **non** rimuove `UIApplicationSceneManifest` dal merge dei plist; il manifest del sorgente arriva nel bundle. Un bundle senza manifest visto all'inizio dipendeva da `AppManifest.plist` intermedio non rigenerato (build incrementale). Quindi la Fase B probabilmente non richiede script post-build né re-firma: basta aggiungere la scena CarPlay al plist sorgente e l'entitlement. Da confermare con lo spike.

- **Fase C, fatta (stesso giorno):**
  - Nuovo `Platforms/iOS/AvStreamPlayer.cs`: player live su `AVPlayer`, senza pagina. Metadata e lock screen restano a `IosNowPlayingService`.
  - `AudioService` su iOS crea e collega `AvStreamPlayer` nel costruttore; `OnAirPage` su iOS rimuove il `MediaElement` (come Android). Mac Catalyst e Windows invariati (`MediaElement`).
  - `LiveStreamAudioService`: `PlayAsync` e `ResumeAsync` senza player non tornano più in silenzio, scrivono un avviso nel log (nessuna stringa utente nuova).
  - Test: nuovo test del Core (255/255 verdi); build `ios`, `maccatalyst`, `android` 0 errori 0 avvisi; `ParityCheck` senza disallineamenti.
  - Simulatore iPhone 16e: avvio, Radio E45 parte (Opening, Paused, Buffering, Playing in circa 1,2 s), pausa e ripresa dal pulsante.
  - **Non verificato:** avvio a freddo vero da CarPlay (serve la Fase B), interruzioni audio (telefonata, Siri) e riproduzione con schermo bloccato su device reale. Con `AVPlayer` un'interruzione mette il player in `Paused` e il watchdog (10 s) può tentare di riaprire lo stream: comportamento identico al `MediaElement` di prima, che usava lo stesso `AVPlayer`, ma da provare.

- **Fase D, fatta (stesso giorno):**
  - Correzione a §3.5: il catalogo **ha già** `StationsRefreshed`, ma non scatta dopo il primo `LoadAsync` (solo dopo `ReloadAsync` e dopo il recupero delle stazioni offline). Non l'ho fatto scattare dopo il primo caricamento: lo ascoltano `OnAirViewModel`, `RadioListViewModel` e `AppMenuViewModel`, e `OnAirViewModel` sceglierebbe la stazione predefinita in corsa con la sua inizializzazione, cambiando il comportamento dell'app principale.
  - Soluzione: nuovo `RadioE45.Core/Services/Radio/CarStationList.cs` (Core, senza UI). Attende `catalog.LoadAsync()` (idempotente, condivide il caricamento già avviato, quindi sicuro anche all'avvio a freddo da CarPlay), si iscrive a `StationsRefreshed` e espone stato (`Loading`, `Ready`, `Empty`), voci, titolo e testo del segnaposto. Registrato nel DI come transient.
  - `CarPlaySceneDelegate` ora: mostra il segnaposto, chiama `LoadAsync`, e su `Changed` aggiorna la lista con `UpdateSections` sul main thread. Tolto il `t.Result` (regola 5).
  - **Anticipato dalla Fase E:** stringhe `CarPlay_Title`, `CarPlay_Loading`, `CarPlay_NoStations` nei tre `.resx` (neutro italiano, `en`, `pl`). Il segnaposto "Caricamento stazioni..." e "Stazioni" non sono più hardcoded.
  - Test: 8 nuovi test del Core (263/263 verdi); build `ios`, `maccatalyst`, `android` e Uno desktop senza errori né avvisi; `ParityCheck` senza disallineamenti.
  - **Non verificato:** l'aggiornamento della lista su CarPlay (serve la scena, Fase B).

---

## 12. Da confermare e da verificare

Cose rimaste aperte nelle fasi fatte o decise per default. Spuntare quando risolte.

### Decisioni da confermare con l'utente
- [ ] **Release in due tempi** (§9 domanda 4): scene + player AVPlayer (Fasi A e C) come versione a sé, senza CarPlay, poi CarPlay. Consigliato; non ancora confermato.
- [ ] **Versione target** per il rilascio con CarPlay (§9 domanda 5).
- [ ] **Artwork nella lista** in prima release o dopo (§9 domanda 6).
- [ ] **Mac Catalyst senza CarPlay** (§9 domanda 7): nessun manifest né entitlement. Previsto così, non confermato.
- [ ] **Stazioni offline nella lista in auto:** oggi `CarStationList` le mostra tutte. Decidere se nasconderle o segnarle, perché toccarne una non farebbe partire nulla.
- [ ] **Sottotitolo "in riproduzione":** si aggiorna solo quando cambia il catalogo, non a ogni brano (`INowPlayingService.NowPlayingUpdated` non è collegato). Decidere se serve.
- [ ] **iPad, una sola finestra:** deciso, ma con `UIApplicationSupportsMultipleScenes=true` (necessario per la scena CarPlay) l'iPad potrebbe aprire più finestre. Da risolvere in Fase B.

### Da verificare (test)
- [x] **Telefonata** (22 ottobre): con la build attuale pausa allo squillo e ripresa dopo la chiusura, confermato dai log. Facoltativo: gestione esplicita delle interruzioni per il caso app in primo piano.
- [x] **Ritardo di circa 20 s in CarPlay con MP3:** risolto con il motore in-process (22 ottobre). Storico: misurato (9 avvii 17-22 s; Musica 0,2-0,7 s; HLS 1,2 s). Safari con lo stesso MP3 parte subito (osservazione utente): cercare l'impostazione del player, una variabile alla volta, misurando in `airplayd`.
- [ ] **"Preferisci HLS" ignorato** se l'ultimo URL riuscito è l'MP3 (`GetCandidateUrls`).
- [x] **Widget CarPlay senza tempi:** comportamento di sistema, anche Musica non li mostra (22 ottobre).
- [x] **Now Playing fermo su ▶:** limite del simulatore; sul device lo stato è corretto (22 ottobre).
- [x] **Scollegamento CarPlay / cuffie:** pausa su `OldDeviceUnavailable` (22 ottobre); B8 e B9 superate sul device. Ripresa al ricollegamento = comando Play di sistema.
- [ ] **iPad con `UIApplicationSupportsMultipleScenes=true`:** verificare se si possono aprire più finestre e, se sì, limitarle a una.
- [ ] **Fase A:** distruzione e ricreazione della scena a processo vivo; lock screen; interruzioni audio; rotazione; ripristino dopo kill. Su device reale.
- [x] **Fase C:** avvio a freddo da CarPlay con audio (simulatore, 21 ottobre).
- [ ] **Fase C:** telefonata e Siri; riproduzione con schermo bloccato; comportamento del watchdog dopo un'interruzione.
- [x] **Fase D:** aggiornamento della lista dal vivo su CarPlay (avvio a freddo, 21 ottobre).
- [ ] **Fase D:** catalogo vuoto o in errore, su CarPlay.
- [ ] **Mac Catalyst e Windows** dopo il cambio a `AudioService` (build ok, ma riproduzione non riprovata dopo la Fase C).
- [ ] **Limiti del veicolo** (`CPListTemplate.MaximumItemCount`): con circa 4 stazioni non dovrebbe servire nulla, ma il valore reale non è stato letto.

### Da correggere nel piano
- [x] §3.1 e §3.2 (MAUI rimuove il manifest, serve uno script post-build): smentiti in Fase A e B, vedi `simulator-testing.md` §5.
- [ ] §1: la riga su `AzuraStationCatalog` ("nessun evento") va corretta come in Fase D.

### Fasi ancora da fare
- [x] **Fase B:** scena CarPlay nel plist, entitlement per simulatore, test sul simulatore (R8 risolto: serve l'entitlement nell'eseguibile).
- [ ] **Fase E:** errore di riproduzione con `CPAlertTemplate`; artwork; indicatore "in riproduzione". (Le stringhe sono già nei `.resx`.)
- [ ] **Fase F:** test su simulatore CarPlay e su device con entitlement.
- [x] **Fase G:** `shell.carplay` a `done`, README, `docs/carplay-ios/README.md`, testi store e note App Review (22 ottobre). Versione e TestFlight con `/release-prep` e la prova C.
- [x] **Dopo l'approvazione Apple (approvata il 21 ottobre):** capability sull'App ID, profili Development e App Store, entitlement nel plist.

### 21 ottobre 2026 (sera)
- **Fase 0 chiusa:** Apple ha assegnato l'entitlement CarPlay Audio all'account (email di conferma lo stesso giorno). R1 superato. Restano i passi manuali di §8: capability sull'App ID e rigenerazione dei profili.

- **Fase B, fatta sul simulatore (stessa sera).** Procedura e diagnostica in [`simulator-testing.md`](simulator-testing.md).
  - `Info.plist`: manifest con scena principale e scena CarPlay, `UIApplicationSupportsMultipleScenes=true`. Tolto il vecchio blocco commentato.
  - `RadioE45.csproj`: tolti i commenti obsoleti su script post-build e re-firma; le build per simulatore usano `Platforms/iOS/Entitlements.Simulator.plist` (con `carplay-audio`, senza profilo). Device e App Store restano su `Entitlements.plist`, senza CarPlay fino ai nuovi profili (verificato con `-getProperty:CodesignEntitlements`).
  - **Primi tentativi falliti, cause misurate:** (1) icona assente perché l'eseguibile non era stato rilinkato e non conteneva l'entitlement (cache del link basata sul percorso); (2) click finito su Messaggi perché `System Events click at` non è un click vero; (3) menu CarPlay disabilitato per simulatore non pronto. La re-firma a mano non serviva: su simulatore conta la sezione `__entitlements`. Dettagli in `simulator-testing.md` §5.
  - **Verificato (iPhone 16e, iOS 26.2):**
    - icona RadioE45 nella home CarPlay; "Application declares audio entitlement" nei log;
    - lista "Stazioni" con le 4 stazioni; la stazione in onda ha il brano come sottotitolo;
    - tap su Radio Cartoni: riproduzione (`Playing` in 0,6 s) e push del Now Playing con titolo e artista;
    - **avvio a freddo da CarPlay** (app terminata, 0 processi): viene creata solo la scena CarPlay; la lista è impostata prima del caricamento del catalogo (00.934 contro 01.310-01.727) e si aggiorna da sola (Fase D); tap su una stazione: `Playing` in 0,7 s senza finestra principale (Fase C);
    - apertura dell'app sul telefono durante la riproduzione da CarPlay: stessa istanza, stessa stazione, nessun riavvio dello stream;
    - scollegamento CarPlay: scena chiusa, processo e audio vivi, app principale utilizzabile.
  - **Problema aperto, Now Playing di CarPlay:** il pulsante resta su ▶ e il tempo non avanza, mentre l'audio suona. Misura: in `mediaremoted` lo stato di RadioE45 passa a `Paused` alla registrazione dei comandi remoti e non diventa mai `Playing`; cambia solo `playbackRate` (0 → 1). `MPNowPlayingInfoCenter.playbackState` è solo macOS (documentazione Apple; assente nel binding iOS), quindi non è quella la correzione (tentativo annullato). Ipotesi da verificare: sul simulatore il sistema non rileva l'audio attivo, oppure serve `MPNowPlayingSession` collegato all'`AVPlayer`. Prima verificarlo su device reale, poi decidere.

### 22 ottobre 2026 — prime prove su iPhone 12 mini (build 0.47.9 = `e49b1cc`, Debug, profilo Team)
- **B1 KO, causa misurata:** la build installata non ha `carplay-audio` (firma col profilo Team; `Entitlements.plist` vuoto; profili ancora di giugno, senza CarPlay). Atteso finché non sono fatti P1 e P2 di `device-test-plan.md`. Il brano nella home CarPlay è il widget di sistema "In riproduzione", che funziona per qualunque app audio.
- **A7 KO, telefonata:** l'audio si abbassa invece di fermarsi. Misure: la sessione è `Playback` senza opzioni (non mixabile) sia nell'app sia nel `MediaElement` di CommunityToolkit (decompilato: `SetCategory(Playback)`), quindi non è una sessione mixabile. Fatti certi nel codice: su iOS nessuna gestione delle interruzioni; il watchdog tratta `Paused` come stream bloccato e riapre ogni 10 s. Ipotesi principale: il watchdog riapre lo stream durante la chiamata. Da confermare con i log; possibile anche una chiamata VoIP (WhatsApp), che abbassa invece di interrompere.
- **Ritardo di circa 15 s all'avvio e dopo pausa/play** sentendo l'audio da CarPlay Simulator; la pausa è immediata. Da misurare: tempo di scelta URL e tempo apertura→`Playing` (nuovi log), confrontati con il telefono senza CarPlay Simulator.
- **Widget CarPlay senza tempi:** probabilmente legato allo stato Now Playing (`Paused` in `mediaremoted`, registro Fase B). Da misurare con A6.
- **Strumenti aggiunti (solo log, nessun cambio di comportamento):** `Platforms/iOS/AudioSessionDiagnostics.cs` (categoria e opzioni, interruzioni, cambi di uscita, reset dei servizi media), `AvStreamPlayer` (ms dall'apertura a `Playing`), `LiveStreamAudioService` (URL scelto e ms della scelta). Verificati sul simulatore: scelta URL 773 ms, `Playing` 4409 ms dopo l'apertura.

### 22 ottobre 2026 — capability CarPlay sull'App ID e profili rigenerati
- Portale: CarPlay Audio attivo su `com.radioe45.app`. Rigenerati `Radio E45 AppStore` e `Radio E45 Mac AppStore`; creato il profilo manuale `Radio E45 Development` (iPhone e iPad). Verifica §4.4 di `provisioning-profiles.md`: i tre profili nuovi hanno `carplay-audio`. Il vecchio `Radio E45 AppStore` di giugno (stesso nome) è stato tolto dalla cartella dei profili e spostato nella copia `~/Documents/RadioE45-profili-backup-20261022`.
- `Platforms/iOS/Entitlements.plist` ora contiene `com.apple.developer.carplay-audio`. `RadioE45.csproj`: nuova firma esplicita per Debug su device (`-r ios-*`): `Apple Development`, `Radio E45 Development`, `Entitlements.plist`.
- Misure con `codesign -d --entitlements -` e profilo incorporato:
  - Debug `ios-arm64`: `carplay-audio` = 1, `get-task-allow` = 1, profilo `Radio E45 Development`;
  - Release `ios-arm64`: `carplay-audio` = 1, `get-task-allow` = 0, profilo `Radio E45 AppStore`;
  - Release Mac Catalyst: firmata con `Radio E45 Mac AppStore`, entitlement invariati (niente CarPlay), `codesign --verify --deep --strict` ok.
- Prossimo passo: P3 e seguenti di `device-test-plan.md` (installare la build Debug sull'iPhone e ripetere B1).

### 22 ottobre 2026 — analisi dei log del telefono (`log collect`, 11:35-11:42)
Nota strumento: nella shell zsh `log` è una funzione; usare `/usr/bin/log show <archivio>`.
- **Avvio sull'altoparlante: OK.** Scelta URL 10-34 ms, `Playing` 564-738 ms dopo l'apertura (6 misure). L'utente sente circa 1 s.
- **Telefonata: OK con questa build.** Squillo 11:37:59.9 → `audiomxd` interrompe RadioE45 (`Ringtone` priorità 10 contro `NonMixable` priorità 0), sfumatura 0,7 s a zero, `STOPPED`; risposta 11:38:16.9; fine chiamata 11:38:53.8; `End Interruption, ShouldResume` 11:38:55.3; `Playing` 11:38:56.1. In `mediaremoted`: `Playing → Interrupted → Playing`. Il watchdog non ha riaperto durante la chiamata (app sospesa). La gestione esplicita delle interruzioni resta consigliata per il caso app in primo piano (Siri, sveglia), ma non c'è un difetto misurato.
- **Now Playing fermo su ▶ (simulatore): chiuso.** Sul device lo stato segue la riproduzione (`Paused`/`Playing`/`Interrupted`). Era un limite del simulatore.
- **CarPlay, audio dopo circa 20 s: causa a valle dell'app.** L'app è `Playing` in meno di 0,8 s anche con uscita `CarAudio`; il motore AirPlay bufferizzato di `airplayd` (`APAudioEngineBuffered`) resta in `Prerolling` con buffer 0% e passa a `Started` dopo 17-20 s (11:40:17.8 → 11:40:38.1; 11:41:16.1 → 11:41:33.0; route CarPlay 11:39:42 → 11:40:01.3). La pausa è immediata (12 ms). Ipotesi: il motore attende un buffer minimo che uno stream MP3 dal vivo riempie solo in tempo reale. Previsione: con HLS (segmenti già pronti) la partenza è molto più rapida.
- **Widget CarPlay senza tempi:** il Centro di Controllo dell'iPhone li mostra, quindi i metadati ci sono. Da confrontare con un'app Apple sullo stesso CarPlay Simulator.

### 22 ottobre 2026 — esperimenti di confronto (utente)
- Musica (Apple), Safari con lo stesso URL MP3 di Radio Casa e RadioE45 con "Preferisci HLS": audio subito su CarPlay. RadioE45 con MP3: circa 20 s, anche aprendo l'app da CarPlay. Quindi il ritardo dipende dalla combinazione `AvStreamPlayer` + MP3 progressivo sul percorso CarPlay, non da iOS in generale. Il sorgente WebKit (`MediaPlayerPrivateAVFoundationObjC.mm`) non mostra impostazioni diverse ovvie (`automaticallyWaitsToMinimizeStalling` lasciato attivo, nessun `preferredForwardBufferDuration`): confronto da fare nei log (`radioe45-prove2.logarchive`).
- Copertina, dettagli del brano e cambio stazione su CarPlay: sempre corretti. **I tempi nel widget CarPlay mancano ancora** (presenti nel Centro di Controllo dell'iPhone): confronto con Musica nello stesso archivio.

### 22 ottobre 2026 — analisi di `radioe45-prove2.logarchive` (11:30-12:10)
Metodo: per ogni `cmsSetIsPlaying … starting` in `audiomxd`, tempo fino al primo `Started` del motore CarPlay in `airplayd` (`APAudioEngineBuffered`).

| Sorgente | Avvii | Tempo fino a `Started` |
|---|---|---|
| Musica (Apple) | 5 | 0,17-0,69 s |
| RadioE45, HLS (AzuraTest Radio, `demo.azuracast.com/hls/.../live.m3u8`) | 1 | 1,16 s |
| RadioE45, MP3 (Radio Casa LAN, Radio E45, `radioe45.ddns.net`) | 9 | 17-22 s |

L'app è `Playing` 0,55-1,26 s dopo l'apertura in tutti i casi, anche con uscita `CarAudio`.
- **Safari:** l'utente osserva partenza immediata con lo stesso MP3; non c'è nei log e si accetta l'osservazione (decisione utente: non cercarla). Quindi la sola spiegazione "MP3 dal vivo arriva in tempo reale" non basta: conta anche la configurazione di `AvStreamPlayer` (`AVPlayer` con impostazioni predefinite).
- **Difetto trovato (misurato):** con "Preferisci HLS" attivo Radio Casa ha usato sempre l'MP3, perché `LiveStreamAudioService.GetCandidateUrls` mette per primo `OnAirStreamUrl` (l'ultimo URL riuscito) prima dell'ordine scelto dall'impostazione.

### 22 ottobre 2026 — prova 1 e prova 2 sul ritardo CarPlay
- **Prova 1, `AutomaticallyWaitsToMinimizeStalling = false`: falsificata.** Build verificata nel log; MP3 su CarPlay +20,0 / +22,0 / +22,0 s (`radioe45-prova1.logarchive`). Modifica tolta.
- **Differenza trovata:** per Safari (`com.apple.WebKit`, 12:16:41) `airplayd` apre uno stream CarPlay in tempo reale (`APEndpointStreamCarPlayAudio` "AudioMain: Setup succeeded", `audioType media`, `audioLatencyMs 32`, `type 100`) e il motore bufferizzato non registra nessun `Started`. RadioE45 invece si registra come client di AirPlay bufferizzato (`FigBufferedAirPlayClientRoutingRegistry`) e passa da `APAudioEngineBuffered`, che con un MP3 dal vivo resta in preroll circa 20 s. WebKit imposta `AVPlayer.allowsExternalPlayback` (`MediaPlayerPrivateAVFoundationObjC.mm`).
- **Prova 2, `AllowsExternalPlayback = false`:** in corso. Previsione: "AudioMain: Setup succeeded … type 100" anche per RadioE45, nessun preroll bufferizzato, audio in 1-2 s. Da verificare anche: avvio sull'altoparlante invariato; AirPlay verso altri altoparlanti (se disponibile), che con questa impostazione passerebbe in tempo reale.
- **Prova 2, `AllowsExternalPlayback = false`: falsificata.** Build verificata; `airplayd` registra comunque RadioE45 come client di AirPlay bufferizzato a ogni avvio; MP3 su CarPlay +20 / +22 / +22 / +22 s (`radioe45-prova2.logarchive`). AirPlay verso un altoparlante: funzionante (utente). Modifica tolta.
- **Causa individuata (misurata):** in `audiomxd`, all'avvio, Safari ha `IsPlayingOutput:YES` e RadioE45 `IsPlayingOutput:NO`; tutto il resto coincide (`MediaPlayback/Default`, `NonMixable`, `System Music`, `NowPlayingApp:YES`). Safari produce l'audio nel proprio processo e CarPlay lo riceve in tempo reale (`type 100`, 32 ms); il nostro `AVPlayer` consegna lo stream al percorso AirPlay bufferizzato (`APAudioEngineBuffered`), che con un MP3 dal vivo deve accumulare circa 20 s. HLS (+1-3 s) e Musica (+0,2-0,7 s) riempiono il buffer subito. È una differenza di motore, non di parametro: niente altre prove a parametri.
- **Opzioni (decisione utente):**
  1. **HLS quando la stazione lo offre**, correggendo `GetCandidateUrls` perché "Preferisci HLS" (o una preferenza automatica con uscita CarPlay) non sia scavalcato dall'ultimo URL riuscito. Misurato: +1-3 s. Non risolve le stazioni solo MP3 (oggi Radio Cartoni, Radio E45).
  2. **Motore MP3 in-process** (download dello stream + decodifica con AudioToolbox, uscita con `AVAudioEngine` o `AudioQueue`), cioè audio prodotto nell'app come Safari. Previsione: percorso in tempo reale e avvio in 1-2 s anche per MP3. Lavoro significativo e da tenere su iOS soltanto.
  3. **Accettare il ritardo per gli MP3** e consigliare HLS ai gestori delle stazioni (AzuraCast lo attiva per stazione).

### 22 ottobre 2026 — motore MP3 in-process (decisione utente)
- Documento del problema e testi per le issue: [`carplay-mp3-start-delay.md`](carplay-mp3-start-delay.md).
- Nuovi file iOS: `Platforms/iOS/AudioQueueStreamPlayer.cs` (`NSUrlSession` → `AudioFileStream` → `OutputAudioQueue`, 16 buffer da 8 KB, partenza dopo 2 buffer, `Buffering` solo dopo 2 s senza dati, interruzione → `Paused`), `Platforms/iOS/IosStreamPlayer.cs` (`.m3u8` → `AvStreamPlayer`, il resto → `AudioQueueStreamPlayer`). `AudioService` su iOS usa `IosStreamPlayer`. `RadioE45.csproj`: `AllowUnsafeBlocks` solo per iOS (l'allocazione dei buffer con `nint` è obsoleta in .NET for iOS).
- Simulatore iPhone 16e: MP3 via Internet, audio 285-396 ms dopo l'apertura (3 avvii), pausa immediata, cambio stazione corretto. Corretto uno sfarfallio Playing → Buffering all'avvio: con l'MP3 la coda restituisce i buffer appena decodificati, non suonati.
- Build `ios`, `maccatalyst`, `android` senza errori né avvisi. Installata su iPhone 12 mini.
- **Da misurare sul device:** `IsPlayingOutput:YES` in `audiomxd`; in `airplayd` "AudioMain: Setup succeeded … type 100" e nessun preroll del motore bufferizzato; audio in CarPlay entro 1-2 s; regressioni (altoparlante, telefonata, background e lock screen, cambio rete, HLS ancora con `AVPlayer`).

### 22 ottobre 2026 — esito del motore in-process sul device
- **CarPlay: l'MP3 parte subito (utente).** Log: `audiomxd` `NowPlayingApp:YES IsPlayingOutput:YES`; avvio 136-606 ms dopo l'apertura (un caso 1353 ms); `mediaremoted` Playing/Paused/Interrupted corretti. Lock screen e Centro di Controllo regolari.
- **Now Playing su CarPlay:** inizialmente segnalato come assente; i log del processo `CarPlay` mostravano etichette, avanzamento e copertina ricevuti, e la schermata catturata era con la radio ferma (Now Playing svuotato di proposito). Con la radio in riproduzione: tutto visibile (utente).
- **⏮/⏭ non cambiavano stazione:** in `AppDelegate.SetupRemoteCommandCenter` `NextTrackCommand`/`PreviousTrackCommand` erano disattivati. Ora attivi e collegati a `OnAirViewModel.NextStationCommand`/`PreviousStationCommand` (stessa logica della UI e di Android Auto: stazione adiacente, con giro). Seek resta disattivato. **Verificato dall'utente: i tasti cambiano stazione.**
- **Tempi nel widget della dashboard CarPlay:** assenti; il processo `CarPlay` riceve comunque durata e tempo trascorso ("Progress view set duration …") e la schermata In riproduzione li mostra. **Chiuso:** anche Musica non mostra i tempi nel widget (utente, 22 ottobre): è il comportamento del widget di sistema, non un difetto di RadioE45.
- `CHANGELOG.md` `[Unreleased]`: righe `New` (successivo/precedente dai comandi di sistema) e `Fixed` (avvio rapido degli MP3 su CarPlay).

### 22 ottobre 2026 — prove A e B su iPhone con il motore in-process
- **P1 e P2 chiusi:** profili `Radio E45 Development`, `Radio E45 AppStore`, `Radio E45 Mac AppStore` rigenerati il 22 ottobre con `carplay-audio`; commit `9432d63` firma le build per device con l'entitlement (verificato con `codesign`). L'icona RadioE45 compare in CarPlay.
- **Parte A (A1-A12): tutte superate** (utente).
- **Widget dashboard senza tempi:** anche Musica non li mostra; comportamento di sistema.
- **B5, avvio a freddo da CarPlay:** compare la lista, nessuna riproduzione automatica. È il comportamento del codice (la scena CarPlay non avvia l'audio). Guida CarPlay di Apple (2025-06-09): l'audio parte quando l'utente sceglie un elemento della lista; non c'è un divieto esplicito di avvio automatico. **Decisione utente (22 ottobre): lasciarlo così** (lista, l'audio parte alla scelta della stazione, come nell'esempio Apple).
- **B8, scollegamento:** l'app va in pausa e dopo pochi secondi l'audio riparte (sull'altoparlante). **B9, ricollegamento:** l'audio riparte da solo. Da analizzare nei log: chi mette in pausa (interruzione, cambio di uscita, comando remoto) e chi fa ripartire (watchdog, comando play di CarPlay).
- **Nuovo rischio (guida Apple, "good audio citizen"):** attivare la sessione audio ferma subito la radio FM dell'auto; va attivata solo quando si sta per suonare. Oggi `AppDelegate.FinishedLaunching` chiama `SetActive(true)` a ogni avvio, anche quando l'app viene aperta da CarPlay senza riproduzione. Da spostare all'avvio effettivo della riproduzione.

### 22 ottobre 2026 — analisi B8/B9 (`radioe45-b8b9.logarchive`) e correzioni
- **B8, causa misurata (13:03:47 e 13:21:27, motore nuovo; uguale alle 12:27:43 con AVPlayer):** cavo staccato → `route change OldDeviceUnavailable` → `Speaker`; il sistema invia un'interruzione `Began`; il player passa a `Paused`; 1,4-4,2 s dopo `Watchdog: state=Paused … reconnecting` riapre lo stream sull'altoparlante. **B9:** l'audio suonava già sull'altoparlante e al ricollegamento (`NewDeviceAvailable` → `CarAudio`) passa all'auto; nessun comando "play" ricevuto.
- **Correzione:** `AudioService` (iOS) su `OldDeviceUnavailable` chiama `PauseAsync` (pausa vera: il watchdog non riapre). Vale anche per le cuffie.
- **Falsi "Buffering" del motore nuovo (misurati, 13:00:57 e 13:01:38-41):** con un flusso in tempo reale la coda si svuota e si riempie di continuo. Ora `Buffering` solo se nessun dato è stato accodato negli ultimi 2 s (una sola verifica alla volta). Simulatore: 35 s senza falsi `Buffering`.
- **Sessione audio:** `AppDelegate` imposta solo la categoria; `IosStreamPlayer.Open` attiva la sessione (guida Apple, radio FM dell'auto). Simulatore: avvio regolare (398 ms), nessun errore di attivazione.
- `CHANGELOG.md`: riga `Fixed` per la pausa allo scollegamento di cuffie o CarPlay.
- Build installata sul device: da ripetere B8, B9, A1 (avvio), A7 (telefonata), A8 (cuffie).
- **Esito sul device (utente):** B8 superata (resta in pausa). B9: ricollegando il cavo l'audio riparte da solo; da capire nei log se per un comando "play" del sistema (accettabile) o per un nostro riavvio. B5: confermato di lasciarlo così per ora.
- **B9 chiarita (`radioe45-b9.logarchive`):** 13:41:41.508 cavo staccato → "Audio output device removed, pausing" → pausa; 13:41:53.050 `mediaremoted` "Sending command … command = Play" a RadioE45 da `com.apple.cmsession.carplay`; 13:41:53.062 `Resume station streaming`. Uguale alle 13:39:44. La ripartenza al ricollegamento è un comando di sistema (iOS riprende l'ultima app audio quando l'auto si collega), non un riavvio dell'app: comportamento atteso, lasciato così. **B8 e B9 superate.**

### 22 ottobre 2026 — Fase G
- `parity/features.yaml`: `shell.carplay` → `maui: done` (note: solo iOS, verificato con CarPlay Simulator e iPhone, auto reale non provata). `ParityCheck`: nessun disallineamento.
- `CHANGELOG.md` `[Unreleased]` `New`: Apple CarPlay.
- `README.md`: voce funzioni, sezione "Apple CarPlay", riga nella tabella dei documenti.
- Nuovi documenti: [`README.md`](README.md) (tecnico) e [`app-store-carplay.md`](app-store-carplay.md) (controlli, TestFlight, What's New, descrizione, note per App Review).
- `implementation-plan.md` segnato come storico; `STORE-DISTRIBUTION.md` con avviso: gli script `build-store-*.sh` non esistono più, comando `dotnet publish` dalla documentazione Microsoft (non ancora eseguito).
- `CarPlaySceneDelegate.cs`: commento obsoleto sullo script di iniezione corretto.
- **Restano all'utente:** `/release-prep`, build App Store e TestFlight (prova C), prove B6, B7, B10, B11, invio ad App Review.
