# Apple CarPlay — Piano di Riattivazione ed Estensione

> Data: 31 luglio 2026
> Target: `net10.0-ios`
> Stato: scaffolding già scritto a metà giugno 2026, disabilitato in attesa dell'entitlement Apple

---

## 1. Obiettivo

Portare il supporto CarPlay di RadioE45 da "codice pronto ma spento" a feature attiva e testata, allineandolo al livello di qualità già raggiunto con [Android Auto (Media3)](../android-auto-media3/README.md): lista stazioni nel veicolo, riproduzione, metadata e controlli di trasporto, con relativa documentazione utente/README.

Non tocca `IAudioService`, `IPlatformNowPlayingService`, `AudioService`, `IosNowPlayingService` né i ViewModel — stesso vincolo del draft originale, confermato ancora valido.

---

## 2. Stato attuale (verificato sul codice, non sul draft)

A differenza di Android Auto, per CarPlay **non si parte da zero**. Il lavoro di metà giugno 2026 ha prodotto:

| File | Stato | Note |
|---|---|---|
| `Platforms/iOS/CarPlaySceneDelegate.cs` | ✅ Presente, completo, compila | Root template = `CPListTemplate` (lista stazioni), non `CPNowPlayingTemplate` come nel draft originale. Gestisce `DidConnect`/`DidDisconnect`, DI via scope, tap su stazione → `PlayAsync` + push `CPNowPlayingTemplate.SharedTemplate` |
| `Platforms/iOS/AppDelegate.cs` | ✅ Presente | `GetConfiguration` instrada le scene role `CarTemplateApplication` verso `CarPlaySceneDelegate`; `SetupRemoteCommandCenter()` già configura `MPRemoteCommandCenter` (play/pause/toggle, seek disabilitato — corretto per streaming live) |
| `Platforms/iOS/SceneDelegate.cs` | ✅ Presente | Thin subclass di `MauiUISceneDelegate` per la finestra principale |
| `Platforms/iOS/Info.plist` | ⚠️ Bloccato commentato | Blocco `UIApplicationSceneManifest` con entrambe le scene (main + CarPlay) scritto e corretto, ma dentro un commento XML con istruzioni di ripristino |
| `Platforms/iOS/Entitlements.plist` | ⚠️ Vuoto | `com.apple.developer.carplay-audio` rimosso: causava fallimento del code signing su device fisico senza provisioning profile aggiornato |
| `RadioE45.csproj` | ⚠️ Bloccato commentato | `CustomEntitlements` per CarPlay + due target MSBuild post-build (`InjectCarPlaySceneManifest`, `ResignAppBundle`) tutti commentati con istruzioni di ripristino dettagliate |
| `scripts/inject-carplay-manifest.sh` | ❌ **Mancante** | Esisteva nel commit iniziale (14 giugno), **cancellato il 16 giugno** (commit `159dc66`, "Update plist file for App publishing"). Referenziato dal csproj ma il file non esiste più sul disco |
| `scripts/resign-app-bundle.sh` | ❌ **Mancante** | Stessa sorte del precedente |
| Entitlement Apple `com.apple.developer.carplay-audio` | ❌ **Mai richiesto** | Confermato dall'utente — nessuna richiesta ancora inviata a developer.apple.com |

**Perché era stato disabilitato**: MAUI rimuove `UIApplicationSceneManifest` durante il merge dei plist in fase di build; serviva uno script post-build per re-iniettarlo, più un re-sign ad-hoc per il simulatore. Senza entitlement approvato, l'`ItemGroup CustomEntitlements` avrebbe fatto fallire il code signing su device fisico/distribuzione. È stata una disattivazione intenzionale e ben documentata (commenti dettagliati in tutti e 3 i file), non un abbandono a metà.

---

## 3. Gap da colmare

1. **Entitlement Apple** — mai richiesto, è il vero blocco. Tutto il resto può procedere in parallelo, ma senza approvazione l'app non funzionerà su device fisico né in TestFlight/App Store (solo simulatore Xcode con firma ad-hoc).
2. **Script mancanti** — `inject-carplay-manifest.sh` e `resign-app-bundle.sh` vanno riscritti da zero (il codice originale non è recuperabile da git, cancellato senza essere mai stato nella working tree di un branch ancora vivo — verificato che esistano solo nella storia).
3. **Riattivazione scaffolding** — decommentare 3 blocchi coordinati (csproj, Info.plist, Entitlements.plist), tutti già scritti correttamente.
4. **Divergenza dal draft**: il draft (`docs/carplay/android-auto-carplay-implementation.md`) descrive `CPNowPlayingTemplate` come root con pulsante lista; il codice reale ha `CPListTemplate` come root. Questa è una scelta implementativa successiva, non un bug — da confermare se mantenerla o allinearla al draft (vedi Fase 3).
5. **Test mai eseguito end-to-end** — nessuna osservazione conferma un run riuscito su CarPlay Simulator con audio realmente in riproduzione.

