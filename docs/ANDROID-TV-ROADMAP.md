# RadioE45 su Android TV / Google TV — Piano operativo

> Documento operativo, da riprendere a ogni sessione Claude Code. Modellato su `docs/LINUX-UNO-ROADMAP.md` (stesso sistema di parità, stesse regole di processo).
> Analisi di fattibilità e rischi: [`ANDROID-TV.md`](ANDROID-TV.md). Ultimo aggiornamento: 2026-10-22 (app v0.48).
> Stato: **T0 e T1 completate; prossima sessione: T2** (§6). Aggiornare le checkbox e il registro (§9) alla fine di ogni sessione.

---

## 0. TL;DR per la sessione che riprende

1. Leggere §1 (regole), §4 (architettura), §5 (parità TV) e §6 (piano a sessioni).
2. Trovare in §6 la prima sessione con checkbox aperte ed eseguire solo quella.
3. Alla fine: build verde, checkbox aggiornate, voce nel registro §9, messaggio di commit proposto **come testo** (mai eseguire `git commit`).
4. Le prove si fanno sull'emulatore `Television_1080p` (§2); il TV Sony reale arriva in T7.

---

## 1. Obiettivi e regole fisse

**Obiettivi**
- La stessa app Android (`com.radioe45.app`, target `net10.0-android`) funziona su Android TV / Google TV, con un'interfaccia pensata per il telecomando (D-pad) e per la distanza da divano.
- Ambito: **opzione C allargata** (decisione 2026-10-22): lettore con stazione corrente, Schedule, selezione e gestione delle stazioni (aggiunta, modifica, eliminazione) e podcast (elenco, episodi, ascolto). Niente layout a 10 piedi completo come l'opzione B.
- **Nessuna impostazione e nessun menu laterale (flyout) su TV** (decisione 2026-10-22). Le stazioni si scelgono e si gestiscono dalla sezione **Stazioni**, solo TV (§4). Il dialogo dei termini d'uso compare solo al primo utilizzo.
- Fire TV (Amazon Appstore): il dispositivo c'è, ma T8 è **rinviata** (decisione 2026-10-22). Si riprende a Google TV chiuso.
- L'app MAUI su telefono, iOS, Mac, Windows e l'app Uno non cambiano.

