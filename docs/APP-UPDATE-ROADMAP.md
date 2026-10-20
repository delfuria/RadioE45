# RadioE45 — Roadmap controllo aggiornamenti

> Stato: **piano pronto, nessuna implementazione.** Prossima sessione: **U0** (§9). Ultimo aggiornamento: 2026-10-20 (app v0.46.6).
> Analisi di partenza (meccanismi per piattaforma): [`APP-UPDATE-CHECK.md`](APP-UPDATE-CHECK.md). Questo file è il piano operativo e prevale in caso di differenze.
> Branch di lavoro: `app-update`. Un branch per sessione solo se la sessione tocca tanti file; altrimenti si resta su `app-update`.

## 0. Come usare questo documento

A inizio sessione leggere §1 (decisioni), §2 (stato dei canali) e la sessione indicata in §9. Alla fine:
1. spuntare le checkbox della sessione e aggiungere la riga nel registro (§10);
2. `dotnet build RadioE45.slnx`, `dotnet test --project RadioE45.Core.Tests/RadioE45.Core.Tests.csproj`, `dotnet run --project tools/ParityCheck` (0 o 1);
3. proporre il messaggio di commit **come testo**, in inglese, con il trailer `Parity:` / `Parity-Debt:` (mai eseguire `git commit`).

Regole di progetto che pesano su questa feature (`CLAUDE.md`): logica e testi nel Core, nessun `#if` né tipi MAUI/Uno nel Core, nessun sync-over-async, stringhe solo nei `.resx` (it neutro, en, pl), MAUI first poi porting Uno (skill `ui-parity-port`), `parity/features.yaml` aggiornato.

## 1. Decisioni

### Confermate dall'utente (2026-10-20)

| # | Decisione |
|---|---|
| D1 | **Windows ha due canali**: Microsoft Store (MSIX) e installer Inno Setup `.exe` (`RadioE45_Setup_x64/arm64.exe` allegati alla release GitHub). Servono due sorgenti e il rilevamento del canale a runtime. |
| D2 | **UX**: dialog non bloccante all'avvio, dopo i Termini d'uso, **solo al primo avvio di ogni giorno** (giorno di calendario locale) se è disponibile una versione più recente ("Aggiorna" / "Più tardi"; "Più tardi" non ha memoria, il giorno dopo l'avviso riappare finché l'app non è aggiornata). In più, controllo manuale nelle Impostazioni. |
| D3 | **Android rimandato**: il primo rilascio non controlla né aggiorna su Android (nessun interop Play). Android registra `NullUpdateSource`; il debito va dichiarato in `parity/features.yaml`. |
| D4 | **Struttura**: sessioni indipendenti stile S8.x (§9), ognuna con checklist, file toccati, criteri di uscita e commit. |

| D8 | **L'app iOS è disponibile in tutti i paesi** (2026-10-20) | Lookup con il paese del dispositivo (`RegionInfo.CurrentRegion`), fallback `it` se il paese è vuoto o non valido. Nessun giro su più paesi |
| D9 | **Mac Catalyst non è pubblicato** sul Mac App Store (2026-10-20) | Mac usa `NullUpdateSource`, nessun avviso. Quando verrà pubblicato: Lookup Apple con l'entity Mac, dopo aver verificato la risposta reale |
| D5 | Frequenza | Check e avviso **una volta al giorno**, al primo avvio del giorno. Il controllo manuale ignora il limite giornaliero |
| D6 | "Più tardi" | Nessuna memoria per versione: se l'update resta disponibile, riappare al primo avvio del giorno dopo |
| D7 | Confronto versioni | **Solo Major.Minor** (es. `0.46`). Le patch (`0.46.x`) non generano mai l'avviso, su tutti i canali |

### Proposte da confermare (non bloccanti fino alla sessione indicata)

| # | Questione | Proposta | Serve in |
|---|---|---|---|
| D10 | Aggiornamenti con release GitHub per APK/DMG/EXE | Android sideload e macOS `.dmg` potrebbero usare la stessa sorgente GitHub; fuori scope per ora | futuro |
| D11 | Web | Banner "nuova versione, ricarica" solo se serve (U7, opzionale) | U7 |

## 2. Canali di distribuzione e stato

