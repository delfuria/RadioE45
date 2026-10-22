# RadioE45 — regole di progetto per Claude Code

App radio per AzuraCast. L'app **MAUI** (Android, iOS, Mac Catalyst, Windows) è quella principale e in produzione; un'app **Uno Platform** (desktop Linux e web) è in costruzione e condivide la logica tramite `RadioE45.Core`.
Piano operativo e stato delle sessioni: [`docs/LINUX-UNO-ROADMAP.md`](docs/LINUX-UNO-ROADMAP.md) (leggere §0, §1, §6 e §12 a inizio sessione).

**Android TV / Google TV** (in costruzione): stessa app MAUI Android con viste dedicate al telecomando in `RadioE45/Views/Tv/`, stessi ViewModel del Core. Piano operativo, ambiente di prova (emulatore `Television_1080p`) e sessioni T0–T9: [`docs/ANDROID-TV-ROADMAP.md`](docs/ANDROID-TV-ROADMAP.md) (leggere §0, §1, §5 e §6 quando si lavora sulla TV). Fattibilità e rischi: [`docs/ANDROID-TV.md`](docs/ANDROID-TV.md). La piattaforma di parità `tv` arriva con la sessione T1: fino ad allora le viste TV non esistono e `parity/` non le conosce.

## Struttura della soluzione (`RadioE45.slnx`)

| Progetto | Ruolo |
|---|---|
| `RadioE45/` | App MAUI: viste (`Views/`, `AppShell.xaml`), stili, `Platforms/`, implementazioni MAUI delle interfacce di piattaforma (`Services/Platform/Maui*.cs`), audio MAUI, CarPlay, Android Auto |
| `RadioE45.Core/` | `net10.0`, senza UI: modelli, client AzuraCast (Refit), servizi radio/podcast, repository SQLite, interfacce di piattaforma (`Services/Platform/`), motore audio live (`LiveStreamAudioService`), **tutti i ViewModel**, stringhe `.resx` |
| `RadioE45.Uno/` | App Uno (`net10.0-desktop`, `net10.0-browserwasm`, tema Fluent, `RootNamespace=RadioE45`): `Views/`, `Styles/`, `Services/Shared/` (desktop + web); `App.Services` è il service provider usato dalle pagine |
| `RadioE45.Core.Tests/` | xUnit v3 (Microsoft.Testing.Platform): logica del Core, snapshot della superficie dei ViewModel, test di ParityCheck |
| `tools/ParityCheck/` | Verifica di parità MAUI ↔ Uno (legge `parity/*.yaml` e i XAML) |

## Regole