---

## 4. Piano a fasi

### Fase 0 — Richiesta entitlement Apple (bloccante, azione manuale, avviare subito)

Poiché l'approvazione richiede tempi Apple non controllabili, va avviata **per prima**, in parallelo alle fasi successive che non ne dipendono (1 e 2).

1. Vai su https://developer.apple.com/contact/carplay/
2. Compila "CarPlay Entitlement Request", categoria **Audio App**
3. Serve probabilmente: nome app, bundle ID (`com.radioe45.app`), descrizione funzionalità (streaming radio live, non browsing di libreria), eventualmente link/video demo
4. Attendi approvazione (tempi variabili, storicamente giorni-settimane)
5. Dopo approvazione: rigenera i provisioning profile (Development e Distribution "Radio E45 AppStore") su developer.apple.com includendo la capability CarPlay Audio, scaricali, installali

⚠️ Le Fasi 4 (test su device) e 5 (distribuzione) sono bloccate da questo step. Le Fasi 1-3 no.

---

### Fase 1 — Ricreare gli script MSBuild mancanti

**`scripts/inject-carplay-manifest.sh`**
- Input: `$(OutputPath)` (cartella bundle .app) e `$(MSBuildProjectName)`
- Compito: localizzare `Info.plist` dentro il bundle compilato (`<OutputPath>/<AppName>.app/Info.plist`), inserire (via `PlistBuddy` o `plutil`) la chiave `UIApplicationSceneManifest` con il contenuto oggi commentato in `Platforms/iOS/Info.plist` (fonte di verità)
- Deve essere idempotente (rieseguibile senza duplicare chiavi) e fallire in modo esplicito se `Info.plist` non si trova

**`scripts/resign-app-bundle.sh`**
- Input: stessi parametri
- Compito: re-firma ad-hoc il bundle (`codesign --force --sign - --deep`) dopo l'iniezione del manifest, solo per il path simulatore
- **Non deve girare in build Release/device**: la condizione `AfterTargets="InjectCarPlaySceneManifest"` nel csproj già scoperta va integrata con un controllo esplicito (es. `Condition` su `$(Configuration)` == Debug, o su un flag tipo `_IsSimulatorBuild`), perché ri-firmare dopo il codesign di un certificato Distribution reale invalida la firma — rischio già annotato nel commento del csproj ma non ancora implementato come guard automatico

Entrambi vanno resi eseguibili (`chmod +x`) e committati.

---

### Fase 2 — Riattivazione scaffolding esistente

Una volta pronti gli script (Fase 1), indipendentemente dall'esito della Fase 0 (funziona già su simulatore con firma ad-hoc):

1. Decommenta `<ItemGroup Condition="... == 'ios'"> <CustomEntitlements .../> </ItemGroup>` in `RadioE45.csproj`
2. Decommenta i target `InjectCarPlaySceneManifest` e `ResignAppBundle` in `RadioE45.csproj`
3. Decommenta il blocco `UIApplicationSceneManifest` in `Platforms/iOS/Info.plist`
4. Aggiungi `<key>com.apple.developer.carplay-audio</key><true/>` in `Platforms/iOS/Entitlements.plist`
5. Build `net10.0-ios` Debug (simulatore) e verifica: 0 errori, script post-build eseguiti (log MSBuild), bundle firmato ad-hoc

Nota: finché l'entitlement non è approvato (Fase 0), la build **Release/device fisico** fallirà in code signing — atteso, non è una regressione.

---

### Fase 3 — Estensioni funzionali (decisione utente: procedere oltre la sola riattivazione)

Proposte concrete, in ordine di valore/sforzo:

1. **Artwork nella lista stazioni** — `CPListItem` supporta un'immagine (`UIImage`) accanto a titolo/sottotitolo. Oggi `BuildStationListItem` non la imposta. Caricare da `station.LogoUrl` riusando `RemoteArtworkLoader` già esistente (consumato oggi solo da `IosNowPlayingService`, vedi nota architetturale). Migliora la UX in auto in modo visibile a basso rischio.
2. **Gestione stato "caricamento"** — oggi se `_catalog.Stations` è vuoto al momento di `DidConnect`, mostra un placeholder statico "Caricamento stazioni..." **senza refresh successivo**: se `LoadAsync()` completa dopo la connessione CarPlay, la lista resta bloccata sul placeholder finché l'utente non riconnette. Va sottoscritto un evento di aggiornamento catalogo (o pollato) per rigenerare il `CPListTemplate` quando le stazioni arrivano.
3. **Allineamento al pattern draft (opzionale, da confermare)** — il draft originale prevede `CPNowPlayingTemplate` come root con pulsante lista in `trailingNavigationBarButtons`; il codice attuale fa l'opposto (lista come root, Now Playing in push dopo il tap). Il pattern attuale è valido e comune per app radio-first; consiglio di **mantenerlo com'è** salvo preferenza esplicita per l'altro flusso — cambiarlo è puro re-lavoro senza guadagno funzionale.
4. **Error handling in `PlayStationAsync`** — già presente un `try/catch` con log; valutare feedback visivo nella UI CarPlay (es. `CPAlertTemplate`) se `PlayAsync` fallisce, invece del solo log silenzioso.
5. **Indicatore stazione in riproduzione nella lista** — `isCurrent` è già calcolato in `BuildStationListItem` e usato per il sottotitolo; si può rinforzare con un accessory/icona dedicata se l'API `CPListItem` lo consente nel binding .NET 10 usato.

Item 1 e 2 sono i consigliati come "must" per una prima release solida; 3 è una non-azione consigliata; 4 e 5 sono rifiniture successive.

---

### Fase 4 — Test

**Simulatore Xcode (non richiede entitlement approvato):**
1. Avvia RadioE45 su simulatore iOS da Xcode/Rider
2. `I/O → External Displays → CarPlay` per aprire la finestra CarPlay
3. Verifica: lista stazioni visibile, tap avvia riproduzione, push a `CPNowPlayingTemplate`, metadata (titolo/artista/artwork) aggiornati da `MPNowPlayingInfoCenter`, transport controls (play/pause) funzionanti
4. Verifica nuovamente dopo le estensioni di Fase 3 (artwork, refresh catalogo)

**Device fisico (richiede Fase 0 completata + provisioning aggiornato):**
1. Build Release firmata con provisioning che include CarPlay Audio
2. Test con veicolo reale o accessorio CarPlay wireless/cablato, oppure "CarPlay for iPhone" su un secondo dispositivo se disponibile
3. Verifica lifecycle multi-scene: la finestra principale dell'app continua a funzionare normalmente quando CarPlay è connesso e dopo la disconnessione (rischio già segnalato nel draft originale, mai verificato su device reale)

---

### Fase 5 — Distribuzione e documentazione

1. Aggiornare `docs/STORE-DISTRIBUTION.md` / `guida-appstore.html` se il processo di submission cambia con la capability CarPlay
2. Aggiungere sezione "Apple CarPlay" al `README.md`, stesso stile della sezione "Android Auto" già presente (bilingue se si segue la convenzione del progetto)
3. Considerare se creare `docs/carplay-ios/README.md` con la documentazione tecnica finale (architettura, file coinvolti), sul modello di `docs/android-auto-media3/README.md` — questo piano diventa storico una volta eseguito

---

## 5. Rischi e note tecniche

- **Guard mancante negli script di re-sign**: se `ResignAppBundle` gira anche su build Distribution, invalida la firma reale. Va implementato un controllo esplicito nello script o nella `Condition` del target MSBuild (Fase 1) — rischio già noto ma non ancora mitigato in modo automatico.
- **Tempi Apple non stimabili**: la Fase 0 è l'unico vero collo di bottiglia per test su device e distribuzione; tutto il resto è eseguibile subito.
- **Lifecycle multi-scene mai verificato su device fisico**: `UIApplicationSupportsMultipleScenes = true` cambia il lifecycle iOS anche per la finestra principale, non solo per CarPlay. Testare a fondo la navigazione app normale dopo la riattivazione, non solo il flusso CarPlay.
- **RemoteArtworkLoader condiviso**: se si implementa l'artwork nella lista stazioni (Fase 3.1), verificare che il caricamento concorrente (lock screen + CarPlay list) non generi race condition sullo stesso loader.

---

## 6. Checklist azioni manuali (utente)

- [ ] Inviare richiesta entitlement CarPlay su https://developer.apple.com/contact/carplay/ (Fase 0)
- [ ] Dopo approvazione: rigenerare provisioning profile Development + "Radio E45 AppStore" con capability CarPlay Audio
- [ ] Procurarsi un modo di testare su device reale (auto compatibile, accessorio CarPlay, o "CarPlay for iPhone")