**Regole decise (non ridiscutere senza l'utente)**
1. **MAUI first, su Android:** le viste TV sono pagine MAUI in `RadioE45/Views/Tv/`, scelte a runtime quando il dispositivo è una TV. Nessun nuovo target framework, nessun nuovo progetto.
2. **Nessuna feature senza traccia:** `tv` è la quarta piattaforma del manifest di parità (§5). Il disallineamento è ammesso solo se dichiarato (`todo`, oppure `n/a`/`limited` con motivo).
3. **ViewModel unici nel Core:** le viste TV usano gli stessi ViewModel del telefono. Niente logica nelle viste TV, solo layout, binding e gestione del focus. Se serve comportamento nuovo (es. "stazione successiva"), va nel ViewModel o in un servizio del Core.
4. **Core senza UI** (regola 4 di `CLAUDE.md`): il rilevamento TV e i tasti del telecomando stanno nel progetto MAUI o dietro un'interfaccia di `Services/Platform/`, mai nel Core.
5. **Stringhe uniche** nei `.resx` del Core (it neutro, en, pl). Ogni testo TV nuovo passa da lì.
6. **Test sempre su emulatore prima del device reale**; ogni sessione che tocca la UI produce screenshot e la mappa del focus (§2.3).
7. Vincoli dell'utente già validi: niente commit, tag o push eseguiti da Claude (solo testo del messaggio, in inglese); testi di store e changelog in inglese; dopo ogni modifica `.cs` diagnostica LSP e `dotnet build`; niente `rm -rf`.

---

## 2. Ambiente di sviluppo e test

| Dispositivo | Uso | Stato |
|---|---|---|
| **Emulatore `Television_1080p`** | Prove di ogni sessione | Attivo: Android 12 (API 31), immagine Android TV con Google Play, arm64, 1920x1080 a 320 dpi (**960x540 dp**), `leanback_only` |
| **TV Sony Bravia Google TV** (circa 3 anni) | Prova finale: audio, standby, telecomando vero (T7) | Modello e versione Android da leggere dalle impostazioni; si collega con `adb connect <ip>` |
| **Fire TV** | Solo T8 (rinviata) | Dispositivo disponibile; non usato per ora |

### 2.1 Comandi

```bash
ADB=~/Library/Android/sdk/platform-tools/adb          # non è nel PATH di default

# installare la build di debug sull'emulatore (compila e installa)
dotnet build RadioE45 -f net10.0-android -t:Install -p:AdbTarget="-s emulator-5554"

# avviare l'app come la avvia il launcher TV
$ADB -s emulator-5554 shell am start -a android.intent.action.MAIN \
  -c android.intent.category.LEANBACK_LAUNCHER -n com.radioe45.app/crc64c01558c6a485bf1e.MainActivity

# telecomando: KEYCODE_DPAD_{UP,DOWN,LEFT,RIGHT,CENTER}, KEYCODE_BACK, KEYCODE_HOME, KEYCODE_MEDIA_PLAY_PAUSE
$ADB -s emulator-5554 shell input keyevent KEYCODE_DPAD_DOWN

# screenshot dello schermo del dispositivo (mai dello schermo del Mac)
$ADB -s emulator-5554 exec-out screencap -p > shot.png
```

Il nome della classe activity (`crc64...`) è generato da .NET: se cambia, leggerlo con `$ADB shell cmd package query-activities --brief -a android.intent.action.MAIN -c android.intent.category.LEANBACK_LAUNCHER`.

### 2.2 Avvertenze apprese
- Nella home TV non premere `DPAD_CENTER` alla cieca: apre ciò che ha il focus (è successo con YouTube). Guardare il focus prima.
- Al primo avvio il dialogo dei termini d'uso non ha il focus sul pulsante: serve prima un movimento del D-pad (es. `DPAD_DOWN`), poi `DPAD_CENTER`. Con la vista TV il pulsante dovrà avere il focus iniziale.

### 2.3 Mappa del focus (verifica automatizzabile)
`uiautomator dump` restituisce l'elemento con `focused="true"`. Mandando una sequenza di tasti e leggendo il focus a ogni passo si ottiene la **mappa di raggiungibilità**: ogni controllo azionabile della vista TV deve essere raggiungibile col D-pad. In T2 diventa uno script (`tools/tv-focus-probe.sh`) usato come criterio di uscita delle sessioni UI, al posto dell'impressione visiva.

---

## 3. Misure sull'emulatore (T0, 2026-10-22)

| Verifica | Esito |
|---|---|
| Manifest: `leanback` e `touchscreen` con `required="false"`, banner, `LEANBACK_LAUNCHER` | Build verde; il manifest generato contiene tutto |
| `query-activities` per la categoria `LEANBACK_LAUNCHER` | `MainActivity` risolta |
| Launcher TV | L'app compare nella riga **Apps** con il banner (logo E45 su fondo crema) |
| Avvio dell'UI attuale (telefono) su TV | Parte e non va in crash; layout a due colonne in landscape |
| Focus D-pad sulla UI attuale | **Erratico**: il focus iniziale è su un `RecyclerView`; `DOWN` porta al tab `Request` in basso; `RIGHT` va su una riga di testo e poi si blocca; `LEFT` va al pulsante menu. Play, avanti, volume, Schedule e cuore **non sono mai raggiunti** |
| Indicatore di focus visibile | Assente negli screenshot |

**Conclusione:** l'opzione A (stessa UI con un po' di focus) è scartata con dati reali. Servono viste TV dedicate (§4).

---

## 4. Architettura di destinazione

```
App Android (un solo APK/AAB)
 ├─ telefono/tablet  → AppShell (invariato)
 └─ TV               → TvRootPage (viste in RadioE45/Views/Tv/)
                          │
        ViewModel del Core (RadioListViewModel, OnAirViewModel,
        PodcastListViewModel, PodcastEpisodesViewModel)
                          │
        Servizi del Core + RadioPlaybackService (Media3, già presente)
```

