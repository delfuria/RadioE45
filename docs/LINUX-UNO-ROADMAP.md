# RadioE45 su Linux e Web con Uno Platform — Piano operativo

> Documento operativo, da riprendere a ogni sessione Claude Code.
> Branch di lavoro attuale: `S8.6` (da `S8.5`, non ancora unita a `main`); da S8 un branch per ogni punto. Ultimo aggiornamento: 2026-10-16 (app v0.45).
> Stato: **S0–S8.1 completate (regressione MAUI ok); S8.2 codice completato (prove manuali dell'utente); S8.3 completata; S8.4, S8.5 e S8.6 codice completato (S8.4–S8.6 non ancora committate); prossima sessione: S8.7** (§12). Aggiornare le checkbox e il registro (§14) alla fine di ogni sessione.

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
ssh radioe45-vm 'pkill -f "[n]et10.0-desktop/RadioE45.Uno"'   # chiusura (le [] evitano che pkill chiuda anche la shell SSH, la cui riga di comando contiene lo stesso testo)
```

Dalla VM (anche per Rider): `~/RadioE45/tools/vm-sync.sh` (`--dry-run` per vedere le differenze) fa lo stesso rsync in locale dalla cartella condivisa `/media/psf/Lavori/RadioE45`. In Rider aprire **`~/RadioE45/RadioE45.slnx`**, mai la cartella condivisa (anche `~/Desktop/Parallels Shared Folders` punta lì): le sue `obj/` sono del Mac e la build fallisce con `NETSDK1064`.

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
| Immagini remote | **Verificato in S8.1:** Uno disegna le immagini con Skia, quindi il browser le carica con CORS. Le copertine `/api/station/<id>/art/<id>.jpg` hanno `Access-Control-Allow-Origin: *`; la copertina generica (brani senza copertina, es. i jingle E45) fa redirect a `/static/img/generic_song.jpg`, che **non** ha l'header → sul web resta il segnaposto (sul desktop e in MAUI si vede il logo) | **Limite accettato** (utente, 2026-10-15). Si risolverebbe solo lato server aggiungendo l'header CORS a `/static/`. Segnaposto sempre sotto le immagini remote. Nota in `onair.artwork`. |
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
| `Shell` + `TabBar` (OnAir, SongRequest, Podcast) | `NavigationView` con tab in alto (desktop a finestra fissa e colonna web, nessun layout adattivo) + `Frame`, tramite `INavigationService` |
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

### S7 — Servizi di piattaforma Uno · Mac + VM (divisa in S7a + S7b)

Decisioni prese con l'utente (2026-10-15):
- **Finestra desktop come MAUI:** 500×855 (verticale) o 900×450 (orizzontale) da `desktop_orientation`, non ridimensionabile (`RadioE45/App.xaml.cs`). Fatta in S7 (non in S8) perché i port delle pagine vanno progettati sulla stessa finestra di MAUI. Nuova feature `shell.desktop-window-size` (web `n/a`).
- **Player podcast estratto nel Core** come in S4b (motore condiviso + adattatori MAUI e Uno): richiede una regressione podcast su MAUI.
- **Sentry desktop rimandato** a S10 (insieme al web): `ICrashReportingService` non disponibile su Uno (`IsAvailable = false`).
- Branch `S7` da `S6`.

#### S7a — Finestra, percorsi, rete, database, audio live · Mac + VM — ✅ completata 2026-10-15
- [x] **Finestra desktop come MAUI.** Dimensioni spostate nel Core (`Services/Platform/DesktopWindowSize`: 500×855 / 900×450 da `desktop_orientation`), usate da MAUI (`App.CreateWindow`, stesso comportamento) e da Uno (`App.ApplyDesktopWindowSize`).
  - `AppWindow.Resize` vuole **pixel fisici dell'area client**: la dimensione si moltiplica per `XamlRoot.RasterizationScale`, noto solo dopo il caricamento del contenuto.
  - Risultato: macOS Retina 500×855 punti + 28 di barra del titolo; VM (scala 1) client 500×855 + decorazioni GNOME.
  - Presenter non ridimensionabile e non massimizzabile. Feature `shell.desktop-window-size` (desktop `done`, web `n/a`).
- [x] `UnoAppPaths`: `LocalFolder/data`, inizializzato in modo asincrono in `App.OnLaunched` **prima** di creare la shell (sul web attende IDBFS). Percorsi: macOS `~/Library/Application Support/RadioE45/com.radioe45.app/LocalState/data`, Linux `~/.local/share/RadioE45/com.radioe45.app/LocalState/data`, web `/local/data`.
- [x] `INetworkMonitor`:
  - desktop: `DesktopNetworkMonitor`, su `NetworkChange` (disponibilità e cambi di indirizzo, evento solo quando il valore cambia);
  - web: `BrowserNetworkMonitor`, `navigator.onLine` + eventi `online`/`offline` via `JSImport` (`WasmScripts/RadioE45Network.js`).
  - Costanti `RADIOE45_DESKTOP`/`RADIOE45_BROWSER` nel csproj; `AllowUnsafeBlocks` sul web (lo richiede il generatore `JSImport`).
  - Gli script extra in `WasmScripts` vanno dichiarati `EmbeddedResource`: il template collega solo `AppManifest.js`.
- [x] DI come `MauiProgram`: `IDatabaseService`, `IAzuraStationCatalog`, `ICrashReportingService` non disponibile (`UnavailableCrashReportingService`), `IPlatformNowPlayingService`/`IAudioFocusManager` nulli, ViewModel con gli stessi cicli di vita (tranne `PodcastEpisodesViewModel`, che aspetta il player podcast di S7b). `ScheduleViewModel` registrato transient.
- [x] Audio live:
  - `MediaPlayerStreamPlayer` (Uno `MediaPlayer` senza elemento visuale) + `UnoAudioService : LiveStreamAudioService`;
  - debounce degli stati in `RadioE45.Core/Services/Audio/StreamStateDebouncer` (400 ms, `TimeProvider`), 5 test con `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing` 10.10.0);
  - Close = `Pause()` + `Source = null` + stato `Stopped` immediato; `Position` non usata;
  - sul web, togliere la sorgente fa scattare un `MediaFailed` senza messaggio dell'elemento `<audio>`: ignorato quando la sorgente è già `null`.
- [x] Pagina Debug: sezione S7a (percorso dati, stato della rete, "Carica stazioni" = seed + catalogo, Play/Pausa/Riprendi/Stop, volume). Autotest desktop esteso: percorsi, rete, DB + catalogo, play, 5 s di riproduzione, pausa (stazione mantenuta), ripresa, stop. Il controllo del timer ora aspetta 3 tick entro 2 s: con una finestra fissa di 550 ms falliva al primo avvio a freddo nella VM.
- [x] Esiti:
  - **macOS:** autotest 17/17, in riproduzione in 0,7 s;
  - **VM Ubuntu arm64:** 17/17, in riproduzione in 1,1–1,5 s;
  - **Chrome:** catalogo caricato (SQLite su IDBFS + HTTP), Play → `playing=True`, Pausa (stazione mantenuta, nessun errore), Riprendi, Stop. Il primo clic su un pulsante dopo un altro controllo a volte va solo al focus del canvas (come in S2).
- [x] `dotnet build RadioE45.slnx` 0 avvisi/0 errori (MAUI compreso); **121 test** verdi (compresi 7 su `DesktopWindowSize`); `ParityCheck` = 1.
- Da fare con l'utente: **ascolto reale** su macOS, nella VM e nel browser (gli autotest controllano lo stato del player, non l'audio). La prova del `NetworkMonitor` con la rete staccata è in S7b, insieme alle cadute lunghe.
- **Uscita:** ✅ (salvo conferma d'ascolto dell'utente)

#### S7b — Podcast, test di rete lunghi · Mac + VM — ✅ completata 2026-10-15 (regressione utente ok)
- [x] **Motore podcast condiviso** `RadioE45.Core/Services/Audio/PodcastPlaybackService` (logica di `PodcastPlayerService` MAUI spostata senza modifiche di comportamento: retry all'avvio, ripresa silenziata con seek sul primo campione reale di posizione, guardia sull'episodio corrente, salvataggio ogni 5 s / pausa / stop / fine = completato) + interfaccia `IPodcastMediaPlayer`. `TimeProvider` per i ritardi, `IUiDispatcher` al posto di `MainThread`. **14 test** con player e tempo finti.
- [x] MAUI: `MediaElementPodcastPlayer` (adattatore) e `PodcastPlayerService : PodcastPlaybackService, IMediaElementHost`, ridotto a un guscio (prima 285 righe). Pagina e ViewModel invariati.
- [x] Uno: `MediaPlayerPodcastPlayer` + `UnoPodcastPlayerService`; `PodcastEpisodesViewModel` registrato. La posizione viene letta ogni 500 ms (il `MediaPlayer` Uno non ha un evento di posizione), gli stati passano dallo `StreamStateDebouncer`.
- [x] **Comportamenti di Uno 6.7 aggirati** (documentati nell'adattatore):
  1. macOS: riaprire un file sullo stesso `MediaPlayer` dopo `Source = null` si ferma in Paused subito dopo Playing → su desktop un `MediaPlayer` nuovo per ogni episodio.
  2. macOS: `Dispose()` del `MediaPlayer` fa crashare l'app (`NullReferenceException` in `MacOSMediaPlayerExtension.OnPeriodicPositionUpdate`) → i player rilasciati vengono fermati e tenuti in memoria, mai `Dispose`.
  3. macOS: subito dopo l'apertura il seek può essere ignorato → seek verificato con retry (fino a ~2 s).
  4. Web: player multipli si disturbano (`AbortError: play() interrupted by a call to pause()` / `by a new load request`) e il primo `play()` subito dopo la creazione del player si perde (l'elemento HTML nasce in modo asincrono) → sul web un solo player podcast, creato all'avvio. Anche il `Close()` della radio ora non tocca nulla se la sorgente è già chiusa.
  5. Linux (libVLC): a fine episodio il backend ricarica la sorgente come percorso locale (`file:///<cartella>/https:\\host\…`) e genera un `MediaFailed`: ignorato fino alla successiva apertura (resta solo un messaggio di libVLC nel terminale).
- [x] **Radio senza rete (Linux):** libVLC non segnala la caduta (S1). L'adattatore `MediaPlayerStreamPlayer` ora riceve `INetworkMonitor` e alla perdita della rete segnala Buffering, così la UI non resta su "in riproduzione". Test nella VM con `prlctl set … --device-set net0 --disconnect`:
  - 3 minuti senza la correzione: `playing=True` per tutto lo stallo (requisito non rispettato);
  - 2 minuti con la correzione: subito `buffering=True`, il watchdog ritenta ogni 10 s in silenzio, al ritorno della rete radio di nuovo in riproduzione in circa 2 s.
  - Modalità di prova: `RADIOE45_SELFTEST=network RADIOE45_SELFTEST_MINUTES=n` (stato ogni 5 s, righe `NETTEST`).
- [x] Autotest desktop esteso ai podcast (episodio demo AzuraTest): radio → podcast (la radio si ferma), avanzamento, pausa vera, seek + ripresa, posizione salvata, ripartenza dalla posizione salvata, fine episodio = completato. **macOS e VM: tutto ok** (due esecuzioni consecutive ciascuno). Pagina Debug: sezione podcast ("Trova episodio" separato da "Play" perché sul web il play deve seguire il clic entro pochi secondi).
- [x] **Bug preesistente (MAUI, quindi anche Uno) corretto il 2026-10-15:** ascolto di un episodio → In Onda → ritorno alla pagina episodi: il play non ripartiva e il mini player mostrava 0, anche se la posizione era salvata bene nel DB (verificato: 11 s); con freccia indietro + nuova apertura funzionava. In iOS crash.
  - **Causa:** tornando su In Onda, `OnAirPage.OnAppearing` → `RestartAsync` → `StartPlayAndNowPollingAsync` ferma l'episodio **direttamente sul servizio podcast**. Il ViewModel degli episodi (stessa istanza, la pagina resta nello stack della tab) non lo sapeva: `CurrentEpisode` restava valorizzato, quindi il tocco sull'episodio andava nel ramo "stesso episodio → pausa/ripresa" e chiamava `ResumeAsync` su un player chiuso (nulla; probabile causa del crash iOS). Il player emetteva poi un campione 0 dopo lo stop, mostrato dal mini player. Shell non garantisce `OnDisappearing`/`OnAppearing` per una pagina impilata sopra la radice della tab, quindi il ViewModel non può affidarsi al ciclo di vita della pagina.
  - **Correzione (Core, vale anche per Uno):**
    - `IPodcastPlayerService` ha gli eventi `EpisodeStopped` (episodio fermato da chiunque) e `ProgressSaved` (dopo ogni salvataggio);
    - il servizio ignora i campioni di posizione senza episodio caricato e scarica l'episodio prima di chiudere il player;
    - il ViewModel decide "stesso episodio" solo se il player lo ha davvero caricato (altrimenti riapre dalla posizione salvata, anche dal pulsante del mini player), azzera il mini player su `EpisodeStopped`, aggiorna la card da `ProgressSaved`, e `Activate()` riallinea lo stato;
    - `AzuraCastPodcastEpisode.ResumePositionSeconds` è osservabile.
    - Restano anche le correzioni precedenti (`Activate`/`Initialize` a ogni `OnAppearing`, salvataggio dell'ultima posizione nota se il player riporta 0).
  - **Test:** `PodcastEpisodesViewModelTabSwitchTests` riproduce lo scenario con servizio reale e player finto, nelle due varianti (con e senza ciclo di vita della pagina): falliva prima della correzione, ora passa. Totale **141** test.
  - **Da verificare dall'utente** su Mac, iOS (crash), Windows e Android.
- [ ] **Web: podcast da provare a mano (utente)**, con la scheda visibile: nella sessione la finestra di automazione era in background e Chrome rimanda il caricamento dei media nelle schede nascoste (`visibilityState: hidden`), quindi le prove automatiche non sono conclusive.
- Note:
  - Test: **141** verdi.
  - Il server demo `demo.azuracast.com` (unico con podcast nel catalogo) ha risposto più volte 521 durante la sessione: un "nessun episodio" può dipendere da quello.
  - `dotnet build RadioE45.slnx` 0 avvisi/0 errori; **135 test** verdi; `ParityCheck` = 1.
- [x] **Regressione MAUI (utente), un solo giro a fine S7b:** ok (confermato il 2026-10-15, insieme a quella di S8.1).
  - podcast su tutti gli ambienti (ascolto, pausa, ripresa dalla posizione salvata, fine episodio);
  - finestra 500×855 non ridimensionabile su Mac Catalyst e Windows (codice spostato in `DesktopWindowSize` in S7a; 7 test sui valori);
  - build della soluzione sul PC Windows (`global.json` con `Uno.Sdk` e `RadioE45.Uno` nella `.slnx` da S6a).
  - iOS e Android: nessuna modifica che agisca su quelle piattaforme oltre ai podcast.
- [ ] Snap/CI x64: la verifica del fix SkiaSharp `NoDependencies` si sposta in S10 (serve la CI x64).
- **Uscita:** podcast su desktop e web dalla pagina Debug; regressione MAUI ok.

### S8.x — Porting delle pagine (una o due pagine per sessione, con la skill `ui-parity-port`) · Mac (+ VM per le verifiche)
- [x] S8.1 Stili, `TranslateExtension`, `OnAirPage` (incluso `VuMeterView` Skia) — ✅ 2026-10-15, branch `S8.1` da `main`
  - Decisioni con l'utente: logica di `OnAppearing` nel ViewModel; icone Font Awesome in Uno se i simboli MAUI non si vedono (sul web non si vedono); pulsante menu e palinsesto **non mostrati** finché non arrivano S8.3 e S8.4 (`onair.menu-button`, `onair.schedule-entry` restano `todo`).
  - **Core:** `OnAirViewModel.ActivateAsync()` (volume, stazione predefinita, ripresa della stazione fermata da un podcast), usata da MAUI (`OnAppearing`, code-behind ridotto) e Uno (`OnNavigatedTo`). `IAppEnvironment.CanAutoplay` (default `true`, Uno `false` sul web): sul web all'apertura stazione selezionata + now playing, niente play né ripresa automatica (§5). VU meter: simulazione dei livelli spostata in `RadioE45.Core/Visuals/VuMeterLevels` (MAUI `VuMeterView` invariato nell'aspetto). 6 test su `ActivateAsync`/autoplay.
  - **Uno:** `Styles/Styles.xaml` (stesse chiavi MAUI), `Converters/Fn.cs` (funzioni `x:Bind`), `Controls/VuMeterView` (`SKCanvasElement`), `Assets/Images` (cuori e segnaposto da SVG), `OnAirPage` completa (layout verticale/orizzontale come MAUI, slider volume, F5 = aggiorna). Accento Fluent rosso anche nelle varianti `SystemAccentColorLight/Dark1-3` (prima slider e sottolineatura delle tab erano blu).
  - Scoperte: sul web niente font per emoji/simboli (quadratini) → Font Awesome; entità XML nelle stringhe di `x:Bind` non decodificate; la copertina generica di AzuraCast (`/static/img/generic_song.jpg`, redirect) non ha CORS → segnaposto sempre sotto le immagini. Dettagli in `docs/parity/MAUI-TO-UNO.md`.
  - **ParityCheck:** ora legge i percorsi passati alle funzioni di `x:Bind` (1 test). `ignore` di OnAir: `Tab_OnAir` (titolo della pagina, su Uno è nella shell), `IsBusy` (indicatore del `RefreshContainer`).
  - Verifiche: macOS (tema scuro), VM Ubuntu (tema chiaro, simboli ok), Chrome (layout largo a due colonne, icone, storico espandibile, nessun autoplay). Soluzione 0 avvisi/0 errori, **148 test**, autotest desktop 0 errori, `ParityCheck` = 1.
  - **Web in colonna** (richiesta dell'utente dopo la prova): nel browser largo la pagina passava al layout orizzontale a due colonne, poco gradevole. Ora sul web tutta l'app (tab comprese) è una colonna centrata larga al massimo 500 (`DesktopWindowSize.PortraitWidth`) e le pagine restano sempre in verticale (`Services/Shared/UnoLayout`). Nuova feature `shell.web-column` (MAUI e desktop `n/a`). Le prossime pagine usano `UnoLayout.IsSingleColumn` per gli eventuali layout orizzontali.
  - **Limite accettato:** sul web la copertina generica di AzuraCast (jingle) non si vede per mancanza di CORS sul server (§5).
  - [x] **Regressione MAUI (utente)** su In Onda (primo avvio, ritorno da Podcast con ripresa della radio, volume, VU meter, cambio stazione) su tutti gli ambienti: ok (2026-10-15).
  - Da fare con l'utente: ascolto web reale (Play dopo l'apertura); F5 su desktop (non provato in automatico).
- [x] S8.2 `RadioListPage`, `AddStationPage`, `EditStationPage` — ✅ codice 2026-10-15, branch `S8.2` da `S8.1`
  - **Navigazione Uno:** le tre pagine sono impilate nel `Frame` della shell sopra In Onda (come le route MAUI `//OnAirPage/RadioListPage`), con il pulsante indietro della `NavigationView`; un clic su una tab torna sempre alla sua pagina radice (`ItemInvoked`). `UnoNavigationService`: `GoToAddStationAsync`, `GoToEditStationAsync` (id come parametro di `Frame.Navigate`), `OpenFromMenuAsync(Channels)`; `Settings` resta per S8.3. Avvio senza stazioni → Canali, come `AppShell.OnNavigated` (i Termini d'uso arrivano con il menu, S8.3). Finché non c'è il menu, Canali si apre dal pulsante "Canali" della pagina Debug.
  - **Canali:** modifica/elimina dal menu contestuale (tasto destro, pressione lunga): lo `SwipeView` MAUI è solo touch (`Common_Delete` in `ignore`). Riordino trascinando dalla maniglia ☰ (la riga è un pulsante e cattura il puntatore), rilascio su una riga o sotto la lista come in MAUI. Logo con segnaposto sempre sotto (sul web il logo di Radio E45 non si carica). F5 = ricarica. Icone Font Awesome.
  - **Aggiungi:** Invio nella casella URL = cerca. **Modifica:** slider del ritardo con `ValueChanged` (int ↔ double non convertibile da `x:Bind` TwoWay).
  - **Correzioni della shell:** sul web la colonna prendeva la larghezza di ogni pagina (`HorizontalAlignment=Center`): ora `Stretch` + `MaxWidth`, larghezza fissa. Tooltip "F5" nascosto (`KeyboardAcceleratorPlacementMode="Hidden"`) anche su In Onda.
  - Verifiche: Chrome (Canali, menu contestuale, Modifica con ritardo, Annulla, Aggiungi con ricerca su `demo.azuracast.com` e Invio, selezione, pulsante indietro, tocco su una stazione → In Onda); autotest desktop 0 errori; build desktop e web 0 avvisi/0 errori; `ParityCheck` = 1. Core non modificato.
  - Da fare con l'utente: **riordino a trascinamento** su macOS, nella VM e nel browser (il drag sintetico dell'automazione non arriva al rilascio); salvataggio reale di aggiunta, modifica ed eliminazione; avvio con database vuoto (proposta delle stazioni di prova).
  - Nota (anche MAUI, non corretta): cercando `https://demo.azuracast.com` la stazione AzuraTest già presente non risulta "già aggiunta", perché il seed salva l'URL senza schema e il confronto è esatto.
- [x] S8.3 `SettingsPage`, menu app — ✅ codice 2026-10-15, branch `S8.3` da `S8.2`
  - **Menu laterale:** nella shell, pannello in sovrimpressione (280, come `FlyoutWidth` MAUI) sopra la `NavigationView`, con animazione propria (`TranslateTransform`) e strato scuro che chiude al clic; `ShellPage.ViewModel` = `AppMenuViewModel` (intestazione con logo e nome della stazione, Canali, Impostazioni, sito, social, contatti, versione). Si apre dal pulsante ☰ (`Controls/MenuButton`, stile `MenuButton`) sulla copertina di In Onda; si chiude con clic fuori, Esc o scelta di una voce (`UnoNavigationService.OpenMenu`/`CloseMenu`). Sul web il pannello sta nella colonna da 500; la shell ha un `Clip` sui propri bordi, altrimenti l'animazione di apertura mostrava il pannello a sinistra, fuori dalla colonna. All'inizio era una `SplitView` `Overlay`, ma su Uno il pannello compariva aperto per un frame prima dell'animazione (entrambi segnalati dall'utente sul web). I pulsanti dei link nei template passano lo `StationLink` in `Tag` (`x:Bind` nei template non vede la pagina). Tolto il pulsante provvisorio "Canali" dalla pagina Debug.
  - **Termini d'uso:** `ShellPage.OnLoaded` chiama `ITermsService.EnsureAcceptedAsync()` prima del controllo delle stazioni, come `AppShell.OnNavigated` (verificato su macOS e Chrome).
  - **Impostazioni:** pagina impilata sopra In Onda (`OpenFromMenuAsync(Settings)`); slider int con `ValueChanged`, tema con `ComboBox` + `SelectionChanged` (cambio live verificato), Diagnostica nascosta fino al doppio tocco sul titolo come in MAUI. Crash report: interfaccia portata ma nascosta (`UnavailableCrashReportingService`), `settings.diagnostics.crash-reports` resta `todo`.
  - Verifiche: Chrome (termini, menu, Impostazioni, checkbox, Diagnostica, tema chiaro/scuro, Termini dalle Info, Salva → In Onda, menu → Canali); macOS (termini al primo avvio, ☰); autotest desktop 0 errori; build desktop e web 0 avvisi/0 errori; `ParityCheck` = 1. Core non modificato.
  - [x] **Prova dell'utente:** ok (2026-10-15), dopo le due correzioni dell'animazione del menu sul web.
  - Nota (anche MAUI, non corretta): il tema è salvato in due posti (`AppSettings.ThemePreference` nel DB e `theme_preference` in `ISettingsStore`); se divergono la pagina mostra il valore del DB e l'app quello dello store (nel browser di prova: "Dark" nella casella, tema chiaro all'avvio).