1. **MAUI first:** ogni modifica UI nasce in MAUI e poi si porta su Uno (desktop e web).
2. **Nessuna feature senza traccia:** ogni modifica UI aggiorna `parity/features.yaml`. Il disallineamento è ammesso solo se dichiarato come debito (`todo`, oppure `ignore` con motivo in `parity/pages.yaml`).
3. **ViewModel unici nel Core:** comportamento, stato, comandi, testi e validazioni stanno nei ViewModel e nei servizi del Core. Le pagine fanno solo layout e binding; code-behind solo per cose puramente visive.
4. **Core senza UI:** in `RadioE45.Core` vietati `Microsoft.Maui.*`, `CommunityToolkit.Maui.*`, `Uno.*`, `Microsoft.UI.*` e i `#if` di piattaforma (ammesso solo `#if DEBUG`). Le differenze di piattaforma vanno nelle implementazioni delle interfacce di `Services/Platform/`.
5. **Niente sync-over-async nel Core** (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`): WASM è single-thread.
6. **Stringhe uniche:** solo nei `.resx` di `RadioE45.Core/Resources/Strings/` (neutro = italiano, più `en` e `pl`); vedi `docs/LOCALIZATION.md`.
7. **Pagine Uno:** il ViewModel è esposto come proprietà `ViewModel` nel code-behind e si usa `{x:Bind ViewModel.X}`, così `ParityCheck` confronta i binding con MAUI. Conversioni: [`docs/parity/MAUI-TO-UNO.md`](docs/parity/MAUI-TO-UNO.md).
8. **Una modifica a `RadioE45/Views/**`, `RadioE45/AppShell.xaml` o `RadioE45.Core/ViewModels/**` non è finita** finché `parity/features.yaml` non è aggiornato e `ParityCheck` non restituisce 0 o 1. Per portarla su Uno usare la skill `ui-parity-port`.
9. **Commit:** mai eseguire `git commit`; proporre il messaggio come testo, in inglese, con il trailer di parità:
   - `Parity: maui, desktop, web` quando tutto è allineato;
   - `Parity-Debt: <feature-id> (desktop, web)` quando il port è rinviato.
   Stesso vincolo per tag e release: mai `git tag`, `git push`, `gh release create/edit` né lancio del workflow Release; si propongono i comandi.
10. Dopo ogni modifica `.cs`: diagnostica LSP e `dotnet build`. Niente `rm -rf`. Testi di store e changelog in inglese.
11. **Changelog:** ogni modifica visibile all'utente (funzioni, correzioni, testi, piattaforme) aggiunge una riga in inglese sotto `## [Unreleased]` di `CHANGELOG.md` (sottosezioni `New`, `Improved`, `Fixed`; scritta per l'utente, senza nomi di file o classi). Una modifica non è finita senza la riga. `version.json` e il rename di `[Unreleased]` si toccano solo con `/release-prep` o su richiesta.

## Comandi

```bash
dotnet build RadioE45.slnx                                   # tutta la soluzione (MAUI: android, ios, maccatalyst sul Mac)
dotnet build RadioE45 -f net10.0-maccatalyst                 # solo MAUI Mac
dotnet test --project RadioE45.Core.Tests/RadioE45.Core.Tests.csproj
dotnet run --project tools/ParityCheck                       # 0 allineato, 1 solo debito dichiarato, 2 disallineamento (blocca)
dotnet run --project tools/ParityCheck -- --update-surface   # dopo una modifica dell'API pubblica di un ViewModel
dotnet run --project tools/ParityCheck -- --strict --json    # CI / rilascio Uno: anche il debito è errore

dotnet build RadioE45.Uno -f net10.0-desktop
dotnet build RadioE45.Uno -f net10.0-browserwasm
dotnet run --project RadioE45.Uno -f net10.0-desktop             # anche su macOS (stesso renderer Skia)
dotnet run --project RadioE45.Uno -f net10.0-browserwasm         # http://localhost:5045 (la 5000 è dell'AirPlay Receiver di macOS)
RADIOE45_SELFTEST=1 dotnet run --project RadioE45.Uno -f net10.0-desktop   # DEBUG: autotest dei servizi di piattaforma, righe SELFTEST, poi esce
```

Test Linux nella VM Parallels (`ssh radioe45-vm`, sorgenti sincronizzati con `rsync`): comandi in §2.2 del roadmap.

## Release

Procedura completa in [`docs/RELEASE.md`](docs/RELEASE.md): build in locale, bozza GitHub con il workflow `Release` (lancio manuale), caricamento dei pacchetti, pubblicazione. Versione e tag a due cifre da `version.json` (`0.47` → `v0.47`). Per preparare una release usare la skill `release-prep`; le modifiche a `.github/workflows/release.yml` e `linux-deb.yml` solo su richiesta esplicita.

## Sistema di parità

- `parity/features.yaml`: cosa esiste e dove (`done | todo | limited | n/a` per maui, desktop, web; `notes` obbligatorie per `limited` e `n/a`).
- `parity/pages.yaml`: pagina MAUI ↔ pagina Uno, ViewModel, eccezioni `ignore`.
- `parity/vm-surface.txt`: snapshot dell'API pubblica dei ViewModel (generato, non modificare a mano).
- Hook in `.claude/settings.json` (script `tools/parity-hook.sh`): promemoria dopo una modifica UI MAUI; a fine turno `ParityCheck` blocca solo con exit 2.