- **Rilevamento:** all'avvio, `DeviceInfo.Idiom == DeviceIdiom.TV` (MAUI Essentials lo ricava da `UiModeManager`). Da confermare con un log in T2. La scelta della pagina radice avviene dove oggi si crea `AppShell`.
- **Layout:** pagina logica 960x540 dp, margini di sicurezza (overscan) circa 48 dp ai lati e 27 dp sopra e sotto, testo grande, bersagli ampi.
- **Focus:** ogni controllo azionabile è focusabile, con stato visivo `Focused` ben visibile (bordo e ingrandimento). Il focus iniziale cade sul controllo principale (play). Il tasto Indietro risale di livello e, dalla radice, manda l'app in background (comportamento già presente in `MainActivity.OnBackPressed`).
- **Tasti media:** play/pausa, avanti, indietro del telecomando arrivano alla sessione Media3 già esistente. Il cambio stazione avanti/indietro è già agganciato ai comandi remoti iOS: va riusata la stessa logica del Core.
- **Audio:** nessun lavoro nuovo per le stazioni live. Per i podcast da verificare quale servizio li riproduce su Android e se convive con `RadioPlaybackService` (T5).
- **Mappa pagine:**

| Pagina TV | Pagina telefono | ViewModel |
|---|---|---|
| `Tv/TvNowPlayingPage` (stazione corrente, controlli play/pausa, avanti/indietro, accesso a Schedule; il volume è del televisore, `onair.volume` è `n/a`) | `OnAirPage` | `OnAirViewModel` |
| `Tv/TvStationsPage` (sezione **Stazioni**, solo TV: lista con selezione, focus iniziale sulla stazione corrente, azioni di gestione) | `RadioListPage` | `RadioListViewModel` |
| `Tv/TvScheduleDialog` (palinsesto leggibile da lontano, scorrimento col D-pad) | `SchedulePopup` | `ScheduleViewModel` |
| `Tv/TvRequestPage` (richiesta brani, come sul telefono) | `SongRequestPage` | `SongRequestViewModel` |
| `Tv/TvPodcastListPage` | `PodcastListPage` | `PodcastListViewModel` |
| `Tv/TvPodcastEpisodesPage` | `PodcastEpisodesPage` | `PodcastEpisodesViewModel` |
| Impostazioni e menu laterale | `SettingsPage`, flyout di `AppShell` | `n/a` su TV (decisione 2026-10-22) |
| Aggiungi, modifica, elimina stazione | `AddStationPage`, `EditStationPage` | `Tv/TvAddStationPage`, `Tv/TvEditStationPage`, aperte dalla sezione Stazioni; ViewModel `AddStationViewModel`, `EditStationViewModel` |

**Navigazione su TV (decisione 2026-10-22):** niente barra di tab MAUI. Una fila di **pulsanti focusabili** in alto, ciascuno una sezione:
- **On Air** e **Stations**: sempre visibili.
- **Request** e **Podcast**: visibili solo se la stazione corrente ha la capability (come le tab del telefono, `shell.tabs`, pilotate da `OnAirViewModel`).
- Le etichette riusano le chiavi `.resx` esistenti (nessun testo nuovo se non necessario).

La sezione **Stations**, solo TV, elenca le stazioni (OK seleziona) e dà accesso ad aggiunta, modifica ed eliminazione. L'inserimento di nome e URL usa la tastiera su schermo di Android TV. La fila di pulsanti è il primo elemento da verificare nello spike T2: stato `Focused` visibile, sezione corrente evidenziata, SU/GIÙ per uscire dalla fila e rientrarvi.

---

## 5. Sistema di parità per la piattaforma `tv`

Stesso impianto di `LINUX-UNO-ROADMAP.md` §6, con `tv` accanto a `maui`, `desktop`, `web`.