| Canale | Piattaforma | Sorgente "ultima versione" | Azione proposta | Dove sta il codice |
|---|---|---|---|---|
| Apple App Store | iOS | iTunes Lookup (`bundleId=com.radioe45.app`) | Apri `trackViewUrl` | Core (HttpClient, senza `#if`) |
| Mac App Store | Mac Catalyst | Lookup con `entity=macSoftware` | Apri `trackViewUrl` | non pubblicato (D9): `NullUpdateSource` |
| Microsoft Store | Windows MSIX | `StoreContext.GetAppAndOptionalStorePackageUpdatesAsync` | Apri pagina Store (poi, opz., installa in-app) | MAUI Windows (`#if WINDOWS` nell'head) |
| Installer Inno | Windows non pacchettizzato | GitHub Releases `latest` | Apri pagina release | Core |
| `.deb` | Linux Uno desktop | GitHub Releases `latest` | Apri pagina release | Core |
| Browser | Uno WASM | niente (U7 opzionale: `version.json`) | Ricarica | Uno |
| Google Play | Android | **nessuna (D3)** | — | `NullUpdateSource` |

Verifiche fatte il 2026-10-20 con `curl`:
- **iOS**: `https://itunes.apple.com/lookup?bundleId=com.radioe45.app&country=it&entity=software` → `resultCount 1`, `version "0.46.6"`, `trackViewUrl "https://apps.apple.com/it/app/radio-e45/id6780274998?uo=4"`, `kind "software"`, feature `iosUniversal`. La risposta è grande (screenshot, descrizione): leggere solo i campi necessari.
- **Mac**: la stessa chiamata con `entity=macSoftware` restituisce lo **stesso record iOS** (`kind "software"`, nessun `mac-software`). Quindi oggi **non risulta un'app Mac separata** nella Lookup (o è un'unica scheda "iosUniversal" senza build Mac). Conferma dell'utente: l'app Mac **non è ancora pubblicata**, quindi nessun codice Mac finché non lo è (D9).
- **GitHub**: `releases/latest` → `v0.46.6`, `prerelease:false`. Asset della release: `RadioE45.apk`, `RadioE45.dmg`, `radioe45_0.46.6_amd64.deb`, `radioe45_0.46.6_arm64.deb`, `RadioE45_Setup_x64.exe`, `RadioE45_Setup_arm64.exe`. La stessa release serve **tutte** le piattaforme: il tag non dice a quale canale si riferisce, quindi per il Linux/Inno vale il tag, ma la presenza dell'asset giusto va controllata (vedi U4).
- Il Microsoft Store dell'app: `https://apps.microsoft.com/detail/9pj0lkl6mnvq` (dal README).

## 3. Problemi noti (da risolvere nelle sessioni indicate)

1. **Versione dell'app non omogenea tra head.** Con D7 (solo Major.Minor) il problema si riduce: Uno riporta `0.46`, Apple e GitHub `0.46.6`, Windows Store `0.46.0`, e tutti coincidono a profondità 2. Resta da verificare a runtime cosa riporta `MauiAppEnvironment.VersionString` (`AppInfo.VersionString`) su iOS, Mac e Android (U0): `AppVersion` deve comunque troncare a Major.Minor. Conseguenza voluta: un rilascio solo-patch non avvisa nessuno.
2. **Tag e store non sono contemporanei.** GitHub Releases mostra la versione appena taggata, gli store con ritardo (review). Un utente Linux può vedere 0.46.7 mentre iOS è ancora 0.46.6: ogni sorgente decide solo per il proprio canale, mai confrontare tra canali.
3. **Formati tag eterogenei** (`v0.46.6`, `v0.46`, `v0.42`, `0.30`): parser unico nel Core (U0), con test.
4. **Il dialog non deve bloccare l'avvio.** Il check parte dopo `EnsureAcceptedAsync()` in `AppShell.OnNavigated` (MAUI) e `ShellPage.OnLoaded` (Uno), senza `await` sul percorso critico (fire-and-forget con gestione eccezioni e `CancellationToken`).
5. **WASM single-thread**: niente `.Result`/`.Wait()`; `HttpClient` va bene, ma chiamate verso GitHub/Apple dal browser richiedono CORS (non rilevante finché il web non controlla nulla).
6. **Rate limit GitHub** (60 richieste/ora per IP non autenticato): con throttle 24 h non è un problema.

## 4. Architettura

