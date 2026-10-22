# Conversione MAUI telefono → viste Android TV

Guida per `tv-parity-port` e per chi scrive le viste in `RadioE45/Views/Tv/`. Piano e regole: [`../ANDROID-TV-ROADMAP.md`](../ANDROID-TV-ROADMAP.md). Ogni punto «misurato» è stato verificato sull'emulatore `Television_1080p` (Android 12, 1920x1080 a 320 dpi) il 2026-10-22.

## 1. Principio

La TV è la stessa app MAUI Android. Le viste TV sono `ContentPage` in `RadioE45/Views/Tv/`, legate agli stessi ViewModel del Core. Cambia solo il layout e il modo di raggiungere i controlli (D-pad al posto del tocco). Nessuna logica nelle viste, nessun `#if` nel XAML.

- **Scelta della radice:** `App.CreateWindow` usa `DeviceInfo.Current.Idiom == DeviceIdiom.TV` per creare `TvRootPage` invece di `AppShell`. Misurato: sull'emulatore l'idioma è `TV` e la pagina creata è `TvRootPage`. Gli altri dispositivi continuano a creare `AppShell` (non ancora eseguito sul telefono: da provare).
- **Niente `Shell` su TV:** su TV non c'è `Shell.Current`. `MainActivity.OnBackPressed` lo gestisce già (con `Shell.Current` nullo manda l'app in background).
- **Audio:** il player sta nel servizio Media3 e la pagina TV non ha `MediaElement`. Come `OnAirPage`, la pagina chiama `IMediaElementHost.Initialize` per collegare il `MediaController`, qui con `null` e sotto `#if ANDROID` (su Android l'argomento è ignorato).

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
| Tab della `Shell` (On Air, Richieste, Podcast) | Fila di `Button` focusabili in alto: On Air e Stations sempre, Request e Podcast solo con la capability (`NowPlaying.RequestsEnabled`, `HasPodcasts`) |
| Menu laterale (flyout) e Impostazioni | Non esistono su TV (`n/a`) |
| Tocco su un controllo | OK del telecomando (`DPAD_CENTER`) = click del `Button` |
| Swipe, trascinamento, pull to refresh | Non esistono (`n/a` o azione su un pulsante focusabile) |
| Pulsante flottante | `Button` focusabile nel flusso della pagina |
| Pannello dal basso (popup) | Finestra a schermo intero con scorrimento col D-pad |
| `Entry` | Tastiera su schermo di Android TV (da misurare in T4) |
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

- **Etichette per la mappa del focus:** `tools/tv-focus-probe.sh` legge il testo del controllo. Un pulsante con solo un'icona (glifo) ha etichetta vuota: dargli `SemanticProperties.Description`.
- **Verifica:** `tools/tv-focus-probe.sh tour --expect "On Air,Channels,Request"`. Ogni controllo azionabile della pagina deve comparire tra i raggiunti.

## 5. Tasti del telecomando

| Tasto | Comportamento | Stato |
|---|---|---|
| Play/pausa, Pausa, Play (`KEYCODE_MEDIA_*`) | Comandano la sessione Media3 | Misurato, nessun codice dedicato |
| Avanti, Indietro (`KEYCODE_MEDIA_NEXT/PREVIOUS`) | Cambiano stazione | Misurato: il brano passa da RadioE45 ad AzuraTest e ritorna |
| Indietro (`KEYCODE_BACK`) dalla radice | L'app va in background e l'audio continua; la riapertura riporta l'app in primo piano | Misurato |
| Indietro in una pagina interna | Risale di un livello | Da realizzare con le pagine di T3-T5 |
| D-pad e OK | Spostano il focus e attivano il controllo | Misurato |

## 6. ParityCheck

- `pages.yaml`: campo `tv` con il percorso della pagina, oppure `n/a` + `tvReason`; membri del telefono assenti su TV in `ignoreTv`, ciascuno con il motivo.
- `features.yaml`: `status.tv` e `tvNotes`. Su TV `n/a` motivato è una risposta definitiva.
- Comandi: `dotnet run --project tools/ParityCheck -- --include-tv` e, a fine progetto, `--strict-tv`.