### 5.1 Livelli
1. **Architettura:** la logica non diverge perché sta nei ViewModel unici (regola 3).
2. **Manifest** (`parity/features.yaml`): nuova chiave `status.tv` (`done | todo | limited | n/a`) e campo opzionale `tvNotes`, obbligatorio quando `tv` è `limited` o `n/a` (il campo `notes` esistente parla di desktop e web, quindi non si mescola).
3. **Verifica automatica** (`tools/ParityCheck`), estensione prevista in T1:
   - `FeatureStatus.Tv` e `Feature.TvNotes`; `PageMap.Tv` (percorso della pagina TV oppure `n/a` + `tvReason`);
   - stesso confronto dei binding e delle chiavi di stringa tra pagina MAUI e pagina TV, con `ignore` per i membri solo-touch (swipe, trascinamento, pull-to-refresh);
   - copertura: ogni file in `RadioE45/Views/Tv/*.xaml` deve comparire in `pages.yaml`;
   - debito: `tv: todo` è debito dichiarato. `--strict` resta com'è oggi (desktop e web); `--strict --include-tv` conta anche `tv` ed è il gate prima di pubblicare per TV;
   - test in `RadioE45.Core.Tests/Parity/` per le nuove regole.
4. **Processo in Claude Code:**
   - `CLAUDE.md`: regola 8 estesa alle viste TV;
   - skill `tv-parity-port` (T1), gemella di `ui-parity-port`: legge la feature, apre la pagina TV da `pages.yaml`, converte con `docs/parity/MAUI-TO-TV.md` (scritto in T2 dopo lo spike), esegue la mappa del focus, aggiorna lo stato;
   - hook (`tools/parity-hook.sh`): il promemoria dopo una modifica a Views o ViewModel cita anche la piattaforma TV;
   - trailer di commit: `Parity: maui, desktop, web, tv`, oppure `Parity-Debt: <feature-id> (tv)`.
5. **Verifica visiva e di focus:** screenshot dell'emulatore e mappa del focus (§2.3) per ogni pagina TV toccata.

### 5.2 Inventario iniziale (T1)
Per ogni feature esistente in `features.yaml` si dichiara `tv`: `todo` se rientra nell'ambito (§1), `n/a` con `tvNotes` se non ha senso su TV (impostazioni, menu laterale, trascinamento per riordinare, ecc.). Nessuna feature resta senza valore.

---

## 6. Piano a sessioni Claude Code

Ogni sessione è eseguibile e verificabile da un agente: criteri di uscita oggettivi, build verde, emulatore.

### T0 — Ambiente e manifest · Mac + emulatore — ✅ completata 2026-10-22
- [x] Decisioni (opzione C con podcast, Fire TV solo se facile, emulatore prima del Sony)
- [x] Manifest: `leanback`, `touchscreen`, banner 320x180, `LEANBACK_LAUNCHER` su `MainActivity`
- [x] Build Android verde; installazione sull'emulatore; icona e banner nel launcher TV
- [x] Baseline del focus sulla UI telefono (§3): opzione A scartata

### T1 — Sistema di parità TV · Mac — ✅ completata 2026-10-22
- [x] `parity/features.yaml`: `status.tv` e `tvNotes` per tutte le 68 feature esistenti (§5.2) più 4 feature solo-TV (`tv.launcher`, `tv.focus`, `tv.media-keys`, `tv.back`)
- [x] `parity/pages.yaml`: campo `tv` per ogni pagina (percorso o `n/a` + `tvReason`); Impostazioni `n/a`
- [x] `tools/ParityCheck`: modello (`Manifest.cs`), controlli (`ParityChecker.cs`), flag `--include-tv`; 12 test nuovi in `RadioE45.Core.Tests/Parity/`
- [x] Skill `.claude/skills/tv-parity-port/SKILL.md`; hook `tools/parity-hook.sh` con promemoria dedicato alle viste TV; `CLAUDE.md` e `ui-parity-port` con riferimento a `tv`
- [x] Uscita: `ParityCheck` restituisce 1 (solo debito dichiarato), `dotnet test` verde (277), `--strict` invariato (solo `settings.diagnostics.crash-reports`), `--strict --include-tv` conta i 44 `tv: todo`
- Regola aggiunta in T1: una feature **senza ViewModel** (piattaforma, es. `tv.launcher`) può essere `done` su tv anche se il file della pagina TV non esiste ancora; per le altre serve il file.