```
RadioE45.Core
  Services/Platform/IAppUpdateSource      (porta: "qual è l'ultima versione sul MIO canale?")
  Models/AppUpdateInfo                    (versione, azione, url, note, canale)
  Models/AppUpdateAction                  (None | OpenUrl | InAppUpdate)   // InAppUpdate riservato, non usato nel primo rilascio
  Models/AppVersion                       (parser/normalizzatore e confronto; record struct)
  Services/AppUpdateCoordinator           (limite giornaliero, decisione, azione)
  Services/Updates/AppleLookupUpdateSource
  Services/Updates/GitHubReleaseUpdateSource
  ViewModels  → l'avviso e il pulsante manuale vivono in SettingsViewModel (stato + comandi)
              + piccolo punto di ingresso allo startup (vedi sotto)
RadioE45 (MAUI)
  Services/Platform/MauiUpdateSourceSelector  (iOS → Apple; Mac Catalyst → Null; Windows → Store o GitHub; Android → Null)
  Services/Platform/MauiWindowsStoreUpdateSource   (#if WINDOWS nel progetto head)
RadioE45.Uno
  Services/Shared/UnoUpdateSourceSelector     (Linux/macOS/Windows desktop → GitHub; web → Null/U7)
```

Principi:
- **Coordinator nel Core**: confronto Major.Minor, limite giornaliero, scelta dell'azione. Le sorgenti restituiscono solo "ultima versione + URL".
- **Sorgenti HTTP nel Core** (Apple, GitHub): niente `#if`, testabili con `HttpMessageHandler` fittizio.
- **Solo ciò che usa API di piattaforma sta nell'head** (Windows `StoreContext`, rilevamento pacchettizzato: `AppInfo.PackagingModel` in MAUI).
- **Selettore di sorgente per head**, registrato in DI. Il Core non conosce i canali.
- **Dialog via `IDialogService`**, apertura URL via `IUrlLauncher`, persistenza via `ISettingsStore`: già esistenti.
- **Errori**: log tecnico, nessun dialog per errori di rete al check automatico; al check manuale, messaggio breve "impossibile controllare".

Chiavi `ISettingsStore`:
- `update.lastCheckDate` (data locale `yyyy-MM-dd`): scritta dopo un check riuscito (con o senza update). Se la data è oggi, il check automatico non parte. Un errore di rete non la scrive, così il prossimo avvio riprova. "Più tardi" non scrive altro.

Stringhe `.resx` (it neutro, en, pl), prefisso `Update_`: titolo, messaggio con segnaposto versione, "Aggiorna", "Più tardi", "Controlla aggiornamenti", "Aggiornato", "Controllo non riuscito", "Aggiornamento disponibile: {0}".

## 5. Test (xUnit v3, `RadioE45.Core.Tests`)

- `AppVersion`: parsing di `v0.46.6`, `0.46`, `0.30`, `0.46.6.0`, spazi, suffissi `-beta`, valori non validi; confronto solo Major.Minor: `0.46` = `0.46.6`, `0.46.6` < `0.47`, `0.9` < `0.46`.
- `AppUpdateCoordinator`: una volta al giorno (cambio di data con `TimeProvider` fittizio), "Più tardi" senza memoria (riappare il giorno dopo), manuale che ignora il limite, errore di rete che non consuma il giorno, sorgente `Null`.
- `AppleLookupUpdateSource`: risposta reale ridotta (fixture JSON), `resultCount 0`, errore HTTP, JSON malformato.
- `GitHubReleaseUpdateSource`: `latest` normale, prerelease scartata, tag non parsabile, asset mancante per il canale richiesto.
- Snapshot superficie ViewModel (`dotnet run --project tools/ParityCheck -- --update-surface`) dopo le modifiche a `SettingsViewModel`.

## 6. Parità (`parity/features.yaml`)

Nuove feature (da inserire in U5):

| id | titolo | maui | desktop | web |
|---|---|---|---|---|
| `update.startup-prompt` | Avviso aggiornamento all'avvio (primo avvio del giorno) | done | done | n/a (finché non c'è U7) |
| `settings.update.check` | Impostazioni: "Controlla aggiornamenti", stato e "Aggiorna ora" | done | done | n/a |
| `update.android` | Controllo aggiornamenti su Android | todo (debito D3) | n/a | n/a |
| `update.mac` | Controllo aggiornamenti su Mac Catalyst | todo (debito D9, app non pubblicata) | n/a | n/a |

