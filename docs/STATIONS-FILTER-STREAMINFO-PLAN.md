# Piano: filtro e vista a icone nella lista stazioni, Stream info in On Air

Stato: proposta (2026-10-30). Nessun codice scritto.

Due modifiche indipendenti. Ordine consigliato: A, poi B, in sessioni separate.

## Punti di partenza

- Comportamento, stato e testi stanno nel Core: `RadioListViewModel`, `OnAirViewModel`. Le pagine fanno solo layout e binding.
- `RadioListViewModel.Stations` è una `ObservableCollection<AzuraStation>` ricostruita da `RefreshStationsFromCatalog`.
- Lista MAUI: `CollectionView` con righe a card (`RadioE45/Views/RadioListPage.xaml`). Uno: `ListView`. TV: `TvStationsView`, che resta su `Stations`.
- Il nome radio in On Air è il badge `CurrentStation.Name` in `RadioE45/Views/OnAirPage.xaml`. Oggi non ha tap.
- `AzuraCastMountPoint` legge solo `path`, `is_default`, `url`: mancano `bitrate` e `format`.
- L'API AzuraCast non fornisce frequenza di campionamento, canali e genere. Li espone lo stream Icecast negli header: `ice-audio-info` (samplerate, bitrate, channels), `icy-genre`, `Content-Type`.

## Parte A: lista stazioni

### Core (`RadioListViewModel`)

- `FilterText`, `HasFilter`, `ClearFilterCommand`.
- `FilteredStations`, derivata da `Stations` e da `FilterText`. Ricerca solo sul nome (decisione dopo la prova: cercare anche nella descrizione dava risultati poco intuitivi con una sola lettera), senza distinguere maiuscole e accenti.
- `ViewMode` (`List` / `Grid`), salvato tramite `ISettingsStore` / `AppSettings`, con `SetViewModeCommand` e proprietà `IsListMode` / `IsGridMode`.
- Riordino a trascinamento disattivato quando il filtro è attivo (gli indici di `Stations` e `FilteredStations` non coincidono).
- Il filtro si azzera quando si lascia la pagina (da confermare).
- Stringhe in `RadioE45.Core/Resources/Strings/` (neutro italiano, `en`, `pl`): placeholder filtro, Cancella filtro, Elenco, Icone grandi, Nessun risultato.

### MAUI

- Barra in alto: casella di testo con pulsante X (visibile se `HasFilter`) e due pulsanti per la vista.
- `CollectionView` esistente su `FilteredStations`.
- Vista icone: secondo `CollectionView` con `GridItemsLayout`, span adattivo calcolato in code-behind su `SizeChanged` (solo layout). Tile con logo grande, nome, cuore preferito, badge HLS, stato offline, indicatore attiva.
- Messaggio "Nessun risultato" nell'`EmptyView` quando il filtro non trova nulla.

### Uno (desktop e web)

- `TextBox` con `{x:Bind ViewModel.FilterText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}` e pulsante X.
- `ListView` per l'elenco, `GridView` con `ItemsWrapGrid` per le icone, `ToggleButton` per cambiare vista.
- F5 e menu contestuale invariati. Conversioni in `docs/parity/MAUI-TO-UNO.md`.

### TV

- `radiolist.filter` e `radiolist.view-mode`: `tv: n/a` con `tvNotes` (scrivere col telecomando non è pratico, le icone grandi non servono).
- `TvStationsView` non cambia.

### Parità, test, changelog

- `parity/features.yaml`: nuove feature `radiolist.filter` e `radiolist.view-mode` (maui, desktop, web `done`; tv `n/a`).
- `parity/pages.yaml`: aggiornare i binding nuovi; `dotnet run --project tools/ParityCheck -- --update-surface`.
- Test in `RadioE45.Core.Tests`: filtro (accenti, vuoto, nessun risultato), persistenza della vista, riordino disabilitato col filtro.
- `CHANGELOG.md`, sotto `## [Unreleased]` / `New`, riga in inglese.

## Parte B: Stream info nella pagina On Air

### Core

- Estendere `AzuraCastMountPoint` con `bitrate` e `format`.
- Nuovo `IStreamInfoProvider` (stesso stile di `StreamUrlProber`, con `IHttpClientFactory`): GET con `HttpCompletionOption.ResponseHeadersRead` sull'URL in uso, lettura degli header, chiusura immediata, timeout 3 s.
- Modello `StreamInfo { Address, Format, Bitrate, SampleRate, Channels, Genre }`. Parte dai dati API e li integra con gli header. I valori mancanti restano nulli e la riga non si mostra.
- Stream HLS: formato "HLS", niente bitrate per ora.
- `StreamInfoViewModel` con elenco di righe etichetta/valore.
- `OnAirViewModel.ShowStreamInfoCommand`, invocato dal tap sul nome della stazione.
- Stringhe `.resx`: Stream info, Station, Address, Format, Bitrate, Sample rate, Channels, Genre.

### MAUI e Uno

- Stesso schema dello Schedule: `StreamInfoPopup` in MAUI, `StreamInfoDialog` (`ContentDialog`) in Uno.
- Intestazione con colore `Accent`, icona info, titolo e X di chiusura; sotto le righe etichetta piccola / valore. Riferimento: screenshot Stream info fornito.
- Tap sul badge nome (`TapGestureRecognizer` in MAUI, `Tapped` in Uno) più una piccola icona ⓘ accanto, per renderlo scopribile.

### TV

- Proposta `tv: n/a` con `tvNotes`. In alternativa un dialogo dal tasto OK sul nome (da decidere).

### Parità, test, changelog

- Nuova feature `onair.stream-info` in `parity/features.yaml`; aggiornare `pages.yaml` e `vm-surface.txt`.
- Test: parsing di `ice-audio-info`, righe omesse, ripiego sui dati API.
- Riga `New` in `CHANGELOG.md`, in inglese.

## Rischi

- **Web (WASM):** il browser nasconde gli header senza `Access-Control-Expose-Headers`. Su web probabilmente solo formato e bitrate (dall'API); le altre righe spariscono senza errore.
- **Connessione extra:** la GET apre per un istante un ascoltatore Icecast. Farla solo all'apertura del dialogo.
- **Genere:** presente solo se configurato sul mount Icecast; spesso mancherà.
- **Riordino:** deve restare corretto in lista e in griglia, solo senza filtro.

## Decisioni da confermare

1. Il filtro si azzera quando si lascia la pagina?
2. TV `n/a` per entrambe le modifiche?
3. Frequenza, canali e genere dagli header Icecast, con righe omesse se mancano?

## Chiusura di ogni parte

Per regola di progetto: LSP e `dotnet build` dopo ogni modifica `.cs`; `dotnet test --project RadioE45.Core.Tests/RadioE45.Core.Tests.csproj`; `ParityCheck` con exit 0 o 1. I messaggi di commit si propongono come testo in inglese, con trailer `Parity: maui, desktop, web`. Nessun commit automatico.