### T2 — Spike: rilevamento TV, pagina radice, focus · Mac + emulatore
- [ ] Log che conferma `DeviceIdiom.TV` sull'emulatore; scelta della pagina radice in base al form factor
- [ ] `TvRootPage` minima con tre controlli focusabili e stato `Focused` visibile
- [ ] `tools/tv-focus-probe.sh` (§2.3) e `docs/parity/MAUI-TO-TV.md` (tabella di conversione e regole di layout TV)
- [ ] Verifica tasti media e Indietro; dialogo dei termini con focus iniziale sul pulsante
- [ ] Uscita: mappa del focus che raggiunge i tre controlli; decisione confermata sul perimetro (C o C allargata)

### T3 — Navigazione a pulsanti, lettore e selezione stazioni · Mac + emulatore
- [ ] Fila di pulsanti focusabili (On Air, Stations sempre; Request e Podcast solo con la capability, aggiunti in T5) con focus e sezione corrente ben visibili
- [ ] `TvNowPlayingPage`: logo/copertina, titolo e artista, stato, ascoltatori
- [ ] `TvStationsPage`: lista, focus iniziale sulla stazione corrente, selezione con OK
- [ ] Binding a `OnAirViewModel` e `RadioListViewModel`; `features.yaml` e `pages.yaml` aggiornati
- [ ] Uscita: riproduzione di una stazione dall'emulatore, mappa del focus completa, ParityCheck 0 o 1

### T4 — Controlli, Schedule e gestione stazioni · Mac + emulatore
- [ ] Play/pausa e avanti/indietro stazione (il volume resta del televisore); tasti media del telecomando verso la sessione Media3
- [ ] `TvScheduleDialog`: palinsesto con scorrimento col D-pad
- [ ] Gestione stazioni dalla sezione Stations: `TvAddStationPage`, `TvEditStationPage`, eliminazione con conferma; tastiera su schermo
- [ ] Dialogo dei termini d'uso solo al primo utilizzo, con focus iniziale sul pulsante
- [ ] Stati di errore e offline leggibili da lontano
- [ ] Uscita: tutti i controlli raggiungibili e comandabili col solo D-pad e tasti media

### T5 — Richieste e podcast · Mac + emulatore
- [ ] `TvRequestPage` con `SongRequestViewModel`; pulsante **Request** visibile solo con la capability
- [ ] Pulsante **Podcast** visibile solo con la capability
- [ ] Verifica del servizio che riproduce gli episodi su Android e convivenza con `RadioPlaybackService` (prima di costruire la UI)
- [ ] `TvPodcastListPage`, `TvPodcastEpisodesPage`, mini player con seek da D-pad
- [ ] Uscita: ascolto di un episodio dall'emulatore, ripresa dalla posizione salvata, parità aggiornata

### T6 — Rifinitura 10 piedi · Mac + emulatore
- [ ] Stringhe in it/en/pl, dimensioni testo, margini di sicurezza, immagini, tema
- [ ] Prestazioni su lista lunga e immagini, uso memoria
- [ ] Uscita: revisione degli screenshot, nessun elemento non focusabile

### T7 — TV Sony Google TV reale · Mac + TV
- [ ] Abilitare il debug di rete sul TV, `adb connect`, installazione
- [ ] Audio, rete, standby e spegnimento, telecomando vero, tasti media
- [ ] Uscita: esiti e differenze rispetto all'emulatore scritti in §3

### T8 — Fire TV · ⏸ rinviata (2026-10-22, dopo Google TV)
- [ ] Il dispositivo Fire TV c'è: ripartire da qui a Google TV chiuso
- [ ] Verificare che lo stesso APK funzioni; requisiti dell'Amazon Appstore
- [ ] Uscita: `limited` o `done` dichiarato; altrimenti fuori ambito

### T9 — Distribuzione · Mac
- [ ] Play Console: form factor Android TV, screenshot 16:9 (almeno 1280x720), banner, questionario TV
- [ ] Testi di store in inglese; riga in `CHANGELOG.md` sotto `[Unreleased]`
- [ ] `docs/STORE-DISTRIBUTION.md` e `docs/RELEASE.md` aggiornati; `--strict --include-tv` verde
- [ ] Release con la skill `release-prep` (comandi proposti, mai eseguiti da Claude)

