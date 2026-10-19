# Controllo aggiornamenti all'avvio

Stato: **bozza di analisi**, nessuna implementazione.

Obiettivo: all'avvio l'app verifica se nello store è disponibile una versione più recente e lo segnala all'utente.

## Architettura proposta

- Interfaccia `IAppUpdateService` in `RadioE45.Core/Services/Platform/`, con `CheckAsync()` che ritorna la versione disponibile e l'azione da proporre (URL dello store o flusso di aggiornamento in-app).
- Modello `AppUpdateInfo` nel Core.
- Logica nel Core, in un ViewModel dedicato: confronto versioni, quando mostrare il messaggio, testi (in `.resx`), gestione errori.
- Nessun sync-over-async (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`): il check è asincrono e non blocca l'avvio.
- Implementazioni:
  - MAUI: `RadioE45/Services/Platform/Maui*.cs`, una per store o per piattaforma.
  - Uno: implementazioni per desktop Linux e web in `RadioE45.Uno`.
- Ogni modifica UI aggiorna `parity/features.yaml` (regola 2 di `CLAUDE.md`).

## Meccanismi per piattaforma

### Google Play (Android)

- API ufficiale In-App Updates (libreria `com.google.android.play:app-update`, classe `AppUpdateManager`).
- Permette flusso *flexible* (download in background) o *immediate* (blocca l'app fino all'aggiornamento).
- Non esiste binding MAUI pronto: serve interop Java/Kotlin nella piattaforma MAUI.
- Funziona solo per app installate da Google Play.

### Apple App Store (iOS, Mac Catalyst)

- Nessuna API ufficiale per il controllo degli aggiornamenti.
- Si usa la iTunes Lookup API: `https://itunes.apple.com/lookup?bundleId=<bundle>&country=<cc>`.
  - Campo `results[0].version`: confrontare con la versione corrente.
  - Campo `trackViewUrl`: apre la pagina dell'app nello store.
- Implementabile con `HttpClient` semplice, quindi anche nel Core (senza `#if`).
- Non si può forzare l'aggiornamento, solo proporlo.

### Microsoft Store (Windows)

- API `Windows.Services.Store.StoreContext`:
  - `GetAppAndOptionalStorePackageUpdatesAsync()` per il controllo.
  - `RequestDownloadAndInstallStorePackageUpdatesAsync()` per installare.
- Vale solo per app MSIX pacchettizzata (quella distribuita su Store).
- Da app desktop serve inizializzare `StoreContext` con la window handle (`WinRT.Interop.InitializeWithWindow`).

### Uno desktop Linux (.deb)

- Nessuno store.
- Check sulla GitHub Releases API: `https://api.github.com/repos/delfuria/RadioE45/releases/latest`, confronto tra tag e versione corrente.
- Aggiornamento manuale: link alla release.

### Uno web (WASM)

- Nessun aggiornamento esplicito: la nuova versione arriva al reload.
- Opzionale: `version.json` pubblicato con la build su GitHub Pages, confrontato con la versione compilata.

## Limiti e decisioni aperte

- La Lookup Apple e la GitHub Releases riflettono la versione pubblicata, non quella in review.
- Errori di rete: check silenzioso, nessun dialog di errore all'avvio.
- Android e Windows possono aggiornare dentro l'app. Apple e Linux portano solo allo store o alla pagina di download.
- Da decidere: il messaggio è un dialog non bloccante all'avvio (una volta per versione) oppure un banner discreto nelle impostazioni.

## Ordine di lavoro proposto

1. Core: interfaccia, `AppUpdateInfo`, logica di confronto e ViewModel, con test xUnit sulla logica.
2. Implementazioni MAUI: Apple Lookup (copre iOS e Mac Catalyst), poi Windows Store, poi Google Play.
3. Uno desktop via GitHub Releases.
4. Web: opzionale o da saltare.
