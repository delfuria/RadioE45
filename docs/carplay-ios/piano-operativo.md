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
- [ ] Inviare la richiesta entitlement CarPlay Audio (Fase 0) e registrare la data.
- [ ] Dopo approvazione: capability sull'App ID; rigenerare profili Development e App Store.
- [ ] Procurarsi un modo per testare su device (auto o accessorio, oppure secondo dispositivo con CarPlay for iPhone).
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
  - **Da verificare ancora:** background/foreground con radio in riproduzione, distruzione e ricreazione della scena, audio in background e lock screen, interruzioni audio, rotazione, iPad.
- **Correzione a §3.1 e §3.2:** MAUI **non** rimuove `UIApplicationSceneManifest` dal merge dei plist; il manifest del sorgente arriva nel bundle. Un bundle senza manifest visto all'inizio dipendeva da `AppManifest.plist` intermedio non rigenerato (build incrementale). Quindi la Fase B probabilmente non richiede script post-build né re-firma: basta aggiungere la scena CarPlay al plist sorgente e l'entitlement. Da confermare con lo spike.
