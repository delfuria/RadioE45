# RadioE45 su Linux e Web con Uno Platform — Piano operativo

> Documento operativo, da riprendere a ogni sessione Claude Code.
> Branch di lavoro attuale: `S6` (derivato da `S5B`). Ultimo aggiornamento: 2026-10-15 (app v0.44).
> Stato: **S0–S6 completate; prossima sessione: S7** (§12). Aggiornare le checkbox e il registro (§14) alla fine di ogni sessione.

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
| .NET nella VM | SDK `10.0.203` in `~/.dotnet` (script `dotnet-install.sh`; in S0 era 10.0.401) |
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

# avvio GUI sullo schermo della VM (Uno usa X11: sotto GNOME Wayland passa da XWayland :0)
ssh radioe45-vm 'cd ~/RadioE45 && export DISPLAY=:0 XAUTHORITY=$(ls /run/user/1000/.mutter-Xwaylandauth.* | head -1) \
  XDG_RUNTIME_DIR=/run/user/1000 DBUS_SESSION_BUS_ADDRESS=unix:path=/run/user/1000/bus && \
  nohup dotnet run --no-build --project RadioE45.Uno -f net10.0-desktop > /tmp/radioe45.log 2>&1 < /dev/null &'
ssh radioe45-vm 'tail -50 /tmp/radioe45.log'
ssh radioe45-vm 'pkill -f "net10.0-desktop/RadioE45.Uno"'   # chiusura
```

Note:
- La VM è una **copia di lavoro sincronizzata a senso unico** (Mac → VM): non modificare i file nella VM. Il `--delete` di rsync sovrascrive.
- SDK diversi tra Mac (10.0.1xx) e VM (10.0.4xx): il `global.json` fissa `"sdk": { "version": "10.0.101", "rollForward": "latestFeature" }`, cioè 10.0.101 come minimo e qualsiasi 10.0.x più recente va bene. Mai `rollForward: disable`, perché romperebbe la build sulla VM. In S6 si aggiunge `msbuild-sdks` → `Uno.Sdk`.
- Se l'IP della VM cambia, aggiornare `HostName` in `~/.ssh/config` sul Mac.
- Il file `XAUTHORITY` di XWayland (`.mutter-Xwaylandauth.*`) cambia a ogni login nella VM: per questo si risolve con `ls` invece di scriverlo fisso.
- `dotnet run` lanciato via SSH senza `nohup … < /dev/null &` tiene occupata la sessione: per i test interattivi usare sempre la forma in background.
- Sul Mac `timeout` non esiste: per i test automatici con limite di tempo usare `perl -e 'alarm 120; exec @ARGV' dotnet run …`.

---

## 3. Architettura di destinazione

```
RadioE45.slnx
├── global.json                    sdk 10.0.101 + latestFeature, test runner MTP, msbuild-sdks → Uno.Sdk (S6)
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
- Uno, desktop e web: `Windows.Media.Playback.MediaPlayer` di Uno (`UnoFeatures` → `MediaPlayerElement`). Su desktop Linux usa libVLC, su macOS il player di sistema, su web l'elemento HTML5 audio. La sorgente va creata solo con `MediaSource.CreateFromUri()`. **Scelto in S1 (D4)**; `LibVLCSharp` resta come piano B solo se in S7 servissero eventi più ricchi.
- **Esiti dello spike S1, da rispettare nell'implementazione:**
  - Il `MediaPlayer` funziona **senza elemento visuale** (`new MediaPlayer()`): il servizio audio Uno non dipende dalla pagina, a differenza di MAUI.
  - `PlaybackSession.BufferingStarted` e `BufferingEnded` **non sono implementati** in Uno (warning `Uno0001`): il buffering si ricava da `PlaybackState == Buffering`.
  - All'apertura lo stato **oscilla** (Opening → Buffering → Opening → Playing → Paused → Buffering → Playing in pochi ms) e la semantica cambia tra backend: su macOS `Playing` arriva dopo 3 ms, prima dell'audio reale. Il coordinatore deve applicare un **debounce** (es. 300–500 ms di stato stabile) prima di notificare la UI.
  - `PlaybackSession.Position` non è affidabile per gli stream live: con libVLC resta 0 sull'MP3 e avanza sull'HLS; su macOS succede il contrario. **Non usarla** come watchdog di stallo.
  - Caduta di rete breve (circa 20 s): libVLC **non genera eventi** (né `MediaFailed` né cambi di stato) e riprende da solo circa 5 s dopo il ritorno della rete. La riconnessione attiva va comandata da `INetworkMonitor` (ritorno della rete → riapertura se l'audio non è ripartito entro N secondi). Le cadute lunghe vanno verificate in S7.
  - Stop: non esiste `Stop()`; usare `Pause()` + `Source = null`. `Play()` dopo uno stop non fa nulla: va riaperta la sorgente.
- **Regola di prodotto (utente, 2026-10-07): per la radio in diretta Pausa e Stop chiudono entrambi lo stream.** Una diretta non si mette in pausa: `MediaPlayer.Pause()` lascerebbe la connessione aperta e bufferizzata, e alla ripresa si sentirebbe audio non più live. Quindi:
  - `PauseAsync()` = chiudere la sorgente (`Pause()` + `Source = null`) mantenendo stazione e metadati, così la UI e MPRIS/Media Session mostrano "in pausa";
  - `ResumeAsync()` = **riaprire** lo stream della stazione corrente (stessa logica HLS→Icecast di `PlayAsync`);
  - `StopAsync()` = chiudere la sorgente e azzerare lo stato.
  È lo stesso comportamento già presente in `AudioService.PauseAsync` di MAUI: va spostato nel coordinatore condiviso (D3) e coperto da test. **Non vale per i podcast:** `PodcastPlayerService` mantiene la pausa vera, con ripresa dalla posizione.
- Logica di riconnessione e fallback HLS→Icecast (oggi in `AudioService`): **decisione S1**. Estrarla in un `StreamPlaybackCoordinator` nel Core che pilota un `IStreamPlayer` minimale (consigliato: logica scritta una volta e testabile), oppure duplicarla nell'implementazione Uno.

### 4.4 Non riutilizzabile
`Views/*.xaml` (9 pagine, circa 2.300 righe), `AppShell`, `App.xaml`, `Styles/Colors.xaml`, `Controls/VuMeterView` (`GraphicsView` → `SKCanvasElement`), `MenuButton`, `Converters`, `TranslateExtension` (→ `MarkupExtension` WinUI), `Platforms/*`, CarPlay e Android Auto, `Sentry.Maui` (→ pacchetto `Sentry` base sul desktop; sul web da valutare).

---

## 5. Vincoli specifici del web (verificati il 2026-10-07)

| Tema | Stato | Azione |
|---|---|---|
| CORS API AzuraCast | `radioe45.ddns.net` e `demo.azuracast.com` rispondono `Access-Control-Allow-Origin: *` sia alla GET sia alla preflight | OK per le stazioni predefinite. Le stazioni aggiunte dall'utente su server senza CORS **non funzioneranno sul web**: messaggio d'errore dedicato e voce nel manifest. |
| Stream audio | Tutti gli stream E45 sono **HTTPS** (Icecast su porte 8000–8060; HLS su `/hls/.../live.m3u8` con CORS `*`) | Riproduzione tramite `<audio>`: CORS non necessario, niente mixed content. |
| `StreamUrlProber` | **Verificato in S2:** Icecast E45 risponde con CORS se la richiesta ha un `Origin` (`Access-Control-Allow-Origin` = origine riflessa, `Range` e `Icy-*` ammessi). La sonda dal browser funziona (HTTP 200 `audio/mpeg`, circa 230 ms) | Nessuna implementazione web dedicata per le stazioni E45. Per server Icecast di terze parti senza CORS: trattare l'errore della sonda come "sconosciuto" e provare comunque la riproduzione. |
| SQLite su WASM | **Verificato in S2:** `sqlite-net-pcl` 1.11.285 + `bundle_e_sqlite3` 3.0.4 funzionano su `net10.0-browserwasm`; il file in `ApplicationData.LocalFolder` (`/local/...`) **persiste dopo il ricaricamento** (IDBFS). Warning di build `WASM0001` sulle funzioni varargs (`sqlite3_config`, `sqlite3_db_config`): innocui finché non vengono chiamate | Stessa implementazione SQLite del desktop; piano B non necessario. Prima di aprire il DB attendere `LocalFolder.CreateFolderAsync(...)` (inizializzazione IDBFS). |
| Thread | WASM è single-thread: il sync-over-async può bloccare l'app | Nel codice condiviso non ci sono `.Result`, `.Wait()` o `GetResult()` (gli unici due casi sono in `Platforms/` MAUI). Regola in `CLAUDE.md`: vietati nel Core. |
| Controlli multimediali | — | Media Session API (JS interop): metadati e tasti multimediali del browser e del sistema operativo. |
| Riproduzione e autoplay | **Verificato in S2:** senza un gesto reale dell'utente `play()` fallisce (`NotAllowedError: play() failed because the user didn't interact with the document first`). Con clic reale MP3 e HLS partono subito in Chrome e Safari | **Mai** avviare la riproduzione automaticamente all'apertura sul web (niente "riprendi ultima stazione"); ogni `PlayAsync`/`ResumeAsync` deve partire da un'azione dell'utente. Gestire `NotAllowedError` mostrando lo stato "tocca Play". I clic simulati dagli strumenti di automazione non valgono come gesto: i test audio web restano manuali. |
| Crash reporting | Sentry .NET su WASM è limitato | Fase successiva: Sentry JS SDK oppure nessun reporting sul web. |
| Hosting | Sito statico (MIME `.wasm`, `.dat`, `.clr` come da documentazione Uno) | GitHub Pages, Cloudflare Pages o Azure Static Web Apps, con dominio tipo `web.radioe45.it` (S10). PWA installabile. |
| Peso iniziale | **Misurato in S2** (spike, Release): circa 47 MB non compressi, **circa 13,5 MB Brotli** (totale dei file pubblicati, il primo caricamento reale è inferiore) | Servire `.br` dall'hosting; splash di caricamento; valutare AOT/trimming più aggressivo in S10. |

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
- page: OnAir                # nome logico, usato da features.yaml
  maui: RadioE45/Views/OnAirPage.xaml
  uno: RadioE45.Uno/Views/OnAirPage.xaml   # oppure n/a + reason
  viewmodels: [OnAirViewModel]
  ignore: []                 # membri del VM o chiavi di stringa usati solo su MAUI: - name: X / reason: ...
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

### 7.3 Hook (in `.claude/settings.json`, versionato; script `tools/parity-hook.sh`)
- `PostToolUse` su `Edit|Write|MultiEdit` con path `RadioE45/Views/*`, `RadioE45/AppShell.xaml`, `RadioE45.Core/ViewModels/*`, `RadioE45/Resources/Styles/*` → contesto aggiuntivo: "Modifica UI MAUI: aggiornare `parity/features.yaml` e portare su RadioE45.Uno (skill ui-parity-port) oppure dichiarare il debito."
- `Stop` → script `tools/parity-hook.sh` che lancia `ParityCheck --json`; se l'exit code è 2 restituisce `decision: block` con l'elenco dei disallineamenti, così la sessione non si chiude con incoerenze non dichiarate. Con exit 0 o 1 non blocca e resta in silenzio (la lista del debito, ripetuta a ogni fine turno, sarebbe solo rumore). Se `stop_hook_active` è vero (Claude sta già continuando per colpa dell'hook) non blocca una seconda volta: mostra un `systemMessage` all'utente, per evitare cicli. Se la build o ParityCheck falliscono, mostra un `systemMessage` e non blocca.
- Lo script è veloce: esegue la DLL già compilata (circa 0,1–0,4 s) e ricompila `tools/ParityCheck` solo se `.cs`, `.csproj` o `.resx` del Core o del tool sono più recenti del file `tools/ParityCheck/obj/parity-hook.stamp` (circa 3 s).

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
| `{loc:Translate Key}` | `{loc:Translate Key=Key}` (`MarkupExtension` WinUI: niente argomenti posizionali, `Key=` obbligatorio; ParityCheck accetta entrambe le forme) |
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

### S1 — Decisioni e spike desktop · Mac + VM — ✅ completata 2026-10-07
- [x] Decisioni confermate dall'utente: D1 `RadioE45.Uno`, D2 progetti accanto a `RadioE45/`, D3 coordinatore audio **condiviso** nel Core.
- [x] Spike `spikes/UnoAudioSpike` (ignorato da git tramite `.gitignore`, fuori dalla soluzione): template `unoapp` blank, `Uno.Sdk` **6.7.30**, target desktop + browserwasm, `UnoFeatures` `SkiaRenderer; MediaPlayerElement`. Variabili: `SPIKE_AUTO=1` (test scriptato), `SPIKE_EXIT=1`, `SPIKE_STANDALONE=1` (player senza elemento).
- [x] VM (Ubuntu arm64, GNOME Wayland → XWayland): MP3 E45, HLS E45, seconda stazione, pausa/ripresa, volume, cambio sorgente. Audio confermato dall'utente ("ottimo, udito subito"). Stato `Playing` in circa 0,3–0,6 s.
- [x] Rete staccata per circa 20 s: audio interrotto circa 5 s dopo il distacco, ripreso da solo circa 5 s dopo il ritorno della rete; nessun evento al codice (vedi §4.3).
- [x] Una sessione X11 pura non è stata testata: Uno usa comunque X11 (via XWayland). Da ripetere in S9 con "Ubuntu on Xorg".
- [x] SQLite (`sqlite-net-pcl` 1.11.285 + `SQLitePCLRaw.bundle_e_sqlite3` 3.0.4) su linux-arm64 e macOS: ok, persistente tra avvii (`~/.local/share/<app>/<appId>/LocalState`).
- [x] HTTP: API nowplaying ok (circa 0,7–1 s); sonda `GET` sull'Icecast ok sul desktop.
- [x] macOS desktop (`net10.0-desktop`): audio, HTTP e SQLite ok → ciclo di sviluppo veloce sul Mac confermato.
- [x] **Problema bloccante risolto:** su linux-arm64 `libSkiaSharp.so` 3.119.2 (portato da Uno) va in errore all'avvio con `symbol lookup error: … undefined symbol: FT_Get_BDF_Property`. Fix: `PackageReference` a **`SkiaSharp.NativeAssets.Linux.NoDependencies`** 3.119.2 nel progetto Uno (alternativa: `LD_PRELOAD=/lib/aarch64-linux-gnu/libfreetype.so.6`). Da verificare anche su x64 in CI.
- **Uscita:** D4 = Uno `MediaPlayer`. Pacchetti di sistema runtime: `vlc`, `libvlc5`, `vlc-plugin-base` (+ `libfontconfig1`, `libx11-6`, `libgl1`); da inserire nel packaging (S10).

### S2 — Spike web · Mac — ✅ completata 2026-10-07
- [x] Spike `net10.0-browserwasm` (`dotnet run -f net10.0-browserwasm` → `http://localhost:5000/`).
- [x] Audio con clic reale: MP3 e HLS E45 **ok in Chrome e Safari** (conferma utente: partenza immediata). Autoplay senza gesto bloccato (vedi §5). **Firefox non testato:** da fare in S9.
- [x] Pausa = chiusura dello stream e Resume = riapertura della diretta, implementate nello spike secondo la regola del §4.3.
- [x] API nowplaying dal browser: ok (6 stazioni, 558 ms). Sonda Icecast dal browser: ok (CORS riflesso).
- [x] SQLite su WASM: ok e persistente dopo il ricaricamento (righe da 1 a 2).
- [x] Peso Release: circa 13,5 MB Brotli, circa 47 MB non compressi.
- **Uscita:** fattibilità web confermata; D5 = SQLite anche sul web. La cartella `spikes/` resta (ignorata da git) come banco di prova; si può cancellare dopo la S7.
- Note operative: il dev server WASM usa `http://localhost:5000` (da `launchSettings.json`); dopo un `pkill` la porta 5000 sul Mac può rispondere 403, perché l'AirPlay Receiver di macOS usa la stessa porta. Se dà fastidio, cambiare porta nel profilo. Il primo clic sul canvas Uno a volte serve solo a dare il focus.

### S3 — Estrazione del Core (parte 1) · Mac — ✅ completata 2026-10-07
- [x] `RadioE45.Core` (`net10.0`, `Nullable`, `ImplicitUsings`, `RootNamespace=RadioE45`, `AssemblyName=RadioE45.Core`) aggiunto alla `.slnx`. `InternalsVisibleTo` per `RadioE45`, `RadioE45.Uno`, `RadioE45.Core.Tests` (servono a `ConsoleLoggerProvider` e `StationRateLimitedException`, che sono `internal`).
- [x] 67 file `.cs` spostati con `git mv` (namespace invariati): `Models/*`, `Services/IAzuraCast*Api.cs`, `Services/Radio/*` tranne `AzuraStationCatalog`, `Services/Audio` (prober, artwork, now-playing snapshot/tracker/result, interfacce e `Null*`), `Services/Data/*` tranne `DatabaseService`, `Services/Logging/*`, `LocalizationResourceManager`.
- [x] `.resx` spostati in `RadioE45.Core/Resources/Strings/`: il manifest resta `RadioE45.Resources.Strings.AppResources` e le satellite diventano `en|pl/RadioE45.Core.resources.dll`. Rimosso dal csproj MAUI il blocco `EmbeddedResource Update="Resources\Strings\*.resx"`.
- [x] SQLite nel Core **solo in compilazione** (`sqlite-net-pcl` con `PrivateAssets=all ExcludeAssets=runtime`). Verificato che il bundle Mac Catalyst usa ancora lo stesso `SQLite-net.dll` (SHA1 `3e960306…` identico alla baseline) e non contiene `e_sqlite3`/`SourceGear`.
- [x] Core **non** marcato `IsTrimmable`: con il flag sarebbero comparsi 18 warning IL2026 (Refit `RestService.For`, sqlite-net) e il linker Android/iOS in Release avrebbe potuto rimuovere codice che prima, nell'assembly dell'app, non veniva toccato.
- [x] `AddRadioE45Core()` (`RadioE45.Core/DependencyInjection`) registra HTTP client "AzuraCast", prober, artwork, servizi radio e podcast, repository. `MauiProgram` lo chiama e mantiene solo le registrazioni di piattaforma (audio, now playing, focus, `AzuraStationCatalog`, `DatabaseService`, ViewModel, pagine).
- [x] `RadioE45.Core.Tests` (xUnit v3 4.0.1, Microsoft.Testing.Platform): 4 test su `ScheduleFlattener` verdi. Creato `global.json` in root con `"test": { "runner": "Microsoft.Testing.Platform" }`; in S6 vi si aggiunge `msbuild-sdks` → `Uno.Sdk`. Comando: `dotnet test --project RadioE45.Core.Tests/RadioE45.Core.Tests.csproj`.
- [x] Build pulite dopo `dotnet clean`: maccatalyst, ios, android Debug con **0 warning e 0 errori** (come la baseline).
- [x] Rimandati a S4: `CrashReportingConfiguration` (dipende da `AppSecrets.cs`, file locale non versionato), `AzuraStationCatalog`, `DatabaseService`, `CrashDiagnostics`, `CrashReportingSettings` (usano API MAUI).
- [x] **Regressione manuale (utente):** ok (confermato il 2026-10-07).
- [x] Build Windows e build Release: confermate ok dall'utente insieme alla regressione.
- **Uscita:** app MAUI invariata. ✅

### S4a — Astrazioni e ViewModel nel Core · Mac — ✅ completata 2026-10-07 (regressione utente ok)
- [x] Interfacce in `RadioE45.Core/Services/Platform/` (namespace `RadioE45.Services.Platform`): `IUiDispatcher` (+`IUiTimer`), `INavigationService` (intenti: `GoToOnAirAsync`, `GoBackAsync`, `GoToAddStationAsync`, `GoToEditStationAsync(id)`, `GoToPodcastEpisodesAsync(id, title)`, `OpenFromMenuAsync(MenuPage)`, `CloseMenu`, `ResetPodcastTabToRoot`, `ShowScheduleAsync`), `IDialogService` (`AlertAsync`, `ConfirmAsync`, `ShowToastAsync`), `ISettingsStore`, `IAppEnvironment` (versione, build, commit, piattaforma, `UsesSystemVolume`), `IAppPaths`, `INetworkMonitor`, `IUrlLauncher`, `IThemeService`, `ICrashReportingService`.
- [x] Implementazioni MAUI in `RadioE45/Services/Platform/Maui*.cs`, registrate in `MauiProgram`. Le condizioni di piattaforma che prima stavano nei ViewModel (`#if ANDROID || IOS` per volume e Snackbar, `#if MACCATALYST` per Sentry) ora vivono **solo** nelle implementazioni MAUI (`MauiAppEnvironment.UsesSystemVolume`, `MauiDialogService.ShowToastAsync`, `MauiCrashReportingService`).
- [x] I 12 ViewModel spostati in `RadioE45.Core/ViewModels` (`git mv`) e rifattorizzati sulle interfacce; nessun `#if` di piattaforma rimasto (solo `#if DEBUG` per `IsDebugBuild`).
- [x] `[QueryProperty]` (tipo MAUI) tolto dai ViewModel e messo sulle **pagine** (`EditStationPage`: `id`; `PodcastEpisodesPage`: `podcastId`, `podcastTitle`), che inoltrano il valore al ViewModel. Stessa conversione e decodifica di prima.
- [x] Spostati nel Core anche `AzuraStationCatalog` (connettività via `INetworkMonitor`), `DatabaseService` (percorso via `IAppPaths`; `GetDatabasePath(appDataDirectory)` resta statico per l'allegato Sentry all'avvio), `IAudioService` e `IPodcastPlayerService` **senza** `Initialize(MediaElement)`.
- [x] MAUI: nuova interfaccia `IMediaElementHost.Initialize(MediaElement)` implementata da `AudioService`, `PodcastPlayerService` e `Media3AudioService` (Android: connette solo il MediaController). Le pagine fanno `(service as IMediaElementHost)?.Initialize(...)`.
- [x] Restano in MAUI di proposito: `ThemeService` (risorse colore MAUI), `CrashReportingSettings`, `CrashReportingConfiguration`/`AppSecrets`, `CrashDiagnostics` (usati all'avvio prima della DI), `AudioService`, `PodcastPlayerService`, `IosNowPlayingService`.
- [x] Verifica: `grep -rE "Microsoft\.Maui|CommunityToolkit\.Maui" RadioE45.Core` → nessun uso nel codice (solo commenti).
- [x] Test: 9 verdi (`ScheduleFlattener` 4 + `OnAirViewModel` volume/mute 5, con NSubstitute 6.2.0) per le regole che prima erano `#if ANDROID || IOS`.
- [x] Build Debug maccatalyst/ios/android: 0 warning, 0 errori.
- [x] **Regressione manuale (utente)**: ok. Punti verificati:
  - modifica stazione (parametro `id`) e apertura episodi podcast (titolo e id con caratteri speciali);
  - popup palinsesto; menu laterale: Canali, Impostazioni, link della stazione (sito, social, email/telefono);
  - eliminazione stazione con conferma; prompt di seed con lista vuota; cambio stazione mentre si è dentro gli episodi podcast (il tab torna alla lista);
  - volume e mute su Mac (valore ricordato al riavvio) e su telefono (sempre piena scala);
  - Impostazioni: tema, salvataggio (Snackbar su telefono), reset DB, test crash report, avviso di riavvio quando cambia il consenso;
  - barra di avanzamento del brano (timer UI), ritorno della rete con stazioni offline (ricarica del catalogo).
- [x] Build Windows: inclusa nella conferma utente.

### S4b — Motore audio live condiviso · Mac — ✅ completata 2026-10-14 (regressione utente ok)
- [x] `LiveStreamAudioService : IAudioService` nel Core (`RadioE45.Core/Services/Audio/`): è la logica di `AudioService` MAUI **spostata senza modifiche di comportamento**. Comprende ordine e fallback degli URL (`GetCandidateUrls`: ultimo URL funzionante → MP3/Icecast → fallback → HLS, oppure HLS per primo con `PreferHlsStream`), `IsHlsActive`, guardia di riconnessione, watchdog (10 s, buffering bloccato > 12 s), riconnessione al ritorno della rete (`INetworkMonitor`), su `MediaFailed` e su fine stream, regola **pausa live = chiusura stream** e Resume = riapertura, Stop/StopImmediate/Shutdown.
- [x] `IStreamPlayer` (Core): `HasSource`, `Open(url)`, `Close()`, `SetVolume`, `SetMetadata`, `ClearMetadata`, eventi `StateChanged` (enum `StreamPlayerState`), `Failed`, `Ended`. `AttachPlayer(player)` sostituisce l'`Initialize(MediaElement)`.
- [x] MAUI: `MediaElementStreamPlayer` (adattatore del `MediaElement`, contiene la guardia sui metadati per Windows e il reset di `ShouldAutoPlay`) e `AudioService : LiveStreamAudioService, IMediaElementHost`, ridotto a circa 30 righe (prima 515). `IUiDispatcher` ha ora anche `IsUiThread`.
- [x] Android invariato: `Media3AudioService` non usa il motore condiviso (la logica sta nel servizio Media3).
- [x] **Debounce degli stati instabili (spike S1): NON nel motore condiviso**, per non cambiare il comportamento MAUI. Va nell'adattatore Uno (`MediaPlayerStreamPlayer`, S7), che normalizza gli stati di libVLC/HTML5 prima di inoltrarli.
- [x] Test: 20 su `LiveStreamAudioService` con player e rete finti e dispatcher sincrono (ordine dei candidati, apertura, HLS attivo, URL tutti irraggiungibili, focus negato, pausa = chiusura con stazione mantenuta, Resume = riapertura, Stop, Resume dopo Stop, errore in riproduzione = riconnessione silenziosa, errore dopo pausa = segnalato, fine stream, ritorno rete, watchdog, stati, cambio player, volume). Totale progetto: **29 test verdi**.
- [x] Build Debug maccatalyst/ios/android: 0 warning, 0 errori.
- [x] **Regressione audio (utente)**: ok su tutti gli ambienti (confermato il 2026-10-14). Punti coperti: avvio con autoplay, play/pausa/ripresa (riparte in diretta), stop, cambio stazione (frecce e lista), volume/mute, stazione con HLS e preferenza HLS on/off (badge HLS), rete staccata e riattaccata, ritorno su OnAir dopo un podcast, metadati e controlli di sistema (lock screen iOS, Centro di controllo macOS, SMTC Windows).
- [x] **Crash Android (SIGSEGV nel loader assembly Mono, 2026-10-10):** diagnosticato il 2026-10-10 sulle satellite assembly `.resx` del Core (culture-tagged) e sul `NeutralResourcesLanguage` (neutro = italiano), poi corretto con il commit "Fix Neutral Language". Il crash risulta risolto secondo l'utente (2026-10-14).
- Dopo S4b, sullo stesso branch e senza legami con Uno: Terms and Conditions, qualifica dei namespace XAML spostati nel Core, `NeutralResourcesLanguage`, purpose string Local Network + `PrivateHostDetector` (host privati saltati per le stazioni pubbliche), retry della ricerca stazioni su host LAN durante l'alert Local Network. I test del Core sono ora **94** (erano 29).
- Nota: già prima, dopo `Shutdown()` il `CancellationTokenSource` di riconnessione viene eliminato e un successivo `AttachPlayer`+`PlayAsync` lo cancellerebbe da eliminato. Comportamento preservato tale e quale (Shutdown avviene solo alla chiusura dell'app). Da rivedere se l'app Uno riusasse il servizio dopo Shutdown.
- Per Uno (S7): il player podcast (`PodcastPlayerService`) resta MAUI; per Uno serve un'implementazione propria di `IPodcastPlayerService`, oppure si estrae anche quello sullo stesso schema.

### S5 — Sistema di parità · Mac (divisa in S5a + S5b)

Decisioni prese con l'utente (2026-10-14):
- **Versioning:** `.claude/` oggi è tutta in `.gitignore`. Si aggiungono eccezioni per `.claude/skills/` e `.claude/settings.json` (hook condivisi e versionati); `settings.local.json` resta privato.
- **Superficie VM cambiata senza modifica di `features.yaml` = errore (exit 2)**, non solo avviso (modifica al §6.3 punto 4).
- **Granularità di `features.yaml`: per sezione di pagina** (circa 40–60 voci).
- Fatti rilevati: il menu dell'app sta in `AppShell.xaml` (binding e `AppMenuViewModel`), quindi `pages.yaml` ha **10 voci** (9 viste + AppShell). I ViewModel usano `[ObservableProperty]`/`[RelayCommand]` (CommunityToolkit.Mvvm): la superficie si legge **con la reflection su `RadioE45.Core.dll` compilato**, non dai sorgenti. Finché `RadioE45.Uno` non esiste, una pagina Uno mancante è debito (exit 1) se le feature della pagina sono `todo`.

#### S5a — ParityCheck e manifest — ✅ completata 2026-10-14
- [x] `tools/ParityCheck` (`net10.0`, YamlDotNet, riferimento solo a `RadioE45.Core`) con i controlli 1–5 del §6.3, più l'integrità del manifest (id unici, `page`/`viewmodel` esistenti, stati validi, nota obbligatoria per `limited`/`n/a`). Opzioni `--strict`, `--json`, `--update-surface`. Aggiunto alla `.slnx`.
- [x] `parity/pages.yaml` (10 voci), `parity/features.yaml` dall'inventario della v0.44 (README, viste, `roadmap.md`): **60 feature**, `maui: done`, desktop e web `todo`; CarPlay e Android Auto `n/a`. Le limitazioni previste (stazioni personalizzate sul web per il CORS, niente autoplay sul web, Sentry su WASM) restano `todo` con la nota: `limited` vale come "fatto" e, finché la pagina Uno non esiste, ParityCheck lo segnala come errore.
- [x] `parity/vm-surface.txt` iniziale (156 righe) + test snapshot della superficie in `RadioE45.Core.Tests/Parity/` (riferimento al progetto `ParityCheck`).
- [x] Test di `ParityChecker` su un layout temporaneo (15 test): pagine mancanti, binding `{Binding}` ↔ `{x:Bind ViewModel.*}`, stringhe, `ignore`, code-behind, drift della superficie, stati e note, `--strict`. Totale progetto: **109 test verdi**.
- [x] `dotnet build RadioE45.slnx`: 0 avvisi, 0 errori (android, ios, maccatalyst, Core, test, ParityCheck). `ParityCheck` restituisce **1** (solo debito dichiarato); la DLL compilata gira in circa 0,4 s.
- Dettagli di implementazione da sapere:
  - Formato di `pages.yaml`: `page` (nome logico usato da `features.yaml`), `maui`, `uno` (percorso oppure `n/a` + `reason`), `viewmodels` (lista: AppShell ne usa due), `ignore` (lista di `name` + `reason`, vale per binding e chiavi di stringa).
  - Percorsi Uno previsti: `RadioE45.Uno/Views/<Pagina>.xaml`, con `ShellPage.xaml` per AppShell e `ScheduleDialog.xaml` per il palinsesto.
  - Oltre al XAML, ParityCheck confronta i membri usati nel code-behind (`_viewModel.X` in MAUI, `ViewModel.X` in Uno).
  - Pagina Uno mancante: debito se le feature della pagina non sono `done`/`limited` su desktop o web, altrimenti errore. Binding o stringhe mancanti: debito se la pagina ha almeno una feature `todo`, altrimenti errore.
  - Drift della superficie: errore se `git status` non mostra modifiche a `parity/features.yaml`, avviso ("lancia `--update-surface`") se le mostra.
  - Corretto anche il `.gitignore` di `main`, che conteneva marcatori di conflitto committati.
- **Uscita:** `ParityCheck` restituisce 1; build della soluzione e test verdi. ✅

#### S5b — Processo Claude Code — ✅ completata 2026-10-14
- [x] `CLAUDE.md` di progetto (§7.1) e `docs/parity/MAUI-TO-UNO.md` (§8, ampliata: convenzioni per ParityCheck, gesture, popup, converter, navigazione).
- [x] `.gitignore`: `.claude/*` con eccezioni `!.claude/settings.json` e `!.claude/skills/` (`settings.local.json` e i suoi backup restano privati); skill `.claude/skills/ui-parity-port/SKILL.md` (§7.2).
- [x] Hook in `.claude/settings.json` (§7.3): `PostToolUse` (`post-edit`) e `Stop` (`stop`) tramite `tools/parity-hook.sh`; servono `python3` e l'SDK .NET.
- [x] Test dell'hook (script con input JSON simulato): proprietà `ParityHookProbe` aggiunta a `ScheduleViewModel` → ricompilazione automatica, `decision: block` con il diff della superficie; con `stop_hook_active: true` → solo `systemMessage`; con `features.yaml` modificato → avviso, nessun blocco. Modifiche annullate, `ParityCheck` di nuovo a 1. Promemoria `post-edit` verificato su una vista MAUI (nessun output per altri file).
- Nota: gli hook di `.claude/settings.json` valgono dalle sessioni Claude Code successive (all'avvio può essere chiesto di approvarli). `ParityCheck` in CI resta per S10.
- **Uscita:** hook verificato, `ParityCheck` restituisce 1. ✅

### S6 — Scheletro `RadioE45.Uno` · Mac + VM (divisa in S6a + S6b)

Decisioni prese con l'utente (2026-10-14): tema **Fluent** (stile WinUI di default, personalizzato con i colori di RadioE45; niente Uno.Material/Toolkit); navigazione **a mano** (D6: `Frame` + `INavigationService`); branch `S6` da `S5B`.
Fatti rilevati: `Uno.Sdk` 6.7.30 è ancora l'ultima stabile (come negli spike). Lo spike usa la gestione centralizzata dei pacchetti, il repo no: il progetto Uno mette le versioni nel `.csproj`. L'interfaccia chiamata `IAppInfoProvider` nel §4.2 è `IAppEnvironment` nel Core (S4a).

#### S6a — Progetto, shell e pagine vuote · Mac — ✅ completata 2026-10-14
- [x] `global.json` con `msbuild-sdks` → `Uno.Sdk` 6.7.30 (le build MAUI restano verdi). `RadioE45.Uno` generato dal template `unoapp` (blank, Fluent, `-di`, `-nav blank`, `-mpe`) e adattato: `net10.0-desktop;net10.0-browserwasm`, `UnoFeatures` `SkiaRenderer; MediaPlayerElement; Hosting; Logging`; niente MVUX. Aggiunto alla `.slnx`.
- [x] **Niente `Directory.Build.props` né gestione centralizzata dei pacchetti alla radice** (toccherebbero MAUI): impostazioni e versioni nel `.csproj` Uno.
- [x] **`RootNamespace=RadioE45`** (assembly `RadioE45.Uno`): un namespace `RadioE45.Uno` oscurerebbe i namespace `Uno.*` del framework dentro il codice `RadioE45.*`. Pagine in `RadioE45.Views`, servizi in `RadioE45.Services.*`, come in MAUI.
- [x] Riferimento al Core, NBGV, runtime SQLite (`sqlite-net-pcl` 1.11.285 + `bundle_e_sqlite3` 3.0.4), `SkiaSharp.NativeAssets.Linux.NoDependencies` 3.119.2 solo su desktop. `System.Security.Cryptography.Pkcs`/`Xml` fissati a 10.0.13: la 10.0.12 portata da `Microsoft.Windows.Compatibility` (Uno) ha vulnerabilità note (NU1902/NU1903). `WASM0001` di SQLitePCLRaw silenziato (innocuo, §5). `LinkerConfig.xml` preserva anche `RadioE45.Core`.
- [x] Icona e splash dagli SVG MAUI, font OpenSans e Font Awesome (`Styles/Fonts.xaml`, Open Sans come font di default), colori in `Styles/Colors.xaml` con `ThemeDictionaries` Dark/Light e chiavi allineate a MAUI (`PageBackgroundBrush`, `PrimaryTextBrush`…). Tema scuro di default come MAUI. Manifest web "Radio E45".
- [x] `ShellPage.xaml` (`NavigationView` top + `Frame`), `UnoNavigationService` (tab, Back, On Air; gli intenti verso pagine non ancora portate fanno log e niente altro), pagine segnaposto OnAir, SongRequest, PodcastList. DI: `AddRadioE45Core()` + navigazione; `App.Services` per le pagine. Tolto `UseStudio()` del template (Hot Design senza dev server dell'IDE: errore in console e widget sopra il primo tab).
- [x] `TranslateExtension` WinUI anticipata da S8.1: `{loc:Translate Key=Tab_OnAir}`. **`Key=` è obbligatorio** (le markup extension WinUI non accettano argomenti posizionali); ParityCheck già accetta entrambe le forme. Valore letto al caricamento (nessun cambio lingua a runtime).
- [x] Porta del dev server web: **5045** (la 5000 è dell'AirPlay Receiver di macOS, §S2).
- [x] Verifica: `dotnet build RadioE45.slnx` 0 avvisi/0 errori (android, ios, maccatalyst, Core, test, ParityCheck, Uno desktop e browserwasm); 109 test verdi; app aperta su macOS desktop e in Chrome (screenshot), con tab e stringhe italiane del Core; cambio tab ok anche sul web; nessun errore in console. `ParityCheck` = 1: ora confronta davvero Shell, OnAir, SongRequest e PodcastList (binding e stringhe mancanti = debito dichiarato).
- Da sistemare in S8.1 (stili): l'indicatore della tab selezionata resta blu, l'override di `SystemAccentColor` non arriva alla `NavigationView`.
- **Uscita:** ✅

#### S6b — Servizi condivisi e Linux · Mac + VM — ✅ completata 2026-10-15
- [x] Implementazioni in `RadioE45.Uno/Services/Shared/`, registrate in `App`:
  - `UnoWindowHost`: la finestra unica, da cui gli altri servizi prendono `DispatcherQueue`, l'elemento radice (tema) e lo `XamlRoot` (dialog);
  - `UnoUiDispatcher` (`DispatcherQueue`; timer con lo stesso ciclo di vita di MAUI: `Stop()` stacca anche il tick);
  - `UnoDialogService` (`ContentDialog`, chiamate serializzate perché WinUI ne ammette uno alla volta; nessun toast, come MAUI su desktop);
  - `UnoSettingsStore` (`ApplicationData.LocalSettings`, con conversione dei tipi primitivi);
  - `UnoAppEnvironment` (versione NBGV `0.45`, build = altezza git, commit, piattaforma `Linux`/`macOS`/`Windows`/`Web`, volume gestito dall'app);
  - `UnoUrlLauncher` (`Launcher.LaunchUriAsync`), `UnoThemeService` (`RequestedTheme` sulla radice).
- [x] Tema: tolto `RequestedTheme="Dark"` da `App.xaml`, così "System" segue davvero il sistema operativo o il browser; all'avvio si applica `theme_preference` (default `Dark`, come MAUI).
- [x] `Uno.Extensions.Toolkit` (importato in automatico da Uno.Sdk) ha un suo `IThemeService`: alias globale in `GlobalUsings.cs`.
- [x] `ApplicationPublisher` = `RadioE45` (il template metteva `CN=RadioE45`, che serve solo ai pacchetti MSIX e finiva nel percorso dei dati): dati desktop in `~/.local/share/RadioE45/com.radioe45.app/` (`LocalState`, `Settings`).
- [x] Pagina **Debug** (solo DEBUG, voce in fondo alla `NavigationView`): info ambiente, contatore persistente, tema, alert, conferma, apertura link, dispatcher da thread di fondo, timer. Non è nel manifest di parità (nessuna controparte MAUI).
- [x] Autotest DEBUG solo desktop: `RADIOE45_SELFTEST=1 dotnet run --project RadioE45.Uno -f net10.0-desktop` scrive una riga `SELFTEST` per controllo (ambiente, impostazioni persistenti tra due avvii, double, remove, dispatcher, `InvokeAsync` da thread di fondo, timer, tema, navigazione) ed esce. Costante `RADIOE45_DESKTOP` definita nel csproj (Uno non ne ha una che distingua desktop e web; `Application.Exit()` sul web non è implementato, Uno0001).
- [x] Esiti:
  - **macOS desktop:** autotest 9/9 in due avvii consecutivi (contatore 1 → 2);
  - **VM Ubuntu arm64 (X11 via XWayland):** build ok, autotest 9/9 in due avvii, poi di nuovo dopo il cambio di `ApplicationPublisher`; `uno-check --target linux` ora tutto verde (`unosdk` compreso);
  - **Web (Chrome):** pagina Debug ok: `ConfirmAsync` → True/False, link in una nuova scheda, tema Light applicato e mantenuto dopo il ricaricamento, contatore 1 → 2 (localStorage), timer ok. Il dispatcher da "thread di fondo" risulta UI thread: atteso, WASM è single-thread.
- [x] **Verifica manuale dell'utente su desktop (2026-10-15):** pagina Debug → Alert, Conferma, Apri link, cambio tema: tutto ok. Su Linux, avviando da terminale, Chrome lanciato da `xdg-open` scrive nel terminale dell'app `Opening in existing browser session` e due righe `zygote ... Broken pipe (32)`: rumore di Chrome (il processo passa l'URL alla sessione già aperta ed esce), innocuo; non compare con l'app avviata dal menu.
- Nota: l'ultima modifica (spostamento dell'autotest sotto `#if DEBUG && RADIOE45_DESKTOP`) è stata riprovata solo su macOS: la VM era irraggiungibile (timeout SSH) a fine sessione.
- **Uscita:** l'app si apre su macOS, in Chrome e nella VM; `dotnet build RadioE45.slnx` 0 avvisi/0 errori; 109 test verdi; `ParityCheck` = 1. ✅

### S7 — Servizi di piattaforma Uno · Mac + VM
- [ ] Audio: player Uno per radio e podcast (decisione S1).
- [ ] Desktop: `XdgAppPaths`, `LinuxNetworkMonitor`, SQLite.
- [ ] Web: storage (decisione S2), `BrowserNetworkMonitor`, `IStreamUrlProber` senza sonda.
- [ ] Sentry base sul desktop.
- [ ] Riconnessione: cadute di rete lunghe (2–5 min) nella VM, con il coordinatore e `INetworkMonitor`; verificare che l'audio riparta da solo e che la UI non resti su "in riproduzione" durante lo stallo.
- [ ] Snap/CI x64: verificare se serve ancora il fix SkiaSharp `NoDependencies` (§S1).
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
| D1 | Nome del progetto Uno | S1 | ✅ `RadioE45.Uno` |
| D2 | Posizione dei progetti | S1 | ✅ Accanto a `RadioE45/` |
| D3 | Coordinatore audio condiviso o duplicato | S1 | ✅ Condiviso |
| D4 | Player desktop: Uno `MediaPlayer` o `LibVLCSharp` | Fine S1 | ✅ Uno `MediaPlayer` |
| D5 | Storage web: SQLite WASM o repository dedicati | Fine S2 | ✅ SQLite (verificato) |
| D6 | Navigazione: `Frame` scritto a mano o `Uno.Extensions.Navigation` | S6 | ✅ A mano (confermato 2026-10-14); tema Fluent |
| D7 | Canali desktop: Snap Store, Flathub, AppImage | S10 | Snap + AppImage, Flathub dopo |
| D8 | Hosting e dominio web | S10 | Da decidere con l'utente |
| D9 | Hook Stop bloccante o solo avviso | S5 | ✅ Bloccante solo per exit 2; drift della superficie VM non tracciato = exit 2 |
| D10 | Rilascio delle app Uno legato alle versioni MAUI (stesso `version.json`) | S10 | Sì, stessa versione |

---

## 14. Registro delle sessioni

| Data | Sessione | Esito | Note |
|---|---|---|---|
| 2026-10-15 | S6b | Completata (prova manuale utente ok) | Servizi condivisi Uno (dispatcher, dialog, impostazioni, ambiente, link, tema) + `UnoWindowHost`; pagina Debug e autotest `RADIOE45_SELFTEST=1`; 9/9 su macOS e VM, pagina Debug ok sul web; dati in `~/.local/share/RadioE45/`; `uno-check` verde; soluzione verde, 109 test, ParityCheck = 1. Prossima: S7 |
| 2026-10-14 | S6a | Completata | Branch `S6` da `S5B`. `RadioE45.Uno` (Uno.Sdk 6.7.30, Fluent, `RootNamespace=RadioE45`), shell `NavigationView` + 3 pagine segnaposto, `TranslateExtension` (`Key=`), colori/font/icona; desktop macOS e web Chrome verificati; soluzione verde, 109 test, ParityCheck = 1. Prossima: S6b (servizi condivisi + VM) |
| 2026-10-14 | S5b | Completata | Branch `S5B` da `S5A`. `CLAUDE.md`, `docs/parity/MAUI-TO-UNO.md`, skill `ui-parity-port`, hook `PostToolUse`/`Stop` versionati (`.claude/settings.json` + `tools/parity-hook.sh`); blocco con exit 2 verificato e annullato; ParityCheck = 1. Prossima: S6 |
| 2026-10-14 | S5a | Completata | Branch `S5A` da `main`. `tools/ParityCheck` (controlli §6.3 + integrità del manifest + code-behind), `parity/pages.yaml` (10), `features.yaml` (60 feature), `vm-surface.txt`; 15 nuovi test (109 totali); soluzione verde; ParityCheck = 1. Prossima: S5b |
| 2026-10-14 | S4b | Completata (regressione utente ok) | Regressione audio ok su tutti gli ambienti; crash Android (loader assembly Mono) corretto. Commit extra sul branch `S4B`: Terms, namespace XAML, Neutral Language, Local Network/`PrivateHostDetector`. 94 test verdi, Core senza riferimenti MAUI. Prossima: S5 |
| 2026-10-08 | S4b | Codice completato | `LiveStreamAudioService` + `IStreamPlayer` nel Core (logica `AudioService` invariata), adattatore `MediaElementStreamPlayer`, 20 nuovi test (29 totali), build MAUI pulite; attesa regressione audio |
| 2026-10-07 | S4a | Completata (regressione utente ok) | 10 interfacce di piattaforma + implementazioni MAUI, 12 ViewModel + catalog, DB e interfacce audio nel Core; `QueryProperty` sulle pagine; `IMediaElementHost`; 9 test verdi; build MAUI pulite; attesa regressione |
| 2026-10-07 | S3 | Completata (regressione utente ok) | Core + Tests nella soluzione, 67 file e 3 resx spostati, `AddRadioE45Core()`, 4 test verdi, build MAUI pulite (maccatalyst/ios/android); attesa regressione manuale e build Windows |
| 2026-10-07 | S2 | Completata | Web: audio MP3/HLS ok in Chrome e Safari con clic reale, autoplay bloccato; CORS API e Icecast ok; SQLite WASM persistente; circa 13,5 MB Brotli; regola pausa live = stop documentata |
| 2026-10-07 | S1 | Completata | Decisioni D1–D4 chiuse; spike audio/SQLite/HTTP ok su VM arm64 e macOS; fix SkiaSharp NoDependencies; note di comportamento del player in §4.3 |
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
