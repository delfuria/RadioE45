---
name: tv-parity-port
description: Porta una funzione dell'app MAUI (RadioE45/Views, ViewModel del Core) sulla vista Android TV / Google TV (RadioE45/Views/Tv) e aggiorna il manifest di parità per la piattaforma tv. Usare quando l'utente chiede di "portare su TV", "fare la vista TV", "chiudere il debito tv", di eseguire una sessione T2-T6 di docs/ANDROID-TV-ROADMAP.md, o dopo una modifica UI MAUI che ricade nell'ambito TV (feature con tv todo in parity/features.yaml). Input: un feature id di parity/features.yaml, un nome di pagina TV, oppure un range git.
---

# Port di una funzione MAUI → vista Android TV

Riferimenti: `CLAUDE.md`, `docs/ANDROID-TV-ROADMAP.md` (§1 regole, §4 architettura, §5 parità, §6 sessioni), `docs/parity/MAUI-TO-TV.md` (conversione e regole di layout TV, scritto in T2).

La TV è la stessa app MAUI Android del telefono: le viste TV stanno in `RadioE45/Views/Tv/`, usano gli stessi ViewModel del Core e vengono scelte a runtime quando il dispositivo è una TV. Nessun nuovo progetto, nessun nuovo target.

## 0. Prerequisiti

- Input: un **feature id** (es. `onair.playback-controls`), il nome di una **pagina TV** (es. `TvNowPlayingPage`) oppure un **range git**. Se manca, chiedere quale.
- Leggere la sessione corrente in `docs/ANDROID-TV-ROADMAP.md` §6: eseguire solo ciò che appartiene alla sessione in corso.
- Se la funzione è `tv: n/a` in `parity/features.yaml`, **non portarla**: la decisione è già presa (es. impostazioni, menu laterale) ed è nel campo `tvNotes`. Cambiarla solo su richiesta dell'utente, aggiornando prima il roadmap §8.
- Emulatore: `emulator-5554`, comandi in `docs/ANDROID-TV-ROADMAP.md` §2.1. Verificare con `adb devices` che sia attivo.

## 1. Capire la funzione

1. Feature id: leggere la voce in `parity/features.yaml` (`tv`, `tvNotes`, `page`, `viewmodel`) e la pagina in `parity/pages.yaml` (campi `maui`, `tv`, `ignoreTv`).
   Range git: `git diff <range> -- RadioE45/Views RadioE45/AppShell.xaml RadioE45.Core/ViewModels parity/`.
2. Aprire la pagina MAUI del telefono e elencare: binding, comandi, chiavi `{loc:Translate}`.
3. Se la logica è finita nel code-behind, spostarla prima nel ViewModel del Core (regola 3). Nella vista TV solo layout, binding e focus.

## 2. Costruire la vista TV

1. Pagina in `RadioE45/Views/Tv/` (nome come nel campo `tv` di `pages.yaml`), `x:DataType` e binding `{Binding X}` allo stesso ViewModel del telefono (`BindingContext = viewModel` nel code-behind, come le pagine esistenti).
2. Regole di layout (§4 del roadmap): pagina logica 960x540 dp, margini di sicurezza circa 48 dp ai lati e 27 dp sopra e sotto, testo e bersagli grandi.
3. Focus: ogni controllo azionabile è focusabile con stato `Focused` ben visibile; il focus iniziale cade sul controllo principale; Indietro risale di un livello.
4. Niente `#if` e niente logica nel code-behind oltre a cose puramente visive (focus iniziale incluso). Differenze di piattaforma solo nei servizi di `Services/Platform/`.
5. Stringhe solo nei `.resx` del Core (it neutro, en, pl).
6. Membri del telefono che su TV non servono (swipe, trascinamento, pull to refresh): `ignoreTv` in `pages.yaml` con motivo. Non usare `ignoreTv` per rimandare un lavoro: per quello c'è `tv: todo`.

## 3. Aggiornare il manifest

- `parity/features.yaml`: `status.tv` → `done` (o `limited` con `tvNotes`); feature nuove solo-TV: `maui`, `desktop`, `web` = `n/a` con `notes`.
- `parity/pages.yaml`: campo `tv` con il percorso della pagina (o `n/a` + `tvReason`).
- Se è cambiata l'API pubblica di un ViewModel: `dotnet run --project tools/ParityCheck -- --update-surface`.

## 4. Verifica

1. Diagnostica LSP sui file toccati.
2. `dotnet build RadioE45 -f net10.0-android` e, se è cambiato il Core, `dotnet build RadioE45.slnx` e `dotnet test --project RadioE45.Core.Tests/RadioE45.Core.Tests.csproj`.
3. Installare sull'emulatore: `dotnet build RadioE45 -f net10.0-android -t:Install -p:AdbTarget="-s emulator-5554"`, avviare come il launcher TV (§2.1 del roadmap).
4. **Mappa del focus** (§2.3 del roadmap): con `tools/tv-focus-probe.sh` (dalla sessione T2) o, finché non esiste, con `adb shell input keyevent` e `uiautomator dump` leggendo `focused="true"`. Ogni controllo azionabile della pagina deve essere raggiungibile col D-pad. Riportare l'esito.
5. Screenshot dell'emulatore con `adb exec-out screencap -p`, solo dello schermo del dispositivo. Non premere `DPAD_CENTER` alla cieca nella home TV.
6. `dotnet run --project tools/ParityCheck -- --include-tv`: nessun errore (exit 0 o 1).

## 5. Chiusura

1. Aggiornare le checkbox della sessione in `docs/ANDROID-TV-ROADMAP.md` §6 e il registro §9.
2. `CHANGELOG.md`: se la modifica è visibile all'utente finale della TV, riga in inglese sotto `## [Unreleased]` (regola 11).
3. Proporre il messaggio di commit **come testo** (in inglese, mai eseguire `git commit`), con il trailer:
   - `Parity: maui, desktop, web, tv` se la funzione è allineata ovunque rilevante;
   - `Parity-Debt: <feature-id> (tv)` per ciò che resta `todo` su TV.
