# RadioE45 su Linux e Web con Uno Platform — Piano operativo

> Documento operativo, da riprendere a ogni sessione Claude Code.
> Branch: `Linux-support-exp`. Ultimo aggiornamento: 2026-10-07 (app v0.44).
> Stato: **S0 completata, prossima S1** (§12). Aggiornare le checkbox e il registro (§14) alla fine di ogni sessione.

---

## 0. TL;DR per la sessione che riprende

1. Leggere §1 (regole), §6 (sistema di parità) e §12 (piano a sessioni).
2. Trovare in §12 la prima sessione con checkbox aperte ed eseguire solo quella.
3. Alla fine: build verde dell'intera soluzione, checkbox aggiornate, voce nel registro §14, messaggio di commit proposto **come testo** (mai eseguire `git commit`).

---

## 1. Obiettivi e regole fisse

**Obiettivi**
- Nuova app **Uno Platform** con due target dallo stesso progetto: **desktop Linux** (`net10.0-desktop`, Skia, X11 e Wayland) e **web** (`net10.0-browserwasm`).
- L'app **MAUI** esistente (Android, iOS, Mac Catalyst, Windows) resta la principale e continua a essere rilasciata.
- Massimo codice condiviso: tutto ciò che non è layout vive in `RadioE45.Core`.