- [x] S8.4 `SchedulePopup` — ✅ codice 2026-10-16, branch `S8.4` da `S8.3`
  - **Pannello dal basso:** `Views/ScheduleDialog` (`UserControl`, `ViewModel` = `ScheduleViewModel` transient, nuovo a ogni apertura come il popup MAUI) ospitato da `ShellPage.ShowSheet` in un pannello che sale dal basso sopra uno strato scuro, con la stessa tecnica del menu laterale (animazione `TranslateTransform`, niente primo frame aperto, `Clip` della shell, colonna web). Altezza massima 70% della shell, poi la lista scorre. Chiusura con ✕, clic fuori, Esc o trascinamento verso il basso della maniglia/titolo (`ManipulationCompleted`, MAUI swipe giù). Non `ContentDialog`: sarebbe centrato e, sul web, fuori dalla colonna. `UnoNavigationService.ShowScheduleAsync` apre il pannello (`CloseSchedule` per l'autotest).
  - **In Onda:** pulsante palinsesto (stile `ScheduleButton`) accanto al prossimo brano se `HasSchedule`, riga compatta se manca il prossimo brano (`ShowScheduleOnlyRow`; in Uno la riga intera è un pulsante, niente pulsante annidato).
  - **Differenze volute:** lista vuota nascosta durante il caricamento (`Fn.IsEmptyAndIdle`; MAUI mostra "Nessun palinsesto" insieme alla rotella); orari con `SecondaryTextBrush` che segue il tema (MAUI usa il colore grezzo `TextSecondary`, sempre quello del tema scuro); 📅 → glifo Font Awesome. `Fn.Time`, `Fn.DayDate`, `Fn.StreamerFrame/StreamerPadding` (cornice degli streamer, MAUI `DataTrigger`).
  - Verifiche: macOS (pannello con badge "In Onda", orari e date, catturato durante l'autotest); autotest desktop 0 errori al primo giro (poi `audio.steady` fallito perché lo stream di Radio E45 su `192.168.1.100` non rispondeva più, anche con `curl`); build desktop e web 0 avvisi/0 errori; `ParityCheck` = 1. Core e MAUI non modificati. Chrome non verificato (estensione non connessa).
  - Da fare con l'utente: aprire il palinsesto da In Onda su macOS, VM e Chrome (clic, trascinamento verso il basso, Esc, clic fuori); controllare sul web i pallini `●` dei badge (glifo Unicode nelle stringhe, potrebbe essere un quadratino come gli altri simboli).
- [x] S8.5 `SongRequestPage` — ✅ codice 2026-10-16, branch `S8.5` da `S8.4`
  - **Pagina:** ricerca con `TextBox` (`UpdateSourceTrigger=PropertyChanged`: filtra mentre si scrive, come la `SearchBar` MAUI), riquadro d'errore sotto la ricerca (tutto il riquadro è un pulsante → `DismissErrorCommand`), lista in `RefreshContainer` + F5, riga con copertina (segnaposto sempre sotto), titolo, artista, rotella durante la richiesta e pulsante "Richiedi" attenuato e disabilitato dopo una richiesta riuscita (`Fn.DimmedIf`; colore accento mantenuto anche da disabilitato), paginazione sempre visibile con glifi Font Awesome al posto di ◀ ▶. Lista vuota con 🎵 → glifo, nascosta durante il caricamento. Caricamento in `OnNavigatedTo` (MAUI `OnAppearing`). La gestione della tastiera MAUI (tocco/scroll per chiuderla, barra "Fine" iOS) non serve su desktop e web. `Tab_Request` (titolo della pagina MAUI) in `ignore`.
  - **Shell:** la tab Richieste è visibile solo se la stazione accetta richieste (`NowPlaying.RequestsEnabled`, come `AppShell`), con `ShellPage.OnAirViewModel` (secondo ViewModel della shell); se sparisce mentre è selezionata si torna a In Onda. `shell.tabs` resta `todo` fino alla tab Podcast (S8.6).
  - **ParityCheck:** i percorsi `{x:Bind XxxViewModel.Y}` contano come `{x:Bind ViewModel.Y}`, per le pagine con un secondo ViewModel (1 test, 149 totali).
  - Verifiche: macOS con AzuraTest (lista con copertine, paginazione 1/2, tab visibile) e con Radio E45 (errore 500 del server mostrato nel riquadro rosso, tab nascosta); build desktop e web 0 avvisi/0 errori; `ParityCheck` = 1. Core e MAUI non modificati.
  - Da fare con l'utente: invio reale di una richiesta (non fatto in automatico: è un'azione sul server), ricerca da tastiera, F5, pagina successiva, prova su VM e Chrome.
- [x] S8.6 `PodcastListPage`, `PodcastEpisodesPage` — ✅ codice 2026-10-16, branch `S8.6` da `S8.5`
  - **Lista podcast:** tab Podcast con intestazione (☰ + titolo), card come pulsanti (`SelectCommand`), copertina con segnaposto sotto, descrizione, numero di episodi; `RefreshContainer` + F5; lista vuota con glifo (🎙️ in MAUI). `PodcastListViewModel` **singleton** in Uno: si abbona a `OnAirViewModel.PropertyChanged` per seguire la stazione e in MAUI ne esiste una sola istanza (pagina della tab); con il `Frame` Uno, transient voleva dire una nuova istanza (mai sganciata) a ogni visita della tab.
  - **Episodi:** pagina impilata sopra la tab Podcast (`GoToPodcastEpisodesAsync` → `Push` con `PodcastEpisodesParameter(id, titolo)`), titolo `PodcastTitle`, filtri con `ComboBox` (`DisplayMemberPath="Label"`, `SelectedItem` OneWay + `SelectionChanged`), errore in testa alla lista, card come pulsanti (`PlayCommand`: avvia, oppure pausa/riprende l'episodio corrente), riga con stagione/episodio, data, posizione salvata, durata e pallino di stato (`Fn.ProgressStateBrush`). Mini player con play/pausa (glifo Font Awesome), titolo, tempi e slider non legato come in MAUI: valore impostato dal code-behind se l'utente non trascina; seek al rilascio (`PointerPressed`/`PointerCaptureLost` con `handledEventsToo`) o subito con la tastiera. Ciclo di vita S7b: `Activate()` in `OnNavigatedTo`, `StopPlaybackAsync()` + `Cleanup()` in `OnNavigatedFrom`; controllo `IsStaleForCurrentStation()` come in MAUI.
  - **Shell:** tab Podcast visibile solo con `OnAirViewModel.HasPodcasts`; `shell.tabs` completata. `ResetPodcastTabToRoot` resta vuoto: le tab condividono il `Frame` e un cambio di tab ne svuota la storia. `UnoNavigationService` non ha più intenti "non portati" (tolto `NotPorted`).
  - Verifiche: macOS con AzuraTest (lista podcast, pagina episodi con filtro Stato, pallino rosso dell'episodio completato dall'autotest, tempi); autotest desktop 0 errori; build della soluzione, desktop e web 0 avvisi/0 errori; 149 test; `ParityCheck` = 1 (restano solo `shell.system-media-controls` per S9 e `settings.diagnostics.crash-reports`). Core e MAUI non modificati.
  - **Correzione dopo la prova dell'utente (web):** tornando su In Onda dopo un episodio la radio non ripartiva da sola (sul web `CanAutoplay` era sempre `false`). Ora sul web `CanAutoplay` = `navigator.userActivation.hasBeenActive` (`Services/Browser/BrowserUserActivation`, `RadioE45Autoplay.js`): prima del primo clic nella pagina niente avvio automatico, dopo il browser permette `play()`. Nel Core `ActivateAsync` riavvia solo una radio già avviata almeno una volta (`_hasStartedRadio`), altrimenti sul web partirebbe da sola dopo un clic qualsiasi anche senza aver mai premuto Play; in MAUI non cambia nulla (la radio parte sempre all'avvio). 2 test (151 totali). Verificato in Chrome: Play su AzuraTest → episodio → In Onda: la radio riparte; scheda nuova, clic su un'altra tab e ritorno senza Play: resta ferma. Effetto collaterale: dopo un **ricaricamento** della pagina Chrome conserva l'attivazione e la radio parte all'avvio, come su desktop (una scheda nuova invece non parte).
  - **Filtri episodi (richiesta dell'utente, branch `S8.6.5`, MAUI e Uno):** sempre visibili e in ordine Stato, Stagione, Episodio. Tolti `IsSeasonFilterVisible`/`IsEpisodeFilterVisible` da `PodcastEpisodesViewModel` (prima Stagione/Episodio comparivano solo se almeno un episodio del feed ne aveva il valore); senza valori nel feed le due caselle offrono solo "Tutte/Tutti".
  - Da fare con l'utente: ascolto reale di un episodio dalla pagina (play, pausa, seek trascinando e con le frecce, cambio di tab che ferma l'episodio, ripresa dalla posizione salvata) su macOS, VM e Chrome; filtri stagione/episodio con un feed che li valorizza.
- [ ] S8.7 Verifica dei layout fissi, scorciatoie da tastiera
  - **Layout deciso (utente, 2026-10-15), niente layout adattivi:**
    - **Desktop:** finestra non ridimensionabile (né massimizzabile) che parte sempre con la stessa dimensione delle app MAUI desktop (Mac Catalyst, Windows): 500×855 in verticale o 900×450 in orizzontale, da `desktop_orientation` (`DesktopWindowSize` nel Core, `shell.desktop-window-size`, fatto in S7a). Nessuna dimensione salvata o ripristinata.
    - **Web:** solo la colonna centrata a larghezza fissa 500, pagine sempre in verticale (`UnoLayout.IsSingleColumn`, `shell.web-column`, fatto in S8.1). Il "web largo" a due colonne non esiste più.
  - Da fare: controllare che tutte le pagine stiano nelle due finestre desktop (anche 900×450) e nella colonna web; scorciatoie da tastiera.
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
| D9 | Layout desktop e web | S8.7 | ✅ Desktop: finestra fissa non ridimensionabile, stessa dimensione delle app MAUI desktop all'avvio (500×855 o 900×450). Web: una sola colonna larga 500, niente layout largo (2026-10-15) |
| D9 | Hook Stop bloccante o solo avviso | S5 | ✅ Bloccante solo per exit 2; drift della superficie VM non tracciato = exit 2 |
| D10 | Rilascio delle app Uno legato alle versioni MAUI (stesso `version.json`) | S10 | Sì, stessa versione |

---

## 14. Registro delle sessioni

| Data | Sessione | Esito | Note |
|---|---|---|---|
| 2026-10-16 | S8.6 | Codice completato (prove manuali dell'utente) | Branch `S8.6` da `S8.5`. `PodcastListPage` e `PodcastEpisodesPage` Uno (filtri, mini player, ciclo di vita S7b), tab Podcast legata a `HasPodcasts`, `PodcastListViewModel` singleton in Uno; verificate su macOS; soluzione verde, 149 test, ParityCheck = 1 (solo S9 e crash report). Core e MAUI invariati. Prossima: S8.7 |
| 2026-10-16 | S8.5 | Codice completato (prove manuali dell'utente) | Branch `S8.5` da `S8.4`. `SongRequestPage` Uno (ricerca, errore, lista, richiesta, paginazione), tab Richieste visibile solo con richieste abilitate; ParityCheck legge `XxxViewModel.Y`; 149 test; verificata su macOS; build verdi; ParityCheck = 1. Core e MAUI invariati. Prossima: S8.6 |
| 2026-10-16 | S8.4 | Codice completato (prove manuali dell'utente) | Branch `S8.4` da `S8.3`. Palinsesto Uno come pannello dal basso della shell (`ScheduleDialog`), ingresso da In Onda (pulsante e riga compatta); verificato su macOS; build verdi; ParityCheck = 1. Core e MAUI invariati. Prossima: S8.5 |
| 2026-10-15 | S8.3 | Completata (prova dell'utente ok) | Branch `S8.3` da `S8.2`. Menu laterale Uno (pannello in sovrimpressione con animazione propria, pulsante ☰ su In Onda), Termini d'uso al primo avvio, `SettingsPage`; verificate in Chrome e su macOS; autotest desktop ok; build verdi; ParityCheck = 1 (crash report resta `todo`). Deciso il layout per S8.7 (D9): desktop a finestra fissa come MAUI, web solo colonna fissa. Prossima: S8.4 |
| 2026-10-15 | S8.2 | Codice completato (prove manuali dell'utente) | Branch `S8.2` da `S8.1`. Canali, Aggiungi e Modifica stazione su Uno, impilate nel `Frame` della shell con pulsante indietro; menu contestuale al posto dello swipe, riordino dalla maniglia; avvio senza stazioni → Canali; colonna web a larghezza fissa; verificate in Chrome; autotest desktop ok; build verdi; ParityCheck = 1. Prossima: S8.3 |
| 2026-10-15 | S7b + S8.1 | Regressione MAUI ok | Confermata dall'utente su tutti gli ambienti (podcast, finestra desktop, build Windows, In Onda). Branch `S8.2` da `S8.1`. Prossima: S8.2 |
| 2026-10-15 | S8.1 | Completata (attesa regressione MAUI) | Branch `S8.1` da `main`. `OnAirPage` Uno completa (menu e palinsesto rinviati a S8.3/S8.4), stili, funzioni `x:Bind`, VU meter `SKCanvasElement` con livelli nel Core; `OnAirViewModel.ActivateAsync` + `CanAutoplay` (web senza autoplay); icone Font Awesome in Uno; ParityCheck legge gli argomenti delle funzioni `x:Bind`; verificata su macOS, VM e Chrome; 148 test, soluzione verde, ParityCheck = 1. Prossima: S8.2 |
| 2026-10-15 | S7b | Codice completato (attese regressione MAUI e prova podcast web dell'utente) | Motore podcast condiviso nel Core (14 test) con adattatori MAUI e Uno; 5 comportamenti di Uno 6.7 aggirati (macOS riapertura/Dispose/seek, web player multipli, libVLC fine episodio); radio senza rete ora mostra buffering (prova da 3 e 2 minuti nella VM); autotest podcast ok su macOS e VM; 135 test, soluzione verde, ParityCheck = 1. Prossima: S8.1 |
| 2026-10-15 | S7a | Completata (ascolto da confermare) | Branch `S7` da `S6`. Finestra desktop come MAUI (`DesktopWindowSize` nel Core), `IAppPaths`, `INetworkMonitor` desktop/web, DB + catalogo, audio live Uno con `StreamStateDebouncer` (5 test); autotest 17/17 su macOS e VM, ciclo Play/Pausa/Riprendi/Stop ok in Chrome; 121 test, soluzione verde, ParityCheck = 1. Prossima: S7b |
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
