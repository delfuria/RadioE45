# RadioE45 su Android TV / Google TV — Piano operativo

> Documento operativo, da riprendere a ogni sessione Claude Code. Modellato su `docs/LINUX-UNO-ROADMAP.md` (stesso sistema di parità, stesse regole di processo).
> Analisi di fattibilità e rischi: [`ANDROID-TV.md`](ANDROID-TV.md). Ultimo aggiornamento: 2026-10-23 (app v0.48).
> Stato: **T0–T6 completate; prossima sessione: T7** (TV Sony reale, serve il dispositivo) (§6). Aggiornare le checkbox e il registro (§9) alla fine di ogni sessione.

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
2. **Nessuna feature senza traccia:** `tv` è la quarta piattaforma del manifest di parità (§5). Il disallineamento è ammesso solo se dichiarato (`todo`, oppure `n/a`/`limited` con motivo). Su TV molte funzioni non arriveranno mai, perché non servono o non funzionano col telecomando: `n/a` è una risposta **definitiva e legittima** e la parità completa non è un obiettivo.
3. **ViewModel unici nel Core:** le viste TV usano gli stessi ViewModel del telefono. Niente logica nelle viste TV, solo layout, binding e gestione del focus. Se serve comportamento nuovo (es. "stazione successiva"), va nel ViewModel o in un servizio del Core.
4. **Core senza UI** (regola 4 di `CLAUDE.md`): il rilevamento TV e i tasti del telecomando stanno nel progetto MAUI o dietro un'interfaccia di `Services/Platform/`, mai nel Core.
5. **Stringhe uniche** nei `.resx` del Core (it neutro, en, pl). Ogni testo TV nuovo passa da lì.
6. **Test sempre su emulatore prima del device reale**; ogni sessione che tocca la UI produce screenshot e la mappa del focus (§2.3).
7. Vincoli dell'utente già validi: niente commit, tag o push eseguiti da Claude (solo testo del messaggio, in inglese); testi di store e changelog in inglese; dopo ogni modifica `.cs` diagnostica LSP e `dotnet build`; niente `rm -rf`.
8. Changelog: la TV non si annuncia prima del rilascio TV; le voci si accumulano in [`ANDROID-TV-CHANGELOG-PENDING.md`](ANDROID-TV-CHANGELOG-PENDING.md), niente righe sotto `[Unreleased]`.

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
`uiautomator dump` restituisce l'elemento con `focused="true"`. Mandando una sequenza di tasti e leggendo il focus a ogni passo si ottiene la **mappa di raggiungibilità**: ogni controllo azionabile della vista TV deve essere raggiungibile col D-pad. È lo script `tools/tv-focus-probe.sh` (`tour --expect "etichette"`), criterio di uscita delle sessioni UI al posto dell'impressione visiva. Il tour esplora tutto il grafo del focus e richiede circa un minuto per pagina (un `uiautomator dump` per tasto).

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

### Misure di T2 (2026-10-22, pagina TV dedicata)

| Verifica | Esito |
|---|---|
| Rilevamento | `DeviceInfo.Idiom=TV`, creata `TvRootPage` |
| Fila di pulsanti | Focus raggiungibile col D-pad; `Request` compare solo se la stazione lo supporta, `Podcast` resta nascosto (stazione senza podcast) |
| Stato `Focused` | Visibile con lo stile `TvButton` (il `VisualStateManager` di MAUI riceve il focus) |
| Focus iniziale | Da `OnAppearing` non si imposta (nessun elemento focalizzato); da `Loaded` + `Dispatcher` sì, sul pulsante play |
| OK sul pulsante play | Comanda la riproduzione (3 → 0 → 3 sulla sessione Media3) |
| **Focus perso dopo OK** | Il pulsante si disabilita mentre il comando asincrono gira e Android sposta il focus su «On Air» senza restituirlo. Risolto con `TvKeepFocusBehavior`: 4 OK di fila, il focus resta |
| Tasti media del telecomando | Play/pausa, pausa, play, avanti, indietro comandano la sessione Media3 senza codice dedicato; avanti/indietro cambiano stazione |
| Indietro dalla radice | App in background, audio continua (`state=3`), riapertura riporta la stessa activity |
| Emoji ⏸ | Si disegna a colori; `U+FE0E` ignorato. Usati i glifi `FaSolid` |
| DB vuoto | Sull'emulatore le stazioni non c'erano: senza stazione la pagina resta su «Waiting...». Il seed passa dalla pagina Stazioni (T3). Per le prove ho inserito le due stazioni predefinite nel DB dell'emulatore (scaricando il file, `sqlite3` sul Mac, rimettendolo con `run-as`) |
| Osservazione | Una volta, al primo avvio dopo un'installazione, la chiamata Refit è fallita con `FileNotFoundException: System.Net.Quic`. Non riprodotta in 2 avvii a freddo successivi. Non è specifica della TV: da tenere d'occhio, in particolare sul TV reale (T7) |