Note obbligatorie per `n/a`/`limited`; per Android dichiarare nelle `notes` che il debito è D3. Aggiornare `pages.yaml` solo se cambiano le pagine.

## 7. Rischi

| Rischio | Impatto | Mitigazione |
|---|---|---|
| Confronto versioni sbagliato → prompt perenne | Alto | `AppVersion` unico, troncato a Major.Minor (U0) + test su fixture reali |
| Mac non pubblicato / Lookup senza record Mac | Basso | D9: `NullUpdateSource` su Mac; al lancio sul Mac App Store ripetere la prova Lookup e attivare Apple |
| Windows non pacchettizzato scambiato per Store | Medio | rilevamento esplicito del canale, fallback GitHub, mai aprire la pagina Store a un utente Inno |
| Release GitHub senza l'asset del canale (es. solo `.deb`) | Medio | la sorgente GitHub controlla che l'asset attesa esista prima di proporre l'update |
| Prompt fastidioso | Medio | limite giornaliero + dialog non bloccante dopo i Termini |
| Dialog durante l'avvio MAUI su iOS/Windows (AlertManager che scarta la richiesta) | Medio | stesso schema di `WaitUntilLoadedAsync` già usato per i Termini |
| Rate limit GitHub | Basso | throttle 24 h |

## 8. Fuori scope (primo rilascio)

- Aggiornamenti obbligatori / blocco dell'app.
- Android (Play In-App Updates o altro): D3.
- Download e installazione in-app (`RequestDownloadAndInstallStorePackageUpdatesAsync`, auto-update Linux): solo link.
- Aggiornamenti di `.dmg`/`.apk` da GitHub (D10).
- Notifiche push di nuove versioni.

## 9. Piano a sessioni

Criterio comune di uscita: `dotnet build RadioE45.slnx` verde, test Core verdi, `ParityCheck` 0 o 1, checkbox e registro (§10) aggiornati, commit proposto come testo. Dopo ogni modifica `.cs`: diagnostica LSP e `dotnet build`.

### U0 — Fondazione versioni (Core, nessuna UI)
- [ ] Leggere a runtime cosa riportano `AppInfo.VersionString` (iOS, Mac, Android, Windows) e `UnoAppEnvironment` (Linux/web); annotare i valori in §3.
- [ ] `AppVersion` nel Core (parse tollerante, confronto solo Major.Minor) + `AppVersionTests`.
- [ ] Nessuna nuova proprietà in `IAppEnvironment`: `AppVersion` tronca a Major.Minor. Verificare solo i valori a runtime.
- **File:** `RadioE45.Core/Models/AppVersion.cs`, `RadioE45.Core.Tests/Models/AppVersionTests.cs`.
- **Uscita:** test verdi; nessuna regressione UI.

### U1 — Coordinator, modello e sorgente nulla (Core)
- [ ] `AppUpdateInfo`, `AppUpdateAction`, `IAppUpdateSource`, `NullUpdateSource`.
- [ ] `AppUpdateCoordinator` (limite giornaliero, manuale, errori silenziosi, log).
- [ ] Registrazione DI nel Core (`DependencyInjection/`), sorgente di default `Null`.
- [ ] Test del coordinator con `TimeProvider` fittizio e `ISettingsStore` in memoria.
- **Uscita:** logica completa e testata senza alcuna sorgente reale.

### U2 — Avviso all'avvio e Impostazioni, MAUI (UI minima)
- [ ] Stringhe `Update_*` nei `.resx` (it/en/pl).
- [ ] `SettingsViewModel`: stato update, comandi "Controlla aggiornamenti" e "Aggiorna ora".
- [ ] Punto di avvio in `AppShell.OnNavigated`, dopo `EnsureAcceptedAsync()`, non bloccante.
- [ ] UI in `SettingsPage` MAUI.
- [ ] `parity/features.yaml` + `--update-surface`; Uno e web come `todo` (porting in U5).
- **Uscita:** su iOS (o Mac Catalyst, con la sorgente fittizia) con `NullUpdateSource` forzata a "versione fittizia più alta" (flag DEBUG) il dialog compare al primo avvio del giorno e non al secondo (provare cambiando `update.lastCheckDate`); il pulsante manuale funziona. Verifica visiva con screenshot della sola finestra dell'app (per ID finestra).

