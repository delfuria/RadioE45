# Podcast — piano e implementazione

Funzionalità: esplorazione e ascolto dei podcast pubblicati su AzuraCast per la stazione radio attualmente selezionata nell'app.

## API AzuraCast utilizzate

Verificate sul sorgente ufficiale (github.com/AzuraCast/AzuraCast, branch `main`), non su documentazione di terze parti. Sono gli unici endpoint podcast **pubblici** (`security: []`, nessuna API key richiesta) — gli endpoint `/station/{id}/podcasts` e `/station/{id}/podcast/{id}` senza `/public/` nel path richiedono invece un'API key di stazione e non sono usati.

| Endpoint | Uso |
|---|---|
| `GET /api/station/{station_id}/public/podcasts` | Lista podcast della stazione |
| `GET /api/station/{station_id}/public/podcast/{podcast_id}/episodes` | Lista episodi di un podcast |
| `GET /api/station/{station_id}/podcast/{podcast_id}/episode/{episode_id}/media` | Stream/download diretto dell'episodio (anch'esso pubblico) |

Campi JSON rilevanti mappati (vedi `App\Entity\Api\Podcast` e `App\Entity\Api\PodcastEpisode` nel repo AzuraCast):
- Podcast: `id`, `title`, `description`, `author`, `art` (URL assoluto), `episodes` (conteggio)
- Episode: `id`, `title`, `description`, `publish_at` (unix timestamp), `art`, `has_media`, `media.length` (durata in secondi)

## Architettura

Segue il pattern già in uso per il Palinsesto (`ScheduleService`/`ScheduleViewModel`): service stateless che accetta `AzuraStation` come parametro, non una stazione fissa — pronto per un'estensione futura "tutti i podcast di tutte le stazioni" senza refactor (basta iterare `IAzuraStationCatalog.Stations` e aggregare i risultati).

### File nuovi

**Modelli**
- `Models/AzuraCastPodcast.cs`
- `Models/AzuraCastPodcastEpisode.cs` (+ `AzuraCastPodcastMedia`)
- `Models/PodcastEpisodeProgress.cs` — tabella SQLite per resume/played

**Servizi**
- `Services/IAzuraCastPodcastApi.cs` — interfaccia Refit
- `Services/Radio/IPodcastService.cs` + `PodcastService.cs` — fetch podcast/episodi, stesso pattern di `ScheduleService`
- `Services/Data/IPodcastProgressRepository.cs` + `PodcastProgressRepository.cs` — CRUD posizione/completamento episodio
- `Services/Audio/IPodcastPlayerService.cs` + `PodcastPlayerService.cs` — player episodi (vedi sotto)

**ViewModel**
- `ViewModels/PodcastListViewModel.cs` — lista podcast della stazione corrente (letta da `OnAirViewModel.CurrentStation`, singleton condiviso)
- `ViewModels/PodcastEpisodesViewModel.cs` — lista episodi + stato mini-player (play/pause, seek, posizione)

**UI**
- `Views/PodcastListPage.xaml(.cs)` — nuovo tab "Podcast"
- `Views/PodcastEpisodesPage.xaml(.cs)` — lista episodi + mini-player con seek bar
- `Resources/Images/tab_podcast.svg` — icona tab

### File modificati
- `Services/Data/DatabaseService.cs` — `CreateTableAsync<PodcastEpisodeProgress>()` (tabella nuova, nessuna migrazione ALTER necessaria)
- `MauiProgram.cs` — registrazione DI di servizi/viewmodel/pagine
- `AppShell.xaml` / `AppShell.xaml.cs` — tab "Podcast" in TabBar, route `PodcastEpisodesPage`
- `ViewModels/OnAirViewModel.cs` — `StartPlayAndNowPollingAsync` ferma un episodio podcast eventualmente attivo prima di avviare la radio live
- `Resources/Strings/AppResources*.resx` (IT/EN/PL) — stringhe nuove: `Tab_Podcasts`, `Podcast_Empty`, `Podcast_Episodes`, `Podcast_NoEpisodes`, `Err_LoadPodcasts`, `Err_LoadEpisodes`

## Playback e scope v1 (limitazione nota)

`IAudioService` (radio live) è fortemente accoppiato allo streaming live: nessun vero pause (chiude e riapre lo stream), watchdog di riconnessione, prober di URL candidati — e su Android non usa nemmeno `MediaElement`, ma un `Media3AudioService`/`RadioPlaybackService` (ExoPlayer + `MediaLibraryService` in foreground service separato). Estendere questa infrastruttura per un playback on-demand con seek reale avrebbe richiesto toccare anche il foreground service Android, con rischio concreto di regressioni sulla riproduzione radio (già molto ottimizzata per latenza/sincronizzazione — vedi storico commit su `PlaybackLatencyOffsetSeconds`).

Scelta adottata: **`PodcastPlayerService` è un player indipendente**, con una propria `MediaElement` (istanziata dentro `PodcastEpisodesPage`), pause/resume/seek reali. Le due riproduzioni si escludono a vicenda:
- avviare un episodio ferma la radio live (`IAudioService.StopAsync()`)
- riavviare la radio live ferma un episodio podcast attivo (`OnAirViewModel.StartPlayAndNowPollingAsync`)