### Misure di T3 (2026-10-22, primo avvio e navigazione)

| Verifica | Esito |
|---|---|
| Primo avvio, installazione pulita (`pm clear`) | Termini d'uso con il focus sul pulsante → OK → domanda sulle stazioni di prova con il focus su «Yes» → OK → il DB si popola, parte RadioE45, la vista torna a On Air |
| **Focus rifiutato nel dialogo** | `Focus()` rispondeva `True` ma il focus non si agganciava: la finestra è in *touch mode* subito dopo un avvio a freddo e il pulsante non è `focusableInTouchMode` (letto dallo stato nativo: `inTouchMode=True`, `focusableInTouch=False`, `nativeRequest=False`). Con `RequestFocusFromTouch` (helper `TvFocus`) il focus si aggancia |
| **Lista stazioni vuota** | Il ViewModel aveva 2 stazioni ma il layout 0 figli; `ErrorMessage` conteneva `targetType needs to be assignable from System.String`. Causa: un `MultiBinding` con `BoolToObjectConverter` nel testo del pulsante, che lancia alla creazione dei figli e viene inghiottito dal `SafeExecuteAsync` della lista. Sostituito da un `DataTrigger` che imposta l'icona |
| Elenco stazioni | Focus iniziale sulla stazione in ascolto; D-pad raggiunge entrambe le stazioni; il puntino marca quella attiva |
| Selezione | OK su «AzuraTest Radio»: la riproduzione passa ad AzuraTest, la vista torna a On Air, il focus è sul pulsante play |
| Indietro | Da Channels torna a On Air; da On Air l'app va in background e l'audio continua (`state=3`); la riapertura riporta la stessa activity |
| Tour del focus | Il primo tour «a serpentina per righe» non raggiungeva l'ultima stazione (3 volte su 3 dopo la prima): `LEFT` da una riga della lista salta alla fila di pulsanti in alto (Android cerca il vicino più prossimo nella direzione), quindi il percorso ripassava sempre dagli stessi elementi. Sostituito da un'esplorazione del grafo del focus: prova SU/GIÙ/SINISTRA/DESTRA da ogni elemento, ricorda gli archi e ritorna dove restano direzioni da provare (`TRACE=1` li stampa). Dal grafo: GIÙ da On Air o da Channels porta alla prima stazione, SINISTRA dalla lista porta a On Air |
| Dialogo e margini di sicurezza | La scheda arrivava quasi ai bordi dello schermo: area scorrevole del messaggio ridotta a 230 dp. L'altezza del dialogo dei termini resta dentro 27 dp sopra e sotto |
| Indietro nei dialoghi | Sui termini chiude (come l'AlertDialog del telefono, quindi i termini risultano accettati); sulle stazioni di prova equivale a «No» e si torna a On Air |
| Bordo ingrandito tagliato | La riga e il pulsante del dialogo con focus (scala 1,08) venivano tagliati dai bordi di `ScrollView` e `Border`: serve padding intorno ai controlli che si ingrandiscono |
| Crash nativo (1 volta) | `SIGSEGV` in `libmonodroid.so` (`EmbeddedAssemblies::open_from_bundles`) nel thread pool, subito dopo «Yes» al primo avvio dopo un'installazione. Non riprodotto in 2 primi avvii puliti successivi. Collegato all'osservazione `System.Net.Quic` di T2: entrambi sono errori del caricatore di assembly, solo al primo avvio dopo un'installazione di debug (fast deployment). **Da riprovare con una build Release** (T7) prima di considerarlo chiuso |

### Misure di T4 (2026-10-23, controlli, schedule e gestione stazioni)

| Verifica | Esito |
|---|---|
| **Crash all'apertura** (due volte) | 1) `TvKeepFocusBehavior` era `Behavior<Button>`: agganciato a un `ImageButton` (il cuore) lancia durante il caricamento del XAML e l'app si chiude. Ora è `Behavior<VisualElement>`. 2) `BoolToObjectConverter` con `x:TypeArguments="x:String"` su `ImageButton.Source`: stessa `targetType needs to be assignable from System.String` di T3 (la proprietà è `ImageSource`); senza argomenti di tipo funziona. Il caricamento XAML avviene a runtime: l'errore compare solo all'avvio, la build non lo vede |
| Tastiera su schermo | OK su un `Entry` apre la tastiera di Android TV (Gboard); il focus iniziale su un `Entry` **non** la apre. Si naviga col D-pad, il tasto di invio mostra la lente (`ReturnType="Search"`) e lancia `SearchCommand`; la finestra si sposta in su e la tastiera copre la parte bassa. Indietro chiude prima la tastiera, poi risale. `adb shell input text` scrive nel campo mentre la tastiera è aperta |
| Schedule | La riga è un `Button` con il testo da un `MultiBinding` con `StringFormat` (nessun convertitore: non lancia). Il D-pad scorre l'elenco, la riga con il focus entra nello `ScrollView` |
| Conferma di eliminazione | Il dialogo della TV (`TvDialogOverlay`) ha il focus su «Delete»; dopo la conferma il focus torna sulla stazione attiva e l'elenco si aggiorna da solo |
| Aggiunta | Ricerca su un server reale: i risultati compaiono, la spunta passa dal pulsante (`ToggleSelectedCommand`), «Add selected (1)» è raggiungibile con GIÙ dopo l'ultimo risultato; l'elenco delle stazioni si aggiorna al ritorno |
| Pulsante con comando asincrono | `TvKeepFocusBehavior` serve anche su Aggiorna e sul cuore: il focus resta sul pulsante |
| Bordo ingrandito tagliato (di nuovo) | La riga larga con il focus (scala 1,08) era tagliata a sinistra nell'elenco: padding 28 dp per le stazioni, 40 dp per le righe larghe di Schedule e dei risultati |
| Primo avvio dopo `pm clear` | Termini (focus sul pulsante) → stazioni di prova → riproduzione: invariato rispetto a T3 |