### U3 — Apple (solo iOS)
- [ ] Paese della Lookup: regione del dispositivo, fallback `it` (D8).
- [ ] `AppleLookupUpdateSource` (Core): `bundleId`, `country`, `entity`; lettura di `version` e `trackViewUrl`; fixture di test.
- [ ] `MauiUpdateSourceSelector`: iOS → Apple; Mac Catalyst → `NullUpdateSource` (D9).
- [ ] Prova reale con una versione locale più bassa (build di prova con versione forzata).
- **Uscita:** su iOS il prompt compare con l'URL corretto e "Aggiorna" apre lo Store.

### U4 — GitHub Releases (Linux Uno desktop e Windows non pacchettizzato)
- [ ] `GitHubReleaseUpdateSource` (Core): `repos/delfuria/RadioE45/releases/latest`, scarta prerelease/draft, controlla l'asset del canale (`.deb` per Linux, `RadioE45_Setup_*.exe` per Windows), URL = `html_url` della release.
- [ ] `UnoUpdateSourceSelector`: desktop → GitHub; web → `Null`.
- [ ] Windows MAUI non pacchettizzato (`AppInfo.PackagingModel == Unpackaged`) → GitHub (si completa in U6).
- [ ] Test con `HttpMessageHandler` fittizio (fixture `releases/latest`).
- [ ] Prova nella VM Linux (`ssh radioe45-vm`) con versione locale più bassa.
- **Uscita:** su Linux compare il prompt con il link alla release; `0.46.x` locale uguale all'ultimo tag non genera prompt.

### U5 — Porting su Uno (UI) e parità
- [ ] Punto di avvio in `ShellPage.OnLoaded`, dopo `EnsureAcceptedAsync()`.
- [ ] UI Impostazioni su Uno (skill `ui-parity-port`), `{x:Bind ViewModel.X}`.
- [ ] `parity/features.yaml`: `update.startup-prompt`, `settings.update.check`, `update.android` (§6); `ParityCheck` 0 o 1.
- [ ] Verifica visiva su desktop (stessa regola dello screenshot per finestra).
- **Uscita:** desktop allineato; web `n/a` con nota.

### U6 — Windows Store
- [ ] `MauiWindowsStoreUpdateSource` (`#if WINDOWS`): `StoreContext` con `InitializeWithWindow`, `GetAppAndOptionalStorePackageUpdatesAsync`, rilevamento pacchettizzato.
- [ ] Selettore: pacchettizzato → Store (D7); non pacchettizzato → GitHub (U4).
- [ ] Azione: aprire la pagina Store (`ms-windows-store://pdp/?productid=9pj0lkl6mnvq`).
- [ ] Prova su macchina Windows con build Store e con installer Inno.
- **Uscita:** entrambi i canali Windows danno il prompt giusto; il canale Inno non apre mai lo Store.

### U7 — Web (opzionale, solo se richiesto)
- [ ] `version.json` nella build Pages, confronto con la versione compilata, banner "Nuova versione disponibile — ricarica".
- [ ] Decisione D11 prima di iniziare.

### U8 — Hardening e rilascio
- [ ] Revisione stringhe it/en/pl (`docs/LOCALIZATION.md`).
- [ ] Log tecnici, nessun dialog su errore di rete al check automatico.
- [ ] Smoke test su iOS, Windows (Store e Inno), Linux, web.
- [ ] `dotnet build RadioE45.slnx`, test Core, `ParityCheck --strict` per il debito residuo (solo Android).
- [ ] Note di rilascio in inglese (changelog/store).

## 10. Registro sessioni

| Sessione | Data | Esito | Note |
|---|---|---|---|
| — | — | — | nessuna sessione ancora eseguita |

## 11. Definizione di done

- Check non bloccante su MAUI (iOS, Windows) e Uno desktop (Linux).
- Windows distingue Store e installer Inno.
- Un solo prompt al giorno, al primo avvio; pulsante manuale nelle Impostazioni.
- Android (D3) e Mac Catalyst (D9) dichiarati come debito in `parity/features.yaml` (`update.android`, `update.mac`).
- Stringhe it/en/pl, test del Core verdi, `ParityCheck` 0 o 1, build verde.