**Limitazione v1**: il player podcast non si integra con il Now Playing di sistema (notifica Android, lockscreen iOS/Android, Android Auto) — resta riproduzione in foreground, legata alla pagina `PodcastEpisodesPage` (lasciarla ferma la riproduzione, salvando la posizione raggiunta). Estensione futura possibile ma richiede lavoro dedicato sul lato Android (secondo `MediaLibraryService`/sessione) e iOS (`IPlatformNowPlayingService`).

## Persistenza posizione ascolto

`PodcastEpisodeProgress` (StationId, PodcastId, EpisodeId, PositionSeconds, IsCompleted, LastPlayedAt): salvataggio ogni ~10s durante la riproduzione, a pausa/stop e a fine episodio (`IsCompleted = true`). Alla riproduzione successiva dello stesso episodio, se non completato e con più di 3s di posizione salvata, riprende da lì.

## Estensioni future possibili
- Tutti i podcast di tutte le stazioni salvate (loop su `IAzuraStationCatalog.Stations`, nessun refactor di `IPodcastService`)
- Integrazione Now Playing di sistema / Android Auto per il player podcast
- Download offline episodio
- Badge "ascoltato" / "in corso" nella lista episodi (dato già disponibile in `PodcastEpisodeProgress`, manca solo l'esposizione in `PodcastEpisodesViewModel`/UI)
- Ricerca/filtro episodi per titolo

## Bugfix post-implementazione (test manuale utente)

1. **Crash selezionando il tab Podcast** — `Border.IsClippedToBounds` non esiste (proprietà solo su `Layout`), usato per ritagliare l'artwork rotonda nella card podcast. Rimosso — `Border` clippa già nativamente via `StrokeShape`. File: `Views/PodcastListPage.xaml`.
2. **Play non avviava l'audio** — si contava su `MediaElement.ShouldAutoPlay` per avviare la riproduzione dopo aver impostato `Source`, poco affidabile al primo caricamento. Sostituito con chiamata esplicita a `mediaElement.Play()` dopo l'assegnazione della sorgente. File: `Services/Audio/PodcastPlayerService.cs`.
3. **Nessun back button da episodi/player (macOS/Windows/iOS)** — `AppShell` disattiva globalmente la nav bar (`Shell.NavBarIsVisible="False"`); le pagine raggiunte via `GoToAsync` (non tab) devono riattivarla localmente, come già fa `EditStationPage`/`AddStationPage`. Aggiunto `Shell.NavBarIsVisible="True"` a `PodcastEpisodesPage.xaml`.
4. **Lista podcast non si aggiornava al cambio stazione** — `PodcastListViewModel` ora si iscrive a `OnAirViewModel.PropertyChanged` (proprietà `CurrentStation`) e ricarica automaticamente, non solo su `OnAppearing` del tab.
5. **Tab Podcast sempre visibile anche senza podcast** — aggiunta `OnAirViewModel.HasPodcasts` (probe silenziosa via `IPodcastService.GetPodcastsAsync`, eseguita a ogni cambio stazione in `OnCurrentStationChanged`); `AppShell` ora ha `BindingContext = OnAirViewModel` (iniettato tramite `App`) e la `ShellContent` del tab Podcast usa `IsVisible="{Binding HasPodcasts}"` (`BaseShellItem.IsVisible`, supportato nativamente da MAUI Shell per nascondere/mostrare un tab dinamicamente).

Verificato con `dotnet build` su maccatalyst e android dopo ogni fix, 0 errori/warning. Test funzionale reale su dispositivo/emulatore ancora da fare da parte dell'utente.

## Bugfix round 2 (test manuale utente su macCatalyst)

6. **Play ancora non funzionava dopo il fix precedente** — causa reale trovata leggendo il sorgente di CommunityToolkit.Maui (`MediaManager.macios.cs`): `PlatformUpdateSource` fa `if (Player is null) return;` — se l'`Handler` nativo (`AVPlayer`) non si è ancora agganciato nell'istante in cui viene impostata `Source` (possibile con una `MediaElement` invisibile/`HeightRequest=0` appena inizializzata), la richiesta di apertura viene scartata **in silenzio**, senza eccezioni né eventi — né `ShouldAutoPlay` né `Play()` esplicito hanno effetto in quel caso. La radio live "funziona" solo perché il watchdog di `AudioService` ritenta ogni 10s, mascherando la stessa race. Fix: `PodcastPlayerService.OpenAndPlayWithRetryAsync` — fino a 5 tentativi (Source + Play, attesa 700ms, verifica `CurrentState`) prima di arrendersi.
7. **Lista podcast/episodi non si aggiornava cambiando stazione con le frecce OnAir mentre si era su `PodcastEpisodesPage`** e **8. la tab Podcast non tornava alla schermata principale al rientro** — stesso fix per entrambe: `PodcastEpisodesViewModel` ora traccia `_loadedStationId`; `PodcastEpisodesPage.OnAppearing` confronta con la stazione corrente e, se diversa (`IsStaleForCurrentStation()`), fa `Shell.Current.GoToAsync("..")` invece di ricaricare dati non più pertinenti — tornando così alla lista podcast (già aggiornata per la nuova stazione dal fix round 1 punto 4).

Verificato con `dotnet build` su maccatalyst, android e ios, 0 errori/warning.

## Bugfix round 3 (log dispositivo macCatalyst)

9. **Play ancora silenzioso — causa reale trovata nel log**: `fail: MediaManager[] You do not have permission to access the requested resource.` — non un bug del client, è una risposta di errore (auth/permessi) ricevuta dal player nativo per l'URL dell'episodio, nonostante l'endpoint sia documentato come pubblico (`security: []`) nel sorgente AzuraCast. L'utente conferma che l'URL funziona se aperto a mano nel browser — verosimilmente browser autenticato con sessione admin AzuraCast, mentre l'app effettua la richiesta senza credenziali. Da verificare lato utente in una finestra privata (non loggata) o controllando le impostazioni della stazione/podcast su AzuraCast. Nel frattempo il retry ora si interrompe subito su un fallimento reale (invece di ripetere ciecamente) e propaga l'errore in UI (vedi punto 10).
10. **Nessun feedback in UI su fallimento riproduzione** — `PodcastPlayerService` ora si iscrive a `MediaElement.MediaFailed`; un fallimento reale interrompe `OpenAndPlayWithRetryAsync` e alza il nuovo evento `PlaybackFailed` (messaggio del player, es. il testo AzuraCast/AVFoundation ricevuto) che `PodcastEpisodesViewModel` inoltra a `ErrorMessage` — mostrato come label rossa in `PodcastEpisodesPage` (`Err_PodcastPlayback` come fallback se il player non fornisce un messaggio).
11. **Reset navigazione tab Podcast ancora non avveniva** — l'approccio basato su `OnAppearing` (confronto stazione caricata vs corrente) non era sufficiente da solo. Aggiunto un secondo meccanismo, autoritativo: `OnAirViewModel.OnCurrentStationChanged` ora individua direttamente la `ShellSection` con `Route == "PodcastListPage"` (indipendentemente dal tab attualmente attivo) e le applica `Navigation.PopToRootAsync()` se lo stack ha più di una pagina — non dipende dalla visibilità della pagina né da `GoToAsync("..")` relativo (rischioso da un tab diverso da quello Podcast).

Nota: build di verifica sospese su richiesta esplicita dell'utente per il resto della sessione — solo l'ultima build su maccatalyst (prima dei fix 10/11) è confermata pulita; i fix successivi non sono stati ricompilati in sessione.

## Bugfix round 4 — causa reale del fallimento play (JSON reale ispezionato)

L'utente ha fornito le due URL pubbliche del suo AzuraCast (`.../public/podcasts` e `.../public/podcast/{id}/episodes`) — interrogate direttamente per vedere la risposta reale (non solo il sorgente PHP). L'errore "You do not have permission..." aveva causa precisa: **l'URL dello stream episodio veniva ricostruita a mano** (`{urlBase}/api/station/{stationId}/podcast/{podcastId}/episode/{episodeId}/media`, con ID numerico stazione), ma l'URL realmente valida restituita dal server nel campo `links.download` dell'episodio è diversa:

```
http://<host>/api/station/<station_shortcode>/public/podcast/<podcastId>/episode/<episodeId>/download.mp3
```

— path (`/public/.../download.mp3` non `/podcast/.../media`) e identificatore stazione (shortcode, non ID numerico) entrambi diversi da quanto ricostruito. Il sorgente PHP consultato in precedenza (`GetMediaAction.php`, route `.../media`) esiste nel repo ma non è la route effettivamente usata dal generator che produce i link pubblici sull'istanza dell'utente.

**Fix**: aggiunto `AzuraCastPodcastEpisodeLinks` (proprietà `download`, dal campo JSON `links.download`) a `Models/AzuraCastPodcastEpisode.cs`; `MediaUrl` è ora una proprietà calcolata che legge `Links?.Download` invece di essere ricostruita — usa sempre l'URL assoluta fornita dal server, corretta per costruzione qualunque sia lo schema di routing dell'istanza AzuraCast. Rimossa la costruzione manuale in `PodcastService.GetEpisodesAsync`; gli episodi senza `links.download` valorizzato vengono ora scartati dalla lista (oltre al filtro esistente su `has_media`).

Non ricompilato in sessione (build di verifica sospese su richiesta utente) — verificare al prossimo giro.

## Verifica

`dotnet build -f net10.0-maccatalyst` e `dotnet build -f net10.0-android`: 0 errori, 0 warning.

Non testato su dispositivo/emulatore in questa sessione — raccomandato prima di considerare la feature production-ready: verifica del tab, fetch podcast/episodi su una stazione AzuraCast reale con podcast pubblicati, play/pause/seek, mutua esclusione con la radio live, resume dopo riavvio app.