### Misure di T5 (2026-10-23, richieste e podcast)

| Verifica | Esito |
|---|---|
| Servizio podcast su Android | `PodcastPlayerService` (Core `PodcastPlaybackService` + `MediaElementPodcastPlayer`) usa una `MediaElement` ospitata da una vista: sul telefono quella di `PodcastEpisodesPage`, qui quella di `TvPodcastEpisodesView` (invisibile, passata al servizio da `Loaded`). È separata dalla radio, che gira nel servizio Media3 (`RadioPlaybackService`): convivono come sul telefono (l'avvio dell'episodio ferma la radio, tornare su On Air ferma l'episodio). Nessuna modifica al servizio |
| Riproduzione | Episodio di 27 s dal server di prova: parte, il mini player mostra tempi e barra, +30 s lo porta a fine e lo segna come finito (spunta); uscire dalla schermata lo ferma, rientrare riparte dalla posizione salvata (0:06, non da 0) |
| Focus del mini player | Quando compare, Android toglie il focus alla riga che ha avviato l'episodio e lo manda al primo controllo della pagina («On Air»). La vista lo porta sul tasto play/pausa alla comparsa del mini player e sulla lista quando sparisce (non quando si esce dalla sezione) |
| Richieste | Elenco da 370 brani in 37 pagine; la paginazione sta accanto alla ricerca (raggiungibile senza scorrere), il filtro segue il testo, la tastiera su schermo si apre con OK. L'invio della richiesta non è stato provato: il server è la radio reale |
| Capability | Il pulsante Request compare solo se `NowPlaying.RequestsEnabled`, Podcast solo se `HasPodcasts`. Subito dopo un cambio stazione resta per un attimo quello della stazione precedente (il `NowPlaying` si aggiorna col sondaggio); `TvRootPage` esce da una sezione che non vale più, come i tab del telefono |
| Controlli del lettore | Con quattro pulsanti (precedente, play, successivo, Schedule) a destra della copertina l'ultimo usciva dal bordo: la riga dei controlli sta sotto copertina e informazioni, centrata (copertina 300 dp, il testo sotto i pulsanti finisce a ~510 dp su 540). Schedule: il focus parte da Close |
| Stazione di avvio | Senza impostazioni, `IAppEnvironment.AlwaysStartsWithFavorite` (`DeviceIdiom.TV`) fa partire sempre la preferita; senza preferiti la prima della lista. Provato: preferito = Radio Cartoni → dopo il riavvio parte quella; tolto il preferito → parte Radio E45 (la prima) |
| Righe larghe | Con scala 1,08 una riga da ~1000 dp esce di 40 dp per lato e il bordo scompare: `TvRowButton` ha scala 1 e bordo di 6 dp (canali, palinsesto, risultati, richieste, podcast, episodi) |

### Misure di T6 (2026-10-23, rifinitura)

| Verifica | Esito |
|---|---|
| Mappa del focus | `tv-focus-probe.sh tour` su On Air (8 controlli) e su Channels (stazioni, modifica, elimina, Aggiungi, Aggiorna): tutte le etichette raggiungibili, nessuna perdita di focus (la voce `none` del primo giro non si è ripetuta con `TRACE=1`) |
| Lingue | Italiano e polacco forzati con un file `lang.txt` letto da una build di prova (poi tolta): la fila di sezioni, il lettore, Richieste e Canali entrano nello schermo. L'etichetta «Offline» era tagliata («Off…»): la riga della stazione (larghezza minima 560 dp) più modifica, elimina ed etichetta superava lo spazio; ora 440 dp |
| Lista lunga | 370 righe di richieste (`BindableLayout` in uno `ScrollView`): scorrimento col D-pad senza frame lenti (0 su 297, 99° percentile 14 ms); memoria da 265 a 334 MB di PSS all'apertura (circa 190 KB per riga); l'apertura richiede alcuni secondi. Le richieste sono a pagine da 10, quindi in pratica non succede; l'elenco episodi non è paginato, ma un podcast ha di norma poche decine di episodi |
| Sessione multimediale | `dumpsys media_session`: sessione Media3 attiva con titolo e artista e azioni di play, pausa e avanti; la scheda della home TV si guarda sul Sony (T7) |
| Server locale | Radio Casa (`192.168.1.71`) non era raggiungibile dall'emulatore durante la sessione (`EHOSTUNREACH`, il Mac lo raggiungeva): le prove sono state fatte con AzuraTest e Radio E45 |

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
| `Tv/TvNowPlayingView` (stazione corrente, controlli play/pausa, avanti/indietro, accesso a Schedule; il volume è del televisore, `onair.volume` è `n/a`) | `OnAirPage` | `OnAirViewModel` |
| `Tv/TvStationsView` (sezione **Stazioni**, solo TV: lista con selezione, focus iniziale sulla stazione corrente, azioni di gestione) | `RadioListPage` | `RadioListViewModel` |
| `Tv/TvScheduleDialog` (palinsesto leggibile da lontano, scorrimento col D-pad) | `SchedulePopup` | `ScheduleViewModel` |
| `Tv/TvRequestView` (richiesta brani, come sul telefono) | `SongRequestPage` | `SongRequestViewModel` |
| `Tv/TvPodcastListView` | `PodcastListPage` | `PodcastListViewModel` |
| `Tv/TvPodcastEpisodesView` | `PodcastEpisodesPage` | `PodcastEpisodesViewModel` |
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

**Per la TV non si cerca la parità, ma la copertura dichiarata:** ogni feature ha una decisione esplicita (`done`, `limited`, `todo` ancora da fare, `n/a` per sempre con il motivo). Il sistema serve a non perdere funzioni per distrazione, non a replicare tutto il telefono. Un `n/a` motivato non è debito.

### 5.1 Livelli
1. **Architettura:** la logica non diverge perché sta nei ViewModel unici (regola 3).
2. **Manifest** (`parity/features.yaml`): nuova chiave `status.tv` (`done | todo | limited | n/a`) e campo opzionale `tvNotes`, obbligatorio quando `tv` è `limited` o `n/a` (il campo `notes` esistente parla di desktop e web, quindi non si mescola).
3. **Verifica automatica** (`tools/ParityCheck`), estensione prevista in T1:
   - `FeatureStatus.Tv` e `Feature.TvNotes`; `PageMap.Tv` (percorso della pagina TV oppure `n/a` + `tvReason`);
   - stesso confronto dei binding e delle chiavi di stringa tra pagina MAUI e pagina TV, con `ignore` per i membri solo-touch (swipe, trascinamento, pull-to-refresh);
   - copertura: ogni file in `RadioE45/Views/Tv/*.xaml` deve comparire in `pages.yaml`;
   - debito: `tv: todo` è debito dichiarato, ma **solo** `todo`: `n/a` e `limited` non lo sono. Senza opzioni il debito TV è una riga di riepilogo; `--include-tv` lo elenca feature per feature;
   - gate: `--strict` resta quello dell'app Uno (desktop e web) e ignora `tv`; `--strict-tv` è il gate della TV, indipendente dal precedente: fallisce finché resta un `tv: todo`, cioè finché ogni feature non è decisa (`done`, `limited` o `n/a` con motivo);
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
- [x] Uscita: `ParityCheck` restituisce 1 (solo debito dichiarato), `dotnet test` verde (277), `--strict` invariato (solo `settings.diagnostics.crash-reports`), `--strict-tv` conta i `tv: todo`
- Regola aggiunta in T1: una feature **senza ViewModel** (piattaforma, es. `tv.launcher`) può essere `done` su tv anche se il file della pagina TV non esiste ancora; per le altre serve il file.

### T2 — Spike: rilevamento TV, pagina radice, focus · Mac + emulatore — ✅ completata 2026-10-22
- [x] Log che conferma `DeviceIdiom.TV` sull'emulatore (`[TV] DeviceInfo.Idiom=TV root=TvRootPage`); `App.CreateWindow` sceglie la radice in base al form factor
- [x] `TvRootPage` con la fila di pulsanti (On Air, Channels, Request con la capability) e un lettore minimo; stato `Focused` visibile (`Resources/Styles/TvStyles.xaml`)
- [x] `tools/tv-focus-probe.sh` (§2.3) e `docs/parity/MAUI-TO-TV.md`
- [x] Tasti media e Indietro verificati (§3). `TvKeepFocusBehavior` per il focus perso dai comandi asincroni
- [x] Uscita: la mappa del focus raggiunge tutti i controlli; perimetro confermato (C allargata, §1)
- Rinviato a T4: dialogo dei termini d'uso con focus iniziale sul pulsante (non è nella pagina TV)
- Da provare: regressione del telefono (la `Shell` non deve cambiare); build `dotnet build RadioE45.slnx` verde

### T3 — Navigazione a pulsanti, lettore e selezione stazioni · Mac + emulatore — ✅ completata 2026-10-22
- [x] `TvRootPage` come contenitore: fila di pulsanti focusabili (On Air, Channels; Request e Podcast in T5), sezione corrente sottolineata, area sezione, dialogo proprio. Registra un `ITvHost` (`Views/Tv/ITvHost.cs`)
- [x] `MauiNavigationService` e `MauiDialogService` usano l'`ITvHost` quando non c'è la Shell (`GoToOnAirAsync`, `ConfirmAsync`, `AlertAsync`): i ViewModel del telefono restano invariati
- [x] `TvNowPlayingView`: copertina, badge LIVE, stazione, ascoltatori, HLS, titolo, artista, avanzamento, errore, play/pausa con buffering
- [x] `TvStationsView`: elenco (BindableLayout in ScrollView), puntino sulla stazione in ascolto, focus iniziale sulla stazione corrente, OK seleziona e torna a On Air
- [x] `TvDialogOverlay` (Controls): dialogo con focus iniziale sul pulsante principale, testo scorrevole, focus intrappolato, Indietro annulla
- [x] **Primo avvio** (installazione pulita): Termini d'uso → domanda sulle stazioni di prova → riproduzione. Termini d'uso anticipati da T4
- [x] Indietro: dialogo → chiude; sezione diversa da On Air → torna a On Air; On Air → app in background (`MainActivity.OnBackPressed`)
- [x] Parità aggiornata (13 feature `tv` portate a `done` o `limited`); `ParityCheck` a 1, solo debito dichiarato
- [x] Uscita: la mappa del focus raggiunge On Air, Channels e le due stazioni; selezione e riproduzione verificate sull'emulatore

### T4 — Controlli, Schedule e gestione stazioni · Mac + emulatore — ✅ completata 2026-10-23
- [x] Play/pausa e avanti/indietro stazione sul lettore (il volume resta del televisore); tasti media del telecomando verso la sessione Media3 (già verificati in T2); cuore del preferito accanto al nome della stazione
- [x] `TvScheduleView`: palinsesto a schermo intero, una riga focusabile per fascia, scorrimento col D-pad; Indietro o Chiudi tornano a On Air. Il pulsante **Schedule** sul lettore compare solo se la stazione ha un palinsesto
- [x] Gestione stazioni dalla sezione Channels: pulsanti Aggiungi e Aggiorna sopra l'elenco, matita e cestino a destra di ogni stazione; `TvAddStationView` (ricerca, risultati spuntabili, Aggiungi/Annulla), `TvEditStationView` (nome, nome breve, descrizione, info personalizzate, Annulla/Salva), eliminazione con conferma nel dialogo della TV; tastiera su schermo
- [x] Dialogo dei termini d'uso solo al primo utilizzo, con focus iniziale sul pulsante (anticipato e fatto in T3)
- [x] Stati di errore e offline: errore del server nella ricerca, errore dell'elenco e dei form in rosso a 22 pt; etichetta «Offline» sulle stazioni spente. L'etichetta Offline non è stata vista sull'emulatore (le stazioni di prova sono online)
- [x] Uscita: mappa del focus completa su On Air, Channels e form di modifica; schedule, aggiunta (con ricerca reale), eliminazione e modifica provate col solo D-pad. **Non provati:** salvataggio di una modifica, Schedule con un live in corso, stazione offline

### T5 — Richieste e podcast · Mac + emulatore
- [x] `TvRequestView` con `SongRequestViewModel`; pulsante **Request** visibile solo con la capability (`NowPlaying.RequestsEnabled`)
- [x] Pulsante **Podcast** visibile solo con la capability
- [x] Verifica del servizio che riproduce gli episodi su Android e convivenza con `RadioPlaybackService` (prima di costruire la UI)
- [x] `TvPodcastListView`, `TvPodcastEpisodesView`, mini player con seek da D-pad (±30 s, `SeekByCommand`); filtri come pulsanti che ciclano le opzioni (`Cycle*Command`)
- [x] Uscita: ascolto di un episodio dall'emulatore, ripresa dalla posizione salvata, parità aggiornata

### T6 — Rifinitura 10 piedi · Mac + emulatore
- [x] Brano successivo (`onair.next-track`): copertina piccola (36 dp, al posto di un'etichetta) e una riga «titolo – artista» sotto i tempi nella colonna delle informazioni, solo se il server lo pubblica; verificato con titolo su due righe e testo lungo (troncato con i puntini). Cronologia dei brani (`onair.song-history`): `n/a`, non si visualizza (decisione 2026-10-23)
- [x] Stringhe in it/en/pl, dimensioni testo, margini di sicurezza, immagini, tema (schermate in italiano e polacco riviste; corretta l'etichetta «Offline» tagliata)
- [x] Prestazioni su lista lunga e immagini, uso memoria (misure in §3)
- [x] Triage finale dei `tv: todo` rimasti: ognuno diventa `done`, `limited` o `n/a` con `tvNotes` (la parità non è l'obiettivo, §5)
- [x] Uscita: revisione degli screenshot, nessun elemento non focusabile, `ParityCheck --strict-tv` senza errori

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
- [ ] `docs/STORE-DISTRIBUTION.md` e `docs/RELEASE.md` aggiornati; `--strict-tv` verde (nessun `todo` residuo)
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
- **2026-10-23:** il brano successivo si visualizza (copertina piccola e una riga di testo sotto i tempi), la cronologia dei brani no.

## 9. Registro delle sessioni

| Data | Sessione | Esito |
|---|---|---|
| 2026-10-18 | Analisi | Fattibilità e azioni in `ANDROID-TV.md` |
| 2026-10-22 | T0 | Manifest TV, banner, installazione ed esame dell'emulatore; opzione A scartata; piano e parità TV progettati |
| 2026-10-22 | Decisioni | Niente impostazioni né flyout su TV; T8 (Fire TV) rinviata |
| 2026-10-22 | Decisioni | Sezione Stations solo-TV per selezione e gestione; Schedule inclusa; termini d'uso solo al primo utilizzo |
| 2026-10-22 | Decisioni | Navigazione a pulsanti focusabili (On Air, Stations sempre; Request, Podcast con la capability), niente barra di tab; Richieste incluse |
| 2026-10-22 | T1 | Piattaforma `tv` in features/pages, `ParityCheck --include-tv`, 12 test, skill `tv-parity-port`, hook. Inventario: 44 `todo`, il resto `n/a` con motivo o `done` (launcher) |
| 2026-10-22 | T1 (revisione) | La TV non cerca la parità ma la copertura dichiarata: gate `--strict-tv` indipendente da `--strict`; triage di 3 `todo` in `n/a` (VU meter, link personalizzati, ritardo per stazione): restano 41 `todo` |
| 2026-10-22 | T2 | Spike: `DeviceIdiom.TV` → `TvRootPage`, fila di pulsanti con focus, `tools/tv-focus-probe.sh`, `TvKeepFocusBehavior`, `docs/parity/MAUI-TO-TV.md`; tasti media e Indietro verificati |
| 2026-10-22 | T3 | `TvRootPage` contenitore + `ITvHost`, `TvNowPlayingView`, `TvStationsView`, `TvDialogOverlay`, `TvFocus`; primo avvio (termini → stazioni di prova → riproduzione), selezione e Indietro verificati |
| 2026-10-23 | T4 | Cambio stazione e preferito sul lettore, `TvScheduleView`, gestione stazioni (`TvAddStationView`, `TvEditStationView`, azioni nella lista), `ITvHost.GoBack/ShowEditStation`; ParityCheck legge anche `<Binding Path=…/>`
| 2026-10-23 | T5 | `TvRequestView`, `TvPodcastListView`, `TvPodcastEpisodesView`; pulsanti Request/Podcast con la capability; `PodcastEpisodesViewModel` con `SeekBy` e `Cycle*`; `ITvSection.OnHiddenAsync`; `TvRowButton`; servizio podcast verificato; la TV non si annuncia nel changelog prima del rilascio (`ANDROID-TV-CHANGELOG-PENDING.md`) |
| 2026-10-23 | T6 | Brano successivo su TV (copertina e riga sotto i tempi), cronologia `n/a`; schermate it/pl riviste, «Offline» non più tagliato; prestazioni misurate; `shell.system-media-controls` e `tv.focus` chiusi; `--strict-tv` senza errori |

---

## 10. Riferimenti
- [`ANDROID-TV.md`](ANDROID-TV.md): fattibilità, stato iniziale, rischi
- [`LINUX-UNO-ROADMAP.md`](LINUX-UNO-ROADMAP.md): modello del sistema di parità (§6) e del processo
- [`STORE-DISTRIBUTION.md`](STORE-DISTRIBUTION.md), [`RELEASE.md`](RELEASE.md), [`VERSION-BUMP.md`](VERSION-BUMP.md)
- Android TV: linee guida di qualità e requisiti Play per app TV (rileggerli in T9)
