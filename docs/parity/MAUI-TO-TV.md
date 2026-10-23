# Conversione MAUI telefono → viste Android TV

Guida per `tv-parity-port` e per chi scrive le viste in `RadioE45/Views/Tv/`. Piano e regole: [`../ANDROID-TV-ROADMAP.md`](../ANDROID-TV-ROADMAP.md). Ogni punto «misurato» è stato verificato sull'emulatore `Television_1080p` (Android 12, 1920x1080 a 320 dpi) il 2026-10-22.

## 1. Principio

La TV è la stessa app MAUI Android. Le viste TV sono `ContentPage` in `RadioE45/Views/Tv/`, legate agli stessi ViewModel del Core. Cambia solo il layout e il modo di raggiungere i controlli (D-pad al posto del tocco). Nessuna logica nelle viste, nessun `#if` nel XAML.

- **Scelta della radice:** `App.CreateWindow` usa `DeviceInfo.Current.Idiom == DeviceIdiom.TV` per creare `TvRootPage` invece di `AppShell`. Misurato: sull'emulatore l'idioma è `TV` e la pagina creata è `TvRootPage`. Gli altri dispositivi continuano a creare `AppShell` (non ancora eseguito sul telefono: da provare).
- **Niente `Shell` su TV:** su TV non c'è `Shell.Current`. `MainActivity.OnBackPressed` lo gestisce già (con `Shell.Current` nullo manda l'app in background).
- **Audio:** il player sta nel servizio Media3 e la pagina TV non ha `MediaElement`. Come `OnAirPage`, la pagina chiama `IMediaElementHost.Initialize` per collegare il `MediaController`, qui con `null` e sotto `#if ANDROID` (su Android l'argomento è ignorato).

## 1b. Contenitore, sezioni e servizi (T3)

- **`TvRootPage`** è il contenitore: fila di pulsanti in alto, `ContentView` della sezione sotto, `TvDialogOverlay` sopra a tutto. Le sezioni sono `ContentView` (`TvNowPlayingView`, `TvStationsView`) con il ViewModel iniettato dalla DI e implementano `ITvSection` (`OnShownAsync` per caricare i dati, `OnHiddenAsync` per fermare ciò che la sezione sta facendo, `FocusDefault` per il focus iniziale).
- **`ITvHost`** (`Views/Tv/ITvHost.cs`) è ciò che `TvRootPage` offre ai servizi: `ShowSection`, `ConfirmAsync`, `AlertAsync`, `HandleBack`. `TvHostAccessor` (singleton) lo tiene; è nullo su ogni altro dispositivo.
- **Servizi di piattaforma:** `MauiNavigationService` e `MauiDialogService` usano l'host quando c'è (su TV `Shell.Current` è nullo e prima `GoToOnAirAsync` non faceva nulla e `ConfirmAsync` rispondeva sempre «no»). I ViewModel non cambiano: `RadioListViewModel` chiede la conferma delle stazioni di prova e torna a On Air attraverso gli stessi servizi.
- **Sezioni e sottosezioni (T4):** `TvSection` ha le sezioni della fila (`OnAir`, `Stations`) e le sezioni `Request` e `Podcasts` (con pulsante, visibili solo con la capability: `NowPlaying.RequestsEnabled`, `HasPodcasts`) e le sottosezioni `Schedule`, `AddStation`, `EditStation`, `PodcastEpisodes`, che non hanno un pulsante: il pulsante del genitore resta sottolineato. `ITvHost.GoBack()` torna al genitore (Schedule → On Air, Aggiungi/Modifica → Stations, Episodi → Podcast); `MauiNavigationService` lo usa per `GoBackAsync`, `GoToAddStationAsync`, `GoToEditStationAsync(id)` e `ShowScheduleAsync`, così i ViewModel del telefono restano invariati. Le viste vivono quanto `TvRootPage`: ogni visita deve ripartire pulita (`AddStationViewModel.Reset()`, `EditStationViewModel.ReloadAsync()`).
- **Una sezione nuova** = una `ContentView` in `Views/Tv/`, un valore in `TvSection`, un pulsante nella fila (o una sottosezione), un percorso `tv` in `parity/pages.yaml`. Gli XAML in `Views/Tv/` devono essere tutti mappati; i componenti senza pagina gemella (come `TvDialogOverlay`) stanno in `Controls/`.
- **Indietro:** `MainActivity.OnBackPressed` chiede prima a `ITvHost.HandleBack()`: chiude il dialogo, o torna a On Air; solo da On Air l'app va in background.

## 2. Layout

| Cosa | Valore |
|---|---|
| Pagina logica | 960x540 dp (1920x1080 px a 320 dpi) |
| Margini di sicurezza (overscan) | 48 dp ai lati, 27 dp sopra e sotto (`TvSafeMarginX`, `TvSafeMarginY` in `TvStyles.xaml`; oggi applicati come `Padding="48,27"`) |
| Testo principale | 40 (titolo), 32 (brano), 26 (artista); pulsanti 22; icone 36 |
| Bersaglio minimo di un controllo | altezza 56 dp |
| Stili | `Resources/Styles/TvStyles.xaml`, unito solo alle pagine TV (non tocca il telefono) |

Colori e temi: stesse risorse dinamiche del telefono (`PageBackground`, `CardElevated`, `PrimaryText`, `SecondaryText`, `Accent`).

## 3. Tabella di conversione

| Telefono | TV |
|---|---|
| Tab della `Shell` (On Air, Richieste, Podcast) | Fila di `Button` focusabili in alto: On Air e Stations sempre, Request e Podcast solo con la capability (`NowPlaying.RequestsEnabled`, `HasPodcasts`; in T3 ci sono On Air e Channels) |
| Menu laterale (flyout) e Impostazioni | Non esistono su TV (`n/a`) |
| Tocco su un controllo | OK del telecomando (`DPAD_CENTER`) = click del `Button` |
| Swipe, trascinamento, pull to refresh | Non esistono (`n/a` o azione su un pulsante focusabile) |
| Pulsante flottante | `Button` focusabile nel flusso della pagina |
| Pannello dal basso (popup) | Finestra a schermo intero con scorrimento col D-pad |
| `Entry` | `Entry` con stile `TvEntry` (sfondo `Accent` con il focus); OK sul campo apre la tastiera su schermo di Android TV, il focus iniziale no; `ReturnType="Search"` + `ReturnCommand` per «Cerca» (misurato in T4) |
| `CheckBox` | `Button` con `ImageSource` da `DataTrigger` (quadrato con segno / quadrato) e un comando che inverte il valore (`ToggleSelectedCommand`, `ToggleCustomInfoCommand`) |
| `CollectionView` di sola lettura (palinsesto) | `ScrollView` + `BindableLayout` di `Button` focusabili: senza un elemento focusabile il D-pad non scorre |
| Swipe / menu contestuale (modifica, elimina) | Matita e cestino: `Button` con glifo `FaSolid` a destra della riga, con `SemanticProperties.Description` |
| Emoji come icone (▶ ⏸) | Glifi del font `FaSolid` (`&#xF04B;` play, `&#xF04C;` pausa) |

**Emoji:** su Android l'emoji ⏸ si disegna a colori (arancione) e il selettore di presentazione testuale `U+FE0E` viene ignorato. Misurato. Usare i glifi `FaSolid`, come fa `MenuButton`.

## 4. Focus

- **Stato visivo:** ogni `Button` TV usa lo stile `TvButton`, con stato `Focused` (scala 1,08, bordo 4 dp, sfondo `Accent`). Misurato: il `VisualStateManager` di MAUI riceve il focus del D-pad. Senza stile il focus non si vede.
- **Focus iniziale:** impostarlo da `Loaded` con `Dispatcher.Dispatch(() => controllo.Focus())`. In `OnAppearing` la vista nativa non è ancora agganciata e il focus non si imposta. Il controllo principale della pagina è quello da cui parte il telecomando.
- **Comandi asincroni:** un `Button` con un comando in esecuzione è disabilitato e Android gli toglie il focus (misurato: dopo OK su Play il focus saltava su «On Air» e non tornava). Aggiungere `TvKeepFocusBehavior` (`RadioE45/Controls`) ai pulsanti il cui comando è asincrono:

  ```xml
  <Button Style="{StaticResource TvButton}" Command="{Binding PlayPauseCommand}">
      <Button.Behaviors>
          <controls:TvKeepFocusBehavior />
      </Button.Behaviors>
  </Button>
  ```

- **Touch mode:** subito dopo un avvio a freddo la finestra è in touch mode e `Focus()` viene rifiutato (il pulsante non è `focusableInTouchMode`; `Focus()` risponde comunque `True`). Usare sempre `TvFocus.Request(controllo)` (`RadioE45/Controls/TvFocus.cs`), che chiama `RequestFocusFromTouch` su Android. Misurato.
- **Dopo aver mostrato una vista nascosta** (il dialogo) il focus va richiesto più volte a breve distanza finché si aggancia: `TvDialogOverlay` riprova fino a 6 volte.
- **Controlli che si ingrandiscono:** lo stato `Focused` scala il controllo di 1,08; i contenitori (`ScrollView`, `Border`) lo tagliano. Lasciare padding intorno (16 dp per le liste, 14 dp per i pulsanti del dialogo). Misurato.
- **Intrappolare il focus in un dialogo:** disabilitare il resto della pagina (`Chrome.IsEnabled = false`) finché il dialogo è aperto, e ripristinare il focus sulla sezione alla chiusura.
- **Etichette per la mappa del focus:** `tools/tv-focus-probe.sh` legge il testo del controllo. Un pulsante con solo un'icona (glifo) ha etichetta vuota: dargli `SemanticProperties.Description`.
- **Verifica:** `tools/tv-focus-probe.sh tour --expect "On Air,Channels,Radio E45"`. Ogni controllo azionabile della pagina deve comparire tra i raggiunti. Il tour esplora il grafo del focus (non per righe: `SINISTRA` da una lista salta alla fila di pulsanti in alto) e dura circa un minuto per pagina.
- **Margini di sicurezza anche per i dialoghi:** la scheda del dialogo deve stare dentro 27 dp sopra e sotto; l'area scorrevole del messaggio è limitata a 230 dp.

## 4b. Liste e binding (T3, T4)

- **Liste:** `ScrollView` con `VerticalStackLayout` e `BindableLayout.ItemsSource`; ogni riga è un `Button` con stile `TvButton` (focusabile e con stato `Focused`). Android fa scorrere lo `ScrollView` fino al controllo che prende il focus. Va bene per elenchi brevi (le stazioni); una `CollectionView` non è stata provata col D-pad.
- **Elemento attivo:** non usare `MultiBinding` con i convertitori del Toolkit nel testo di un pulsante: lancia `targetType needs to be assignable from System.String` alla creazione dei figli e l'eccezione viene inghiottita dal `SafeExecuteAsync` del ViewModel, lasciando la lista vuota senza errori visibili (misurato). Usare un `DataTrigger` che imposta `ImageSource` (`FontImageSource`, glifo `FaSolid`).
- **Il caricamento del XAML avviene a runtime:** un errore nel XAML (comportamento sul tipo sbagliato, converter con tipo di destinazione sbagliato) non rompe la build e fa chiudere l'app all'apertura della vista. Guardare `logcat` (`FATAL EXCEPTION`, `InitializeComponent`). Due casi misurati in T4: `TvKeepFocusBehavior` va su `VisualElement` (serve anche per `ImageButton`), e un `BoolToObjectConverter` su una proprietà `ImageSource` non deve avere `x:TypeArguments="x:String"`.
- **Pulsanti larghi:** per le righe di un elenco usare `TvRowButton` (scala 1, bordo 6 dp). Con lo stile `TvButton` (scala 1,08) una riga da ~1000 dp cresce di 40 dp per lato, esce dallo `ScrollView` e il bordo sparisce (misurato in T4, corretto in T5). `TvButton` resta per i pulsanti piccoli.
- **Sintomo da riconoscere:** lista vuota senza il messaggio di lista vuota → leggere `ErrorMessage` del ViewModel.

- **Menu a tendina, slider, medie:** niente `Picker` né `Slider` col telecomando: il filtro è un pulsante che passa all'opzione successiva (`Cycle*Command` nel ViewModel), il seek è `±30 s` (`SeekByCommand`). Il comando deve stare nel ViewModel (regola 3), non nel code-behind.
- **Focus e layout che cambia:** quando un elemento compare o sparisce (mini player) Android toglie il focus alla vista che lo ha causato: va riassegnato esplicitamente (`TvPodcastEpisodesView`).
- **`MediaElement` nella vista TV:** il servizio podcast la riceve con `IMediaElementHost.Initialize` da `Loaded`, ogni volta che la vista si riattacca; la radio non la usa (Media3).
- **`CommandParameter` numerico:** `<Button.CommandParameter><x:Double>30</x:Double></Button.CommandParameter>`; una stringa non converte in `double`.

- **Larghezza delle righe con elementi accanto:** somma i bordi (pulsante + modifica + elimina + etichetta) e confrontala con lo spazio dello `ScrollView` (960 dp meno margini di sicurezza e padding): l'etichetta «Offline» era tagliata. Provare sempre in italiano e polacco, che hanno parole più lunghe.
- **Liste lunghe:** `BindableLayout` crea tutte le righe (370 righe ≈ +70 MB e alcuni secondi all'apertura, scorrimento fluido); per elenchi che possono superare qualche centinaio di righe serve la paginazione, come nelle richieste.

## 4c. Primo avvio

Su installazione pulita l'ordine è lo stesso di `AppShell`: `ActivateAsync` → Termini d'uso (una volta) → se non ci sono stazioni, sezione Channels, che chiede se aggiungere quelle di prova → riproduzione. Per provarlo sull'emulatore: `adb shell pm clear com.radioe45.app`, poi avviare l'app.

## 5. Tasti del telecomando

| Tasto | Comportamento | Stato |
|---|---|---|
| Play/pausa, Pausa, Play (`KEYCODE_MEDIA_*`) | Comandano la sessione Media3 | Misurato, nessun codice dedicato |
| Avanti, Indietro (`KEYCODE_MEDIA_NEXT/PREVIOUS`) | Cambiano stazione | Misurato: il brano passa da RadioE45 ad AzuraTest e ritorna |
| Indietro (`KEYCODE_BACK`) dalla radice | L'app va in background e l'audio continua; la riapertura riporta l'app in primo piano | Misurato |
| Indietro in una sottosezione | Schedule → On Air; Aggiungi/Modifica → Channels; con la tastiera aperta la chiude soltanto | Misurato (T4) |
| D-pad e OK | Spostano il focus e attivano il controllo | Misurato |

## 6. ParityCheck

- `pages.yaml`: campo `tv` con il percorso della pagina, oppure `n/a` + `tvReason`; membri del telefono assenti su TV in `ignoreTv`, ciascuno con il motivo.
- `features.yaml`: `status.tv` e `tvNotes`. Su TV `n/a` motivato è una risposta definitiva.
- Comandi: `dotnet run --project tools/ParityCheck -- --include-tv` e, a fine progetto, `--strict-tv`.