**Regole decise (non ridiscutere senza l'utente)**
1. **MAUI first:** ogni modifica all'interfaccia nasce nell'app MAUI e poi viene portata su Uno (desktop e web).
2. **Nessuna feature senza traccia:** ogni modifica UI aggiorna il manifest di parità (§6). Il disallineamento è ammesso solo se dichiarato come debito.
3. **ViewModel unici:** la logica sta nei ViewModel del Core, condivisi da MAUI e Uno. Code-behind solo per cose puramente visive.
4. **Stessa soluzione:** `RadioE45.slnx` contiene MAUI, Core, Uno, test e tool.
5. **Tutto sviluppato in sessioni Claude Code:** il processo deve essere eseguibile e verificabile da un agente (skill, hook, tool a riga di comando, criteri di uscita oggettivi).
6. Vincoli dell'utente già validi: niente commit eseguiti da Claude (solo il testo del messaggio, in inglese); testi di store e changelog in inglese; dopo ogni modifica `.cs` controllare la diagnostica LSP e lanciare `dotnet build`; niente `rm -rf`.

---

## 2. Ambiente di sviluppo e test

| Macchina | Uso |
|---|---|
| **Mac (arm64)** — sviluppo principale | Claude Code, build di tutta la soluzione, app MAUI, **Uno desktop eseguito su macOS** (stesso renderer Skia, iterazione veloce), **Uno web** in Chrome (anche tramite gli strumenti claude-in-chrome per gli screenshot). Workload `wasm-tools` già installato. |
| **VM Parallels Ubuntu 24.04.3 (arm64)** | Test Linux veri: audio con libVLC, X11 e Wayland, MPRIS, build dello snap arm64. |
| **GitHub Actions `ubuntu-latest` (x64)** | Build e test, publish linux-x64, snap x64, publish web. |

La VM su Apple Silicon è **arm64**: x64 si verifica solo in CI.

### 2.1 Setup VM (Sessione S0, eseguita dall'utente con il supporto di Claude)

```bash
# nella VM
sudo apt update && sudo apt install -y openssh-server git curl \
  vlc libvlc-dev libvlc5 libx11-dev libfontconfig1 libice6 libsm6 \
  gnome-shell-extension-manager
# .NET 10 SDK (repo Ubuntu oppure script Microsoft)
sudo apt install -y dotnet-sdk-10.0     # se non disponibile: dotnet-install.sh --channel 10.0
dotnet tool install -g uno.check && uno-check --target linux --non-interactive
# snap (fase packaging)
sudo snap install snapcraft --classic && sudo snap install lxd && sudo lxd init --minimal
sudo usermod -aG lxd $USER
```

Accesso da Claude Code sul Mac (da decidere in S0, consigliata la prima opzione):
- **SSH:** chiave dal Mac, alias `radioe45-vm` in `~/.ssh/config`. Comandi tipo `ssh radioe45-vm 'cd ~/RadioE45 && dotnet build ...'`.
- **`prlctl exec "<nome VM>" <cmd>`:** già disponibile sul Mac (`/usr/local/bin/prlctl`), ma esegue i comandi come root.
- Sorgenti: cartella condivisa Parallels (`/media/psf/...`) oppure `git pull` dal branch nella VM. Per evitare conflitti su `bin/obj` meglio **un clone separato nella VM** sincronizzato con `git` o `rsync` (escludendo `bin/` e `obj/`).
- Avvio dell'app in VM con GUI: `ssh radioe45-vm 'DISPLAY=:0 dotnet run ...'` (X11) oppure dalla sessione grafica della VM.

### 2.2 Stato reale dell'ambiente (S0)

| Voce | Valore |
|---|---|
| VM | Parallels `Ubuntu 24.04.3 ARM64`, hostname `ubuntu-gnu-linux-24-04-3`, utente `parallels`, IP `10.211.55.6` |
| Sessione grafica | Wayland (`WAYLAND_DISPLAY=wayland-0`, `XDG_RUNTIME_DIR=/run/user/1000`) |
| .NET nella VM | SDK `10.0.401` in `~/.dotnet` (script `dotnet-install.sh`) |
| .NET sul Mac | SDK `10.0.101` (workload MAUI e `wasm-tools`) |
| PATH via SSH | Export di `DOTNET_ROOT`/`PATH` **in cima** a `~/.bashrc` (marcatore `# radioe45-dotnet`), perché il `.bashrc` di Ubuntu esce subito nelle shell non interattive |
| Parallels CLI sul Mac | `/usr/local/bin/prlctl` |

Comandi standard dal Mac:

```bash
# sincronizza i sorgenti nella VM (rieseguire prima di ogni build o test in VM)
rsync -az --delete --exclude 'bin/' --exclude 'obj/' --exclude '.vs/' --exclude '.idea/' --exclude '*.user' \
  /Users/delfo/Lavori/RadioE45/ radioe45-vm:RadioE45/

# build nella VM
ssh radioe45-vm 'cd ~/RadioE45 && dotnet build RadioE45.Uno -f net10.0-desktop'

# avvio GUI sulla sessione Wayland della VM (in background, log su file)
ssh radioe45-vm 'cd ~/RadioE45 && export WAYLAND_DISPLAY=wayland-0 XDG_RUNTIME_DIR=/run/user/1000 && \
  nohup dotnet run --project RadioE45.Uno -f net10.0-desktop > /tmp/radioe45.log 2>&1 &'
ssh radioe45-vm 'tail -50 /tmp/radioe45.log'
```

Note:
- La VM è una **copia di lavoro sincronizzata a senso unico** (Mac → VM): non modificare i file nella VM. Il `--delete` di rsync sovrascrive.
- SDK diversi tra Mac (10.0.1xx) e VM (10.0.4xx): nel `global.json` **non** fissare la versione dell'SDK, oppure usare `rollForward: latestFeature`. Fissare solo `msbuild-sdks` → `Uno.Sdk`.
- Se l'IP della VM cambia, aggiornare `HostName` in `~/.ssh/config` sul Mac.

---

## 3. Architettura di destinazione

```
RadioE45.slnx
├── global.json                    { "msbuild-sdks": { "Uno.Sdk": "<stabile>" } }
├── version.json                   NBGV, condiviso da tutte le app
├── CLAUDE.md                      regole di progetto (nuovo, §7)
├── .claude/
│   ├── skills/ui-parity-port/     skill per portare modifiche UI MAUI → Uno (§7)
│   └── settings.local.json        hook di parità (§7)
├── parity/
│   ├── features.yaml              manifest delle feature e stato per piattaforma (§6)
│   ├── pages.yaml                 mappa pagine MAUI ↔ Uno ed eccezioni
│   └── vm-surface.txt             snapshot dell'API pubblica dei ViewModel
├── docs/parity/MAUI-TO-UNO.md     tabella di conversione XAML (§8)
├── RadioE45/                      app MAUI (esistente: Views, Platforms, implementazioni MAUI)
├── RadioE45.Core/                 net10.0: Models, Api, Services, Abstractions, ViewModels, Strings
├── RadioE45.Uno/                  Uno.Sdk: net10.0-desktop;net10.0-browserwasm
│   ├── Views/ Controls/ Converters/ Styles/
│   ├── Services/Shared/           implementazioni comuni desktop+web (dispatcher, navigation, dialog…)
│   ├── Services/Desktop/          #if HAS_UNO_SKIA_DESKTOP… (MPRIS, XDG, libVLC)
│   ├── Services/Browser/          OperatingSystem.IsBrowser() / JS interop (Media Session, online)
│   └── Platforms/Desktop|WebAssembly/
├── RadioE45.Core.Tests/           xUnit: logica Core + snapshot API dei ViewModel
└── tools/ParityCheck/             console net10.0: verifica di parità (§6.3)
```

Scelte (default proposti, da confermare in S1):
- Nome **`RadioE45.Uno`**: neutro, perché ospita sia desktop sia web.
- Nuovi progetti **accanto** a `RadioE45/`, senza spostare tutto in `src/`: diff minimo e storia git leggibile.
- Namespace invariati quando si sposta codice nel Core (`RadioE45.Models`, `RadioE45.Services.*`, `RadioE45.ViewModels`), per avere meno diff nell'app MAUI.

Dipendenze: `RadioE45 → Core`, `RadioE45.Uno → Core`, `Core.Tests → Core`, `ParityCheck` legge i file (nessun riferimento ai progetti UI).

---

## 4. Inventario del codice e riuso (fotografia v0.44)

### 4.1 Nel Core così com'è
- `Models/*` (21 file), `Services/IAzuraCast*Api.cs` (Refit).
- `Services/Radio/*`: `NowPlayingService`, `ScheduleService`, `ScheduleFlattener`, `PodcastService`, `SongRequestService`, `StationListService`, `StationDetailService`, `UrlBaseHelper`, eccezioni.
- `Services/Audio`: `IStreamUrlProber`, `StreamUrlProber`, `RemoteArtworkLoader`, `PlatformNowPlaying*`, `IPlatformNowPlayingService`, `IAudioFocusManager`, `Null*`.
- `Services/Data/*` (repository), `Services/Logging/*`, `CrashReportingConfiguration`.
- `Resources/Strings/AppResources*.resx` + `LocalizationResourceManager` (aggiornare il nome base della risorsa al namespace del Core).

### 4.2 Nel Core dopo aver astratto le API MAUI

| API MAUI (usi) | Astrazione nel Core | Impl. MAUI | Impl. Desktop | Impl. Web |
|---|---|---|---|---|
| `MainThread` (33), `IDispatcherTimer` | `IUiDispatcher` | `MainThread` | `DispatcherQueue` | `DispatcherQueue` |
| `Shell.Current.GoToAsync` (~15) | `INavigationService` (route simboliche) | Shell | `Frame` | `Frame` + deep link nell'URL (fase successiva) |
| `DisplayAlertAsync` (4), `IPopupService` (1) | `IDialogService` | Shell/Toolkit popup | `ContentDialog` | `ContentDialog` |
| `Preferences` (14) | `ISettingsStore` | `Preferences` | `ApplicationData.LocalSettings` | `LocalSettings` (localStorage) |
| `Connectivity` (13) | `INetworkMonitor` | `Connectivity` | `NetworkChange` | `navigator.onLine` + eventi `online`/`offline` (JS interop) |
| `FileSystem.AppDataDirectory` (2) | `IAppPaths` | `FileSystem` | XDG (`~/.local/share/RadioE45`) | `ApplicationData.LocalFolder` (IDBFS, attendere l'inizializzazione) |
| `AppInfo`, `DeviceInfo` (4) | `IAppInfoProvider` | `AppInfo` | `ThisAssembly` (NBGV) | `ThisAssembly` |
| `Launcher` (1) | `IUrlLauncher` | `Launcher` | `Launcher.LaunchUriAsync` | `Launcher.LaunchUriAsync` (nuova scheda) |
| `Application.Current.UserAppTheme` | `IThemeService` | attuale `ThemeService` | `RequestedTheme` | `RequestedTheme` |

Dopo questa fase **tutti i 12 ViewModel** si spostano nel Core.

### 4.3 Audio (punto delicato)
- Togliere `Initialize(MediaElement)` da `IAudioService` e `IPodcastPlayerService`. Nel progetto MAUI resta un'interfaccia `IMediaElementHost.Attach(MediaElement)` (cambiano 2 righe in `OnAirPage` e `PodcastEpisodesPage`).
- Uno, desktop e web: `Windows.Media.Playback.MediaPlayer` di Uno (`UnoFeatures` → `MediaPlayerElement`). Su desktop Linux usa libVLC, su web l'elemento HTML5 audio. Secondo la documentazione Uno supporta MP3 e HLS v3/v4 su tutti i target; la sorgente va creata solo con `MediaSource.CreateFromUri()`. Fallback desktop: `LibVLCSharp`.
- Logica di riconnessione e fallback HLS→Icecast (oggi in `AudioService`): **decisione S1**. Estrarla in un `StreamPlaybackCoordinator` nel Core che pilota un `IStreamPlayer` minimale (consigliato: logica scritta una volta e testabile), oppure duplicarla nell'implementazione Uno.

### 4.4 Non riutilizzabile
`Views/*.xaml` (9 pagine, circa 2.300 righe), `AppShell`, `App.xaml`, `Styles/Colors.xaml`, `Controls/VuMeterView` (`GraphicsView` → `SKCanvasElement`), `MenuButton`, `Converters`, `TranslateExtension` (→ `MarkupExtension` WinUI), `Platforms/*`, CarPlay e Android Auto, `Sentry.Maui` (→ pacchetto `Sentry` base sul desktop; sul web da valutare).

---

## 5. Vincoli specifici del web (verificati il 2026-10-07)

| Tema | Stato | Azione |
|---|---|---|
| CORS API AzuraCast | `radioe45.ddns.net` e `demo.azuracast.com` rispondono `Access-Control-Allow-Origin: *` sia alla GET sia alla preflight | OK per le stazioni predefinite. Le stazioni aggiunte dall'utente su server senza CORS **non funzioneranno sul web**: messaggio d'errore dedicato e voce nel manifest. |
| Stream audio | Tutti gli stream E45 sono **HTTPS** (Icecast su porte 8000–8060; HLS su `/hls/.../live.m3u8` con CORS `*`) | Riproduzione tramite `<audio>`: CORS non necessario, niente mixed content. |
| `StreamUrlProber` | Fa una `GET` HTTP sugli URL Icecast, che **non mandano header CORS**: sul web la richiesta fallisce | Implementazione web di `IStreamUrlProber` che non sonda (considera l'URL raggiungibile) oppure tratta un errore CORS come "sconosciuto". |
| SQLite su WASM | La documentazione Uno dichiara supporto, con persistenza tramite IDBFS | **Da verificare nello spike S2.** Piano B: repository web su `LocalSettings`/IndexedDB dietro le stesse interfacce `I*Repository` del Core. |
| Thread | WASM è single-thread: il sync-over-async può bloccare l'app | Nel codice condiviso non ci sono `.Result`, `.Wait()` o `GetResult()` (gli unici due casi sono in `Platforms/` MAUI). Regola in `CLAUDE.md`: vietati nel Core. |
| Controlli multimediali | — | Media Session API (JS interop): metadati e tasti multimediali del browser e del sistema operativo. |
| Riproduzione in background | La scheda continua a suonare; l'autoplay richiede un gesto dell'utente | Il primo Play deve partire da un clic. |
| Crash reporting | Sentry .NET su WASM è limitato | Fase successiva: Sentry JS SDK oppure nessun reporting sul web. |
| Hosting | Sito statico (MIME `.wasm`, `.dat`, `.clr` come da documentazione Uno) | GitHub Pages, Cloudflare Pages o Azure Static Web Apps, con dominio tipo `web.radioe45.it` (S10). PWA installabile. |
| Peso iniziale | Payload .NET WASM di vari MB | Trimming attivo; valutare AOT e la compressione Brotli lato hosting. |

---

## 6. Sistema anti-disallineamento delle feature

Obiettivo: nessuna modifica UI fatta in MAUI deve restare silenziosamente assente su desktop o web. Il sistema ha quattro livelli, tutti eseguibili da un agente.

### 6.1 Livello 1 — Architettura: la logica non può divergere
- Comportamento, stato, comandi, testi e validazioni stanno **solo** nei ViewModel e nei servizi del Core.
- Le pagine MAUI e Uno fanno solo layout e binding. Una feature che cambia il comportamento si scrive una volta sola; le due UI possono differire solo in **come** la espongono.
- Le stringhe sono uniche (`.resx` nel Core): nessuna traduzione duplicata.

### 6.2 Livello 2 — Manifest di parità (`parity/features.yaml`)
Fonte di verità di **cosa** esiste e **dove**:

```yaml
- id: onair.schedule.live-badge
  title: Schedule distingue live (streamer) da playlist, badge LIVE / In orario / In onda
  since: "0.44"
  page: SchedulePopup
  viewmodel: ScheduleViewModel
  status:
    maui: done
    desktop: done
    web: done
  notes: ""

- id: radiolist.add-custom-station
  title: Aggiunta stazione AzuraCast personalizzata
  since: "0.30"
  page: AddStationPage
  viewmodel: AddStationViewModel
  status:
    maui: done
    desktop: done
    web: limited        # done | todo | limited | n/a
  notes: "Web: funziona solo con server con CORS abilitato"
```

- Stati: `done`, `todo` (debito dichiarato), `limited` (con motivo in `notes`), `n/a` (con motivo: es. CarPlay su desktop).
- `parity/pages.yaml` collega le pagine MAUI a quelle Uno ed elenca le eccezioni ammesse per binding o membri.

```yaml
- maui: RadioE45/Views/OnAirPage.xaml
  uno: RadioE45.Uno/Views/OnAirPage.xaml
  viewmodel: OnAirViewModel
  ignore: []                 # membri del VM usati solo su MAUI, con motivo
```

- Il manifest iniziale si genera in S5 da un inventario delle feature della v0.44 (README, `roadmap.md`, pagine). Dopo, ogni modifica UI lo aggiorna.

### 6.3 Livello 3 — Verifica automatica (`tools/ParityCheck`)
Console `net10.0`, eseguibile con `dotnet run --project tools/ParityCheck -- [--strict] [--json]`. Controlli:

1. **Copertura delle pagine:** ogni `RadioE45/Views/*.xaml` ha una pagina corrispondente in `pages.yaml` (oppure `n/a`), e il file Uno esiste.
2. **Binding:** per ogni coppia di pagine estrae i membri del ViewModel referenziati (`{Binding X}`, `{Binding X.Y}`, `Command="{Binding Cmd}"`, `{x:Bind ViewModel.X}`, `{x:Bind VM.Cmd}`). Ogni membro usato in MAUI e assente in Uno è un errore, salvo `ignore` o feature in `todo`/`n/a`.
3. **Stringhe:** chiavi `.resx` usate nella pagina MAUI (`{loc:Translate Key}`) e assenti nella pagina Uno corrispondente → errore, con le stesse eccezioni.
4. **Superficie dei ViewModel:** la lista delle proprietà e dei comandi pubblici dei ViewModel del Core, generata con Roslyn o reflection e confrontata con `parity/vm-surface.txt`. Se cambia senza che `features.yaml` sia stato modificato nel diff corrente → avviso "nuova API ViewModel non tracciata". Aggiornamento con `--update-surface`.
5. **Debito:** elenca le feature `todo`. Con `--strict` (CI, prima di un rilascio Uno) esce con errore se ce ne sono.

Codici di uscita: `0` ok; `1` solo debito dichiarato; `2` disallineamento non dichiarato (blocca).

Test di supporto in `RadioE45.Core.Tests`: snapshot della superficie dei ViewModel e test sulla logica (`ScheduleFlattener`, now playing, riconnessione).

### 6.4 Livello 4 — Processo in Claude Code
- **`CLAUDE.md` di progetto** (§7.1): regola MAUI-first, obbligo di aggiornare `features.yaml`, comandi standard.
- **Skill `ui-parity-port`** (§7.2): procedura guidata per portare una modifica UI da MAUI a Uno.
- **Hook** (§7.3): promemoria quando si modificano viste o ViewModel; `ParityCheck` alla fine della sessione, che blocca con exit code 2.
- **Messaggio di commit:** trailer obbligatorio nel testo proposto all'utente:
  - `Parity: maui, desktop, web` quando tutto è allineato;
  - `Parity-Debt: <feature-id> (desktop, web)` quando il port è rinviato.
- **CI:** `ParityCheck` su ogni push o PR; `--strict` nel job di rilascio Uno.
- **Verifica visiva** (manuale, assistita): screenshot di MAUI (Mac Catalyst), Uno desktop (macOS) e Uno web (Chrome via claude-in-chrome), affiancati per le pagine toccate.

### 6.5 Flusso tipico di una modifica UI (regime)
1. Modifica in MAUI (Views + ViewModel nel Core) e build.
2. Aggiornamento di `features.yaml` (nuova feature o modifica di una esistente; stato `desktop`/`web` = `todo`).
3. Nella stessa sessione, se possibile, skill `ui-parity-port` → port su `RadioE45.Uno` → stato `done`.
4. `ParityCheck` verde (0) o con solo debito dichiarato (1).
5. Messaggio di commit proposto con il trailer di parità.

---

## 7. Configurazione Claude Code (da creare in S5)

### 7.1 `CLAUDE.md` di progetto (contenuto minimo)
- Struttura della soluzione e ruolo di ciascun progetto.
- Regole del §1, regole del Core (nessun `Microsoft.Maui.*`, nessun sync-over-async).
- Comandi:
  - `dotnet build RadioE45.slnx`
  - `dotnet build RadioE45.Uno -f net10.0-desktop`
  - `dotnet build RadioE45.Uno -f net10.0-browserwasm`
  - `dotnet run --project RadioE45.Uno -f net10.0-desktop`
  - `dotnet run --project RadioE45.Uno -f net10.0-browserwasm`
  - `dotnet test RadioE45.Core.Tests`
  - `dotnet run --project tools/ParityCheck`
- "Una modifica a `RadioE45/Views/**` o `RadioE45.Core/ViewModels/**` non è finita finché `ParityCheck` non restituisce 0 o 1 e `features.yaml` non è aggiornato."
- Riferimento a questo documento e a `docs/parity/MAUI-TO-UNO.md`.

### 7.2 Skill `.claude/skills/ui-parity-port/SKILL.md`
Input: feature id oppure range git. Passi:
1. `git diff` delle viste MAUI e dei ViewModel coinvolti; leggere la voce in `features.yaml`.
2. Aprire la pagina Uno corrispondente da `pages.yaml`.
3. Convertire con la tabella `MAUI-TO-UNO.md`, mantenendo struttura e stili condivisi Uno.
4. Build di `net10.0-desktop` e `net10.0-browserwasm`, diagnostica LSP.
5. `ParityCheck`: risolvere tutti gli errori di livello 2.
6. Opzionale: avvio desktop su macOS e web su Chrome, screenshot.
7. Stato in `features.yaml` → `done` (o `limited` con nota).
8. Proporre il messaggio di commit con il trailer.

### 7.3 Hook (in `.claude/settings.local.json`, coerente con la configurazione attuale dell'utente)
- `PostToolUse` su `Edit|Write` con path `RadioE45/Views/*`, `RadioE45.Core/ViewModels/*`, `RadioE45/Resources/Styles/*` → contesto aggiuntivo: "Modifica UI MAUI: aggiornare `parity/features.yaml` e portare su RadioE45.Uno (skill ui-parity-port) oppure dichiarare il debito."
- `Stop` → script `tools/parity-hook.sh` che lancia `ParityCheck --json`; se l'exit code è 2 restituisce `decision: block` con l'elenco dei disallineamenti, così la sessione non si chiude con incoerenze non dichiarate. Con exit 0 o 1 non blocca, e con 1 riporta il debito.
- Lo script deve essere veloce (< 5 s): `ParityCheck` legge solo i file, e per ripetere le esecuzioni si usa una build già compilata (`dotnet tools/ParityCheck/bin/.../ParityCheck.dll`).

---

## 8. Conversione UI MAUI → Uno/WinUI (`docs/parity/MAUI-TO-UNO.md`)

| MAUI | Uno/WinUI |
|---|---|
| `Shell` + `TabBar` (OnAir, SongRequest, Podcast) | `NavigationView` (top su desktop e web largo, `LeftCompact` su schermi stretti) + `Frame`, tramite `INavigationService` |
| `ContentPage` | `Page` |
| `VerticalStackLayout` / `HorizontalStackLayout` | `StackPanel` (`Orientation`, `Spacing`) |
| `Label` | `TextBlock` |
| `CollectionView` | `ListView` / `ItemsRepeater` |
| `Entry` / `Editor` | `TextBox` (`AcceptsReturn` per `Editor`) |
| `Switch` / `Slider` / `Picker` | `ToggleSwitch` / `Slider` / `ComboBox` |
| `ActivityIndicator` | `ProgressRing` |
| `RefreshView` | `RefreshContainer` |
| Toolkit `Popup` | `ContentDialog` / `Flyout` |
| `IsVisible="{Binding B}"` | `Visibility="{x:Bind ViewModel.B, Converter=BoolToVisibility}"` |
| `AppThemeBinding` | `{ThemeResource}` + `ThemeDictionaries` Light/Dark |
| `FontImageSource` Font Awesome | `FontIcon FontFamily="ms-appx:///Assets/Fonts/FaSolid.ttf#Font Awesome 6 Free Solid"` |
| `GraphicsView` | `SKCanvasElement` |
| `{loc:Translate Key}` | `{loc:Translate Key}` (`MarkupExtension` WinUI equivalente, stessa sintassi per facilitare `ParityCheck`) |
| `{Binding}` compilato con `x:DataType` | `{x:Bind ViewModel.X, Mode=OneWay}` |

Per facilitare `ParityCheck`, nelle pagine Uno il ViewModel si espone sempre come proprietà `ViewModel` nel code-behind e si usa `x:Bind ViewModel.*`.

Ordine di porting delle pagine: OnAir → RadioList / Add / Edit → Settings → SchedulePopup → SongRequest → PodcastList / PodcastEpisodes → menu app.

---

## 9. Integrazione di piattaforma

| Funzione | Desktop Linux | Web |
|---|---|---|
| Tasti multimediali e metadati di sistema | MPRIS (`Tmds.DBus`), implementa `IPlatformNowPlayingService` | Media Session API (JS interop), stessa interfaccia |
| Dati | `~/.local/share/RadioE45` | IDBFS / localStorage |
| Icona e launcher | `.desktop` (generato dal publish snap) | `manifest.webmanifest` (PWA) e favicon |
| Istanza singola | Socket in `$XDG_RUNTIME_DIR` (opzionale) | n/a |
| Chiusura finestra | Stop dell'audio (tray in una fase successiva) | n/a |
| Deep link | n/a | URL `#/station/<shortcode>` (fase successiva) |

---

## 10. Packaging e distribuzione

| Target | Comando o strumento | Note |
|---|---|---|
| Snap (Linux, primo canale) | `dotnet publish RadioE45.Uno -f net10.0-desktop -c Release -p:SelfContained=true -p:PackageFormat=snap` | Classic per default: lo Snap Store richiede una review per classic. Valutare `strict` con le plug `audio-playback network desktop wayland x11` e libVLC nello snap. In CI: `-p:UnoSnapcraftAdditionalParameters=--destructive-mode`. Uno non supporta ancora NativeAOT, R2R e single-file per Linux. |
| AppImage | Publish self-contained + `appimagetool` | GitHub Releases; libVLC richiesto al sistema o incluso. |
| Flatpak | Manifest + `metainfo.xml` | Dopo lo snap; review su Flathub. |
| Web | `dotnet publish RadioE45.Uno -f net10.0-browserwasm -c Release -o ./publish` | Sito statico con MIME configurati e Brotli; PWA. |

Architetture desktop: `linux-arm64` (VM e Raspberry Pi) e `linux-x64` (CI).

---

## 11. Rischi

| Rischio | Impatto | Mitigazione |
|---|---|---|
| Regressioni MAUI durante l'estrazione del Core | Alto (app in produzione) | Fasi piccole, build di tutti i TFM disponibili sul Mac, checklist di regressione manuale (play, cambio stazione, schedule, podcast, Android Auto, CarPlay) prima di ogni merge su `main` |
| Audio libVLC/HTML5 diverso da MediaElement (buffering, metadati, errori) | Alto | Spike S2 con criteri misurabili; coordinatore condiviso testato |
| SQLite non persistente o lento su WASM | Medio | Spike S2; piano B con repository web |
| Server AzuraCast di terze parti senza CORS | Basso | Stato `limited` sul web, messaggio dedicato |
| Il port UI resta indietro | Medio | Sistema di parità §6 (hook che blocca, CI strict) |
| Divergenze di layout non rilevabili dai binding | Basso | Verifica visiva assistita nella skill |

---

## 12. Piano a sessioni Claude Code

Ogni sessione è dimensionata per una singola sessione Claude Code. Criterio comune di uscita: `dotnet build RadioE45.slnx` verde sul Mac, nessuna regressione MAUI nota, checkbox e registro aggiornati, messaggio di commit proposto.

### S0 — Ambiente (utente + Claude) · VM — ✅ completata 2026-10-07
- [x] VM Ubuntu 24.04.3 arm64 (`ubuntu-gnu-linux-24-04-3`, sessione Wayland) con le dipendenze di §2.1; test audio `cvlc` ok.
- [x] Accesso SSH dal Mac: alias `radioe45-vm` → `parallels@10.211.55.6`, chiave `~/.ssh/radioe45_linuxvm`.
- [x] Repo copiato in `~/RadioE45` nella VM con rsync (vedi §2.2).
- [x] `uno-check --target linux` ok, a parte `unosdk` (da ricontrollare in S6 con il `global.json` del repo).
- **Uscita:** `ssh radioe45-vm 'dotnet --version'` → `10.0.401`. ✅

### S1 — Decisioni e spike desktop · Mac + VM
- [ ] Confermare i default del §3 (nome `RadioE45.Uno`, progetti accanto, namespace invariati) e la decisione del §4.3 (coordinatore audio condiviso o duplicato).
- [ ] Progetto spike usa-e-getta in `spikes/UnoAudioSpike` (fuori dalla soluzione): `net10.0-desktop`, `MediaPlayer` di Uno.
- [ ] Nella VM: stream MP3 E45 (`https://radioe45.ddns.net:8060/radio.mp3`), HLS (`https://radioe45.ddns.net/hls/radioe45/live.m3u8`), pausa/ripresa, volume, cambio stazione, rete staccata e riattaccata. Su X11 e su Wayland.
- [ ] Misure: tempo al primo audio, comportamento in caso di errore.
- [ ] `sqlite-net` + `bundle_e_sqlite3` su linux-arm64.
- **Uscita:** player scelto (Uno `MediaPlayer` o `LibVLCSharp`), lista definitiva dei pacchetti di sistema.

### S2 — Spike web · Mac
- [ ] Stesso spike con target `net10.0-browserwasm`: MP3 e HLS in Chrome, Firefox e Safari, primo play da clic, volume.
- [ ] Chiamata Refit a `https://radioe45.ddns.net/api/nowplaying` dal browser (CORS).
- [ ] `sqlite-net` su WASM con persistenza dopo il ricaricamento della pagina. Se fallisce → piano B (§5).
- [ ] Dimensione del payload e tempo di primo caricamento.
- **Uscita:** fattibilità web confermata, strategia di storage scelta. Eliminare la cartella `spikes/` o tenerla fuori dalla soluzione.

### S3 — Estrazione del Core (parte 1) · Mac
- [ ] Creare `RadioE45.Core` (`net10.0`, `Nullable`, stessi analyzer); aggiungerlo alla `.slnx`.
- [ ] Spostare i file di §4.1, con `git mv` per preservare la storia.
- [ ] Spostare `.resx` e `LocalizationResourceManager`; verificare it/en/pl.
- [ ] Core: solo `sqlite-net-pcl` (senza bundle); il progetto MAUI mantiene la sua configurazione SQLite attuale.
- [ ] `AddRadioE45Core()` per la DI; `MauiProgram` lo usa.
- [ ] Creare `RadioE45.Core.Tests` con i primi test (`ScheduleFlattener`).
- **Uscita:** app MAUI invariata (verifica manuale su Mac Catalyst e un dispositivo mobile).

### S4 — Astrazioni e ViewModel nel Core · Mac
- [ ] Interfacce di §4.2 nel Core; implementazioni MAUI nel progetto `RadioE45`.
- [ ] Refactoring dei 12 ViewModel; spostamento nel Core.
- [ ] Spostare nel Core `AzuraStationCatalog`, `DatabaseService`, `CrashReportingSettings`, `CrashDiagnostics`.
- [ ] Audio: separare `Initialize(MediaElement)` (§4.3) ed eventuale `StreamPlaybackCoordinator` con test.
- [ ] Verifica: `grep -rE "Microsoft\.Maui|CommunityToolkit\.Maui" RadioE45.Core` → vuoto.
- **Uscita:** Core UI-agnostic, MAUI invariata, test verdi. Può richiedere 2 sessioni (S4a ViewModel, S4b audio).

### S5 — Sistema di parità · Mac
- [ ] `tools/ParityCheck` con i controlli 1–5 del §6.3, aggiunto alla `.slnx`.
- [ ] `parity/pages.yaml` (9 pagine), `parity/features.yaml` generato dall'inventario delle feature della v0.44, con desktop e web in `todo`.
- [ ] `parity/vm-surface.txt` iniziale.
- [ ] `CLAUDE.md` di progetto (§7.1), skill `ui-parity-port` (§7.2), hook (§7.3), `docs/parity/MAUI-TO-UNO.md`.
- [ ] Test dell'hook: modificare un binding in una vista MAUI e verificare che la sessione venga bloccata con exit 2.
- **Uscita:** `ParityCheck` restituisce 1 (tutto il debito dichiarato, nessun errore 2).

### S6 — Scheletro `RadioE45.Uno` · Mac + VM
- [ ] `global.json` con `Uno.Sdk`; progetto da template `unoapp` con target desktop e browserwasm, `UnoFeatures`: `MediaPlayerElement`, `Hosting`, `Toolkit` (solo se serve); niente MVUX.
- [ ] Riferimento al Core; NBGV attivo; icona, font, colori base.
- [ ] Shell: `NavigationView` + `Frame` + `INavigationService` Uno; pagine vuote per le tre tab.
- [ ] Implementazioni condivise: `IUiDispatcher`, `IDialogService`, `ISettingsStore`, `IAppInfoProvider`, `IUrlLauncher`, `IThemeService`.
- **Uscita:** l'app si apre su macOS (desktop), in Chrome (web) e nella VM; tutta la soluzione compila.

### S7 — Servizi di piattaforma Uno · Mac + VM
- [ ] Audio: player Uno per radio e podcast (decisione S1).
- [ ] Desktop: `XdgAppPaths`, `LinuxNetworkMonitor`, SQLite.
- [ ] Web: storage (decisione S2), `BrowserNetworkMonitor`, `IStreamUrlProber` senza sonda.
- [ ] Sentry base sul desktop.
- **Uscita:** da una pagina di debug, play/stop della stazione E45 funziona su macOS desktop, VM e browser.

### S8.x — Porting delle pagine (una o due pagine per sessione, con la skill `ui-parity-port`) · Mac (+ VM per le verifiche)
- [ ] S8.1 Stili, `TranslateExtension`, `OnAirPage` (incluso `VuMeterView` Skia)
- [ ] S8.2 `RadioListPage`, `AddStationPage`, `EditStationPage`
- [ ] S8.3 `SettingsPage`, menu app
- [ ] S8.4 `SchedulePopup`
- [ ] S8.5 `SongRequestPage`
- [ ] S8.6 `PodcastListPage`, `PodcastEpisodesPage`
- [ ] S8.7 Layout desktop e web largo (due colonne), scorciatoie da tastiera
- **Uscita di ogni sessione:** le feature della pagina sono `done` in `features.yaml` e `ParityCheck` non segnala errori per quella pagina.

### S9 — Integrazione di piattaforma · VM + Mac
- [ ] MPRIS (desktop), Media Session API (web).
- [ ] PWA: manifest, icone, service worker per lo shell offline.
- [ ] Test su GNOME Wayland, KDE, Xfce/X11; Chrome, Firefox, Safari.

### S10 — Packaging, CI, hosting · Mac + VM + GitHub
- [ ] Snap arm64 nella VM; snap x64 in CI.
- [ ] AppImage x64 e arm64.
- [ ] Workflow GitHub Actions: build, test, `ParityCheck`, publish desktop e web, artefatti.
- [ ] Hosting web (decisione: GitHub Pages, Cloudflare o Azure SWA; dominio).
- [ ] Aggiornare `docs/VERSION-BUMP.md` e `docs/STORE-DISTRIBUTION.md`.

### S11 — Rilascio
- [ ] `ParityCheck --strict` verde (nessun `todo`).
- [ ] Beta: snap channel `beta`, web su un URL di staging.
- [ ] Testi e screenshot per lo store in inglese; README con l'installazione su Linux e il link web.
- [ ] Rilascio stable; merge del branch su `main` (fatto dall'utente).

### Stima indicativa
| Blocco | Sessioni |
|---|---|
| S0–S2 ambiente e spike | 3 |
| S3–S4 Core | 3–4 |
| S5 parità | 1–2 |
| S6–S7 base Uno | 2–3 |
| S8 UI | 7–9 |
| S9–S11 integrazione e rilascio | 3–5 |
| **Totale** | **circa 19–26 sessioni** |

---

## 13. Decisioni aperte

| # | Decisione | Quando | Default proposto |
|---|---|---|---|
| D1 | Nome del progetto Uno | S1 | `RadioE45.Uno` |
| D2 | Posizione dei progetti | S1 | Accanto a `RadioE45/` |
| D3 | Coordinatore audio condiviso o duplicato | S1 | Condiviso |
| D4 | Player desktop: Uno `MediaPlayer` o `LibVLCSharp` | Fine S1 | Uno `MediaPlayer` |
| D5 | Storage web: SQLite WASM o repository dedicati | Fine S2 | SQLite, se lo spike passa |
| D6 | Navigazione: `Frame` scritto a mano o `Uno.Extensions.Navigation` | S6 | A mano |
| D7 | Canali desktop: Snap Store, Flathub, AppImage | S10 | Snap + AppImage, Flathub dopo |
| D8 | Hosting e dominio web | S10 | Da decidere con l'utente |
| D9 | Hook Stop bloccante o solo avviso | S5 | Bloccante solo per exit 2 |
| D10 | Rilascio delle app Uno legato alle versioni MAUI (stesso `version.json`) | S10 | Sì, stessa versione |

---

## 14. Registro delle sessioni

| Data | Sessione | Esito | Note |
|---|---|---|---|
| 2026-10-07 | S0 | Completata | VM pronta, SSH e rsync configurati, `dotnet` 10.0.401 raggiungibile via SSH |
| 2026-10-07 | Pianificazione | Documento creato | Verificati CORS dell'API AzuraCast e HTTPS degli stream E45; Mac arm64, `prlctl` disponibile, workload `wasm-tools` installato |

---

## 15. Riferimenti

- Uno Skia desktop — https://platform.uno/docs/articles/features/using-skia-desktop.html
- Uno publishing Linux (snap) — https://platform.uno/docs/articles/uno-publishing-desktop.linux.html
- Uno publishing WebAssembly — https://platform.uno/docs/articles/uno-publishing-webassembly.html
- Uno hosting WebAssembly — https://platform.uno/docs/articles/how-to-host-a-webassembly-app.html
- Uno MediaPlayerElement — https://platform.uno/docs/articles/controls/MediaPlayerElement.html
- Uno file system su WASM (IDBFS) — https://platform.uno/docs/articles/features/file-management.html
- Uno ApplicationData e settings — https://platform.uno/docs/articles/features/applicationdata.html
- Uno: aggiungere piattaforme a un progetto — https://platform.uno/docs/articles/guides/how-to-add-platforms-existing-project.html
- MPRIS — https://specifications.freedesktop.org/mpris-spec/latest/
- Tmds.DBus — https://github.com/tmds/Tmds.DBus
- Media Session API — https://developer.mozilla.org/docs/Web/API/Media_Session_API
- Claude Code hooks — https://docs.claude.com/en/docs/claude-code/hooks
