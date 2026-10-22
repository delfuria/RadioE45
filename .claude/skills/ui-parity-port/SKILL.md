---
name: ui-parity-port
description: Porta una modifica UI dall'app MAUI (RadioE45/Views, AppShell, ViewModel del Core) all'app Uno (RadioE45.Uno, desktop Linux e web) e aggiorna il manifest di parità. Usare dopo ogni modifica a RadioE45/Views/**, RadioE45/AppShell.xaml o RadioE45.Core/ViewModels/**, quando l'utente chiede di "portare su Uno", "allineare desktop/web", "chiudere il debito di parità" o di eseguire una sessione S8.x del roadmap. Input: un feature id di parity/features.yaml oppure un range git.
---

# Port di una modifica UI MAUI → Uno

Riferimenti: `CLAUDE.md`, `docs/LINUX-UNO-ROADMAP.md` (§6, §8), `docs/parity/MAUI-TO-UNO.md`.

## 0. Prerequisiti

- Se `RadioE45.Uno/` non esiste ancora (prima della sessione S6), il port non è possibile: aggiornare solo `parity/features.yaml` (desktop/web = `todo`), verificare `ParityCheck` = 1 e proporre il commit con `Parity-Debt:`. Fermarsi qui.
- Input: un **feature id** (es. `onair.next-track`) oppure un **range git** (es. `main..HEAD`). Se manca, chiedere all'utente quale.

## 1. Capire la modifica

1. Range git: `git diff <range> -- RadioE45/Views RadioE45/AppShell.xaml RadioE45.Core/ViewModels RadioE45/Resources/Styles parity/`.
   Feature id: leggere la voce in `parity/features.yaml` e la pagina MAUI indicata da `page` (percorso in `parity/pages.yaml`).
2. Elencare: binding e comandi toccati, chiavi `{loc:Translate}` nuove, comportamenti nuovi del ViewModel.
3. Se una logica è finita nel code-behind MAUI, spostarla prima nel ViewModel del Core (regola 3 di `CLAUDE.md`), poi continuare.

## 2. Aggiornare il manifest

- Feature nuova: aggiungere la voce in `parity/features.yaml` (`id` = `pagina.sezione[.dettaglio]`, `since` = versione corrente in `version.json`, `maui: done`).
- Se è cambiata l'API pubblica di un ViewModel: `dotnet run --project tools/ParityCheck -- --update-surface`.

## 3. Port su Uno

1. Aprire la pagina Uno da `parity/pages.yaml` (campo `uno`). Se non esiste ancora, crearla seguendo la struttura delle pagine Uno esistenti.
2. Convertire con `docs/parity/MAUI-TO-UNO.md`, mantenendo stili e risorse condivise di `RadioE45.Uno/Styles/`.
3. Binding sempre `{x:Bind ViewModel.X, Mode=OneWay}` (o `TwoWay` per gli input); stringhe con `{loc:Translate Key}`.
4. Differenze desktop/web solo nei servizi (`Services/Desktop`, `Services/Browser`), mai `#if` nel XAML o nei ViewModel.

## 4. Verifica

1. Diagnostica LSP sui file toccati.
2. `dotnet build RadioE45.Uno -f net10.0-desktop` e `dotnet build RadioE45.Uno -f net10.0-browserwasm`.
3. Se è cambiato il Core: `dotnet build RadioE45.slnx` e `dotnet test --project RadioE45.Core.Tests/RadioE45.Core.Tests.csproj`.
4. `dotnet run --project tools/ParityCheck`: nessun errore (exit 0 o 1). Un membro usato solo in MAUI per un motivo di piattaforma va in `ignore` della pagina con `reason`; non usare `ignore` per rimandare un port (per quello c'è `todo`).
5. Opzionale, se l'utente vuole la verifica visiva: avviare `dotnet run --project RadioE45.Uno -f net10.0-desktop` (macOS) e la versione web in Chrome (claude-in-chrome), screenshot affiancati alla pagina MAUI (Mac Catalyst). L'audio web va provato a mano: i clic automatici non valgono come gesto utente.

## 5. Chiusura

1. In `parity/features.yaml` portare `desktop` e `web` a `done`, oppure `limited` con `notes` che spiegano il limite (es. CORS sul web). Per una feature nuova valorizzare anche `tv` (`todo`, oppure `n/a` con `tvNotes` se non ha senso su TV): il port TV si fa con la skill `tv-parity-port`.
2. `ParityCheck` di nuovo: deve restituire 0 o 1.
3. Proporre il messaggio di commit **come testo** (in inglese, mai eseguire `git commit`), con il trailer:
   - `Parity: maui, desktop, web` se la feature è allineata ovunque;
   - `Parity-Debt: <feature-id> (desktop, web)` per ciò che resta `todo`.