### Stima indicativa
T1 un giorno; T2 un giorno; T3 e T4 circa una settimana insieme; T5 due o tre giorni; T6 un giorno o due; T7 mezza giornata; T8 mezza giornata se il device c'è; T9 mezza giornata.

---

## 7. Rischi

| Rischio | Mitigazione |
|---|---|
| Focus D-pad debole nei controlli MAUI | Stato `Focused` esplicito e mappa del focus automatica (§2.3) già in T2; se un controllo non è focusabile, si usa una `Border`/`Button` focusabile al suo posto |
| `DeviceIdiom.TV` non affidabile su tutti i TV | Confermarlo sull'emulatore (T2) e sul Sony (T7); fallback: `UiModeManager` in un servizio di piattaforma |
| Player podcast non compatibile con la sessione Media3 | Verificarlo all'inizio di T5; se serve, i podcast passano da `RadioPlaybackService` |
| Due UI da tenere allineate | Parità `tv` nel manifest, ParityCheck, skill e hook (§5) |
| Audio in background e standby | Prova su TV reale (T7) |
| Requisiti Play Console per TV cambiati | Rileggere la console all'upload (T9) |

---

## 8. Decisioni

**Prese (2026-10-22)**
- Ambito: opzione C con podcast; niente vista TV completa (B).
- Una sola app Android, viste TV dedicate dentro l'app MAUI, parità come quarta piattaforma `tv`.
- Prove su emulatore prima, poi Sony Google TV.
- Fire TV solo se non complica (T8).

- Nessuna impostazione e nessun menu laterale su TV.
- Navigazione su TV: fila di pulsanti focusabili, niente barra di tab. **On Air** e **Stations** sempre visibili; **Request** e **Podcast** solo se la stazione ha la capability.
- Sezione **Stations**, solo TV, per selezione e gestione (aggiunta, modifica, eliminazione).
- Richieste brani su TV: sì.
- Schedule su TV: sì.
- Dialogo dei termini d'uso: solo al primo utilizzo, senza rilettura da Impostazioni.
- Fire TV: dispositivo disponibile, T8 rinviata.

**Aperte**
Nessuna decisione aperta. Lo spike T2 può ancora cambiare i dettagli della fila di pulsanti (§4) se il focus non si comporta come previsto.

---

## 9. Registro delle sessioni

| Data | Sessione | Esito |
|---|---|---|
| 2026-10-18 | Analisi | Fattibilità e azioni in `ANDROID-TV.md` |
| 2026-10-22 | T0 | Manifest TV, banner, installazione ed esame dell'emulatore; opzione A scartata; piano e parità TV progettati |
| 2026-10-22 | Decisioni | Niente impostazioni né flyout su TV; T8 (Fire TV) rinviata |
| 2026-10-22 | Decisioni | Sezione Stations solo-TV per selezione e gestione; Schedule inclusa; termini d'uso solo al primo utilizzo |
| 2026-10-22 | Decisioni | Navigazione a pulsanti focusabili (On Air, Stations sempre; Request, Podcast con la capability), niente barra di tab; Richieste incluse |
| 2026-10-22 | T1 | Piattaforma `tv` in features/pages, `ParityCheck --include-tv`, 12 test, skill `tv-parity-port`, hook. Inventario: 44 `todo`, il resto `n/a` con motivo o `done` (launcher) |

---

## 10. Riferimenti
- [`ANDROID-TV.md`](ANDROID-TV.md): fattibilità, stato iniziale, rischi
- [`LINUX-UNO-ROADMAP.md`](LINUX-UNO-ROADMAP.md): modello del sistema di parità (§6) e del processo
- [`STORE-DISTRIBUTION.md`](STORE-DISTRIBUTION.md), [`RELEASE.md`](RELEASE.md), [`VERSION-BUMP.md`](VERSION-BUMP.md)
- Android TV: linee guida di qualità e requisiti Play per app TV (rileggerli in T9)
