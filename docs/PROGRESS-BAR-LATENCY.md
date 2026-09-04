# Sfasamento barra di progresso / NowPlaying — indagine e stato

Data indagine: 2026-08-02

## Problema

La barra di avanzamento del brano (progress bar in `OnAirViewModel`) non è
perfettamente sincronizzata con l'audio realmente in riproduzione:

- quando la barra arriva alla fine, mancano ancora alcuni secondi di musica
  da riprodurre (la barra "finisce" prima dell'audio reale);
- quando parte una nuova traccia (barra riparte da 0 / cambia titolo), si
  devono aspettare alcuni secondi prima che l'audio nuovo inizi davvero
  (l'audio vecchio continua per un po').

Entrambi i sintomi sono coerenti con un solo fenomeno: l'audio reale è
**in ritardo** rispetto a quanto mostrato dalla UI.

## Fix già presente (drift da timer, non risolve questo problema)

Un fix precedente ha eliminato il drift dovuto al `dispatcher.CreateTimer()`
(che non garantisce un tick esattamente ogni 1000ms — varia per device/OS
throttling). Vedi `RadioE45/ViewModels/OnAirViewModel.cs`:

- `_elapsedAnchorUtc` — ancora temporale invece di un contatore di tick.
- `SetLocalElapsed(int seconds, DateTime? referenceUtc)` (riga ~529) — calcola
  l'ancora come `referenceUtc - seconds`.
- `OnProgressTimerTick` (riga ~468) — ricalcola l'elapsed locale come
  `DateTime.UtcNow - _elapsedAnchorUtc`, non `+1` a ogni tick.
- `ApplyNowPlayingInfo` (riga ~416) — ri-ancora solo quando il valore server è
  avanti o quando lo scarto supera 15s, per non "saltare" ad ogni poll.

Questo fix garantisce che la barra non deriva nel tempo per colpa del timer
locale, ma **non compensa nessuna latenza reale della pipeline audio** — il
punto di partenza (`referenceUtc`, cioè `info.LastUpdated`, impostato a
`DateTime.UtcNow` al momento della fetch in `NowPlayingService.Map`) resta
il valore grezzo riportato dal server AzuraCast, senza alcuna correzione.

## Bug di cache AzuraCast (confermato dall'utente, non ancora riverificato con lo script in questo repo)

È stato confermato (verifica manuale/precedente, non tramite lo script di
questo repo in questa sessione) che il campo `now_playing.elapsed`
dell'endpoint `/api/nowplaying/{station}` dell'istanza AzuraCast di
RadioE45 resta congelato per finestre di ~20s (bug di caching lato server),
invece di aggiornarsi ad ogni richiesta.

`docs/tools/test-fix.sh` (adattato da un'installazione AzuraCast di
riferimento, cartella `/Users/delfo/Lavori/AzuraCast/buildDelfo`) è lo
strumento per riverificarlo puntualmente in futuro (vedi "Come rieseguire
le misure"), ma non è stato eseguito in questa indagine.

Questo bug **non inquinerebbe comunque** la misura di latenza fatta con
`measure-latency.py`, perché quello script non usa `elapsed` ma
`now_playing.played_at` (timestamp Unix impostato una volta all'inizio della
traccia, non soggetto al refresh periodico della cache).

Vale comunque la pena segnalarlo/risolverlo lato AzuraCast in futuro, perché
un `elapsed` che si aggiorna a scatti di 20s invece che ogni secondo è di per
sé un problema per qualunque consumer che lo usi per un timer fluido.

## Metodologia di misura

Script di riferimento presi da un'altra installazione AzuraCast
(`/Users/delfo/Lavori/AzuraCast/buildDelfo/measure-latency.py` e
`test-fix.sh`), adattati e salvati in `docs/tools/measure-latency.py` di
questo repo. Modifiche fatte rispetto all'originale:

1. **Supporto HTTPS per lo stream Icecast** — l'originale usa sempre
   `http.client.HTTPConnection` (HTTP puro). Gli stream di RadioE45 e Muse
   sono serviti in HTTPS, quindi serve `HTTPSConnection` quando lo schema
   dell'URL è `https`.
2. **Finestra di retry per il match del titolo allungata** — da 5 tentativi
   × 0.5s (2.5s totali) a 25 tentativi × 1s (25s totali), per sopravvivere al
   bug di cache di ~20s descritto sopra quando lo script aspetta che l'API
   rifletta il nuovo titolo prima di leggere `played_at`.
3. **Default puntati alla stazione reale** invece di `localhost:8880`.

Come funziona lo script: si connette allo stream Icecast con
`Icy-MetaData: 1`, legge i blocchi di metadata ICY interlacciati nell'audio,
e ogni volta che `StreamTitle` cambia registra il timestamp wall-clock del
cambio. In parallelo un thread pollster interroga l'API `/api/nowplaying`
ogni secondo. Quando il titolo cambia nello stream, associa quel momento al
`played_at` restituito dall'API per la stessa canzone e calcola:

```
delta = stream_title_change_wallclock - played_at
```

**Limite noto e sistematico**: il primissimo `StreamTitle` letto subito dopo
la connessione allo stream viene sempre trattato dallo script come un
"cambio titolo" (perché il valore precedente è `None`), ma in realtà è solo
lo stato della traccia già in corso al momento della connessione — non una
transizione reale. Il primo campione di ogni run va sempre scartato.

**Limite più importante**: questo script misura solo lo scarto fra i
metadata Icecast (lato server) e il `played_at` riportato dall'API
AzuraCast. **Non misura in alcun modo il buffering audio lato device**
(tempo fra "byte arrivato dalla rete sul telefono/PC" e "suono
effettivamente udibile", gestito da `CommunityToolkit.Maui.MediaElement` /
dal player nativo della piattaforma). Quel pezzo di pipeline è quasi
certamente la causa dominante del sintomo riportato dall'utente e va
misurato con un metodo diverso (vedi "Prossimi passi").

## Individuazione dei dati stazione

I dati delle stazioni (URL base AzuraCast, ID numerico stazione, shortcode)
sono nel DB SQLite locale dell'app, non nel repository. Path su macOS:

```
/Users/delfo/Library/Containers/com.radioe45.app/Data/Library/radioe45.db
```

Tabella `RadioStations` (colonne rilevanti: `Id`, `Name`, `StreamUrl`,
`UrlBase`, `StationId`, `ShortName`). Query di esempio:

```bash
sqlite3 -header -column \
  /Users/delfo/Library/Containers/com.radioe45.app/Data/Library/radioe45.db \
  "SELECT * FROM RadioStations WHERE Id = 1;"
```

## Risultati misurati

### Stazione RadioE45 (Id=1 in `RadioStations`, shortcode AzuraCast `radioe45`, host `radioe45.ddns.net`)

```
[1] Mario Tessuto...     delta=+161.24s   <- artefatto di connessione (primo campione, sempre da scartare)
[2] Jingle                delta=-3.93s    (match titolo approssimato per via del bug cache 20s)
[3] Gino Paoli            delta=-3.87s    match pulito
[4] Ornella Vanoni        delta=-3.87s    match pulito
[5] Jingle (da "DJ SET")  delta=-16.80s   <- match titolo fallito, played_at non affidabile, da scartare
```

Scartando i campioni 1 e 5: **media pulita = -3.89s** (3 campioni: -3.93,
-3.87, -3.87 — molto coerenti fra loro).

### Stazione Muse (Id=3 in `RadioStations`, shortcode AzuraCast `muse`, host `hear.moe`)

```
[1] SQUARE ENIX 砂塵ノ記憶   delta=+31.44s  <- artefatto di connessione, da scartare
[2] Furui Riho - Hello       delta=-3.55s
[3] SQUARE ENIX Weight...    delta=-3.97s
```

Nota: qui il match del titolo fallisce sempre (warning su ogni campione) non
per il bug di cache, ma perché il formato `StreamTitle` ICY di questa
stazione è `Artista - Titolo` mentre il campo `text` dell'API è
`Artista - Album - Titolo` — non combaciano mai stringa per stringa. Non
invalida la misura, semplicemente lo script prende sempre il ramo di
fallback ("usa l'ultimo `played_at` noto").

Scartando il campione 1: **media pulita = -3.76s** (2 campioni: -3.55,
-3.97).

## Analisi

| Stazione | Delta medio pulito (server-side) |
|----------|-----------------------------------|
| RadioE45 | -3.89s |
| Muse     | -3.76s |

Le due stazioni sono **sostanzialmente identiche** a questo livello di
misura (stesso ordine di grandezza, stesso segno). Il segno negativo
significa che il cambio titolo nello stream Icecast avviene *prima* del
`played_at` registrato dall'API — cioè, a livello server, `elapsed` (=
`now - played_at`) sottostima leggermente la posizione reale nel brano.
Questa è la **direzione opposta** rispetto al sintomo riportato (bar che
finisce troppo presto / audio in ritardo).

Conclusione: lo scarto Icecast/API misurabile via rete **non spiega** il
sintomo, ed è pressoché identico fra le due stazioni — quindi non spiega
nemmeno perché Muse "sembri" più sincronizzata di RadioE45 a orecchio. La
differenza percepita deve quindi vivere più a valle, nel buffering audio
lato device, che questo script non può misurare. Le due stazioni hanno stack
di delivery diversi che potrebbero spiegare un buffering diverso nel player:

- **RadioE45**: mount Icecast diretto, porta 8060, `HTTP/1.0`.
- **Muse**: dietro reverse proxy nginx, porta 443, `HTTP/2`.

Un mount diretto vs uno proxato con protocollo diverso può far comportare
diversamente `MediaElement` in fase di buffering iniziale/prefetch — è
l'ipotesi più probabile per la differenza percepita fra le due stazioni, ma
va verificata sul device.

## Prossimi passi proposti

1. **Misura del buffering lato device** (la parte che manca). Non
   misurabile via rete: serve instrumentare l'app o cronometrare a mano.
   - Manuale: tap su play, cronometro fino al primo suono udibile,
     confrontato con `TrackElapsedSeconds` letto dall'API nello stesso
     istante. Ripetere per entrambe le stazioni, più volte, per avere una
     media.
   - Instrumentato: log con timestamp in `IAudioService` al momento in cui
     il player passa da stato "buffering" a "in riproduzione" (evento già
     esistente, vedi `IsBuffering` in `OnAirViewModel`), confrontato con il
     timestamp di `ApplyNowPlayingInfo`/`OnStreamOpened`.
   - Punti di codice coinvolti: `RadioE45/ViewModels/OnAirViewModel.cs`
     (`OnStreamOpened`, `OnPlaybackStateChanged`, `IsBuffering`),
     `RadioE45/Services/Audio/*` (implementazione `IAudioService` per
     piattaforma).

2. **Se confermato un ritardo costante misurabile**, applicare una
   compensazione lato client spostando l'ancora invece di sottrarre
   dall'elapsed (per gestire simmetricamente sia l'inizio che la fine
   traccia con un solo numero L):

   ```csharp
   private void SetLocalElapsed(int seconds, DateTime? referenceUtc = null)
   {
       _localElapsedSeconds = Math.Max(0, seconds);
       _elapsedAnchorUtc = (referenceUtc ?? DateTime.UtcNow)
           - TimeSpan.FromSeconds(_localElapsedSeconds)
           + TimeSpan.FromSeconds(PlaybackLatencySeconds); // L misurato
   }
   ```

   ```csharp
   private void OnProgressTimerTick(object? sender, EventArgs e)
   {
       if (_trackDurationSeconds <= 0) return;
       int realElapsed = (int)(DateTime.UtcNow - _elapsedAnchorUtc).TotalSeconds;
       _localElapsedSeconds = Math.Clamp(realElapsed, 0, _trackDurationSeconds); // era Math.Min, serve anche il floor a 0
       UpdateProgressDisplay();
       if (_localElapsedSeconds >= _trackDurationSeconds) AdvanceToNextTrackLocally();
   }
   ```

   Effetto: quando arriva "nuova traccia, elapsed=0" l'ancora è nel futuro
   di L secondi, quindi la barra resta ferma finché l'audio vecchio non ha
   davvero finito; a fine traccia la barra raggiunge la `duration` L secondi
   dopo il calcolo "naive", coincidendo con la fine reale dell'audio.

3. **Se L differisce sensibilmente fra stazioni** (verosimile, viste le
   pipeline di delivery diverse), promuovere `PlaybackLatencySeconds` da
   costante globale a campo su `AzuraStation` / tabella `RadioStations`
   (richiede migrazione DB, vedi `DatabaseService.CurrentDbVersion`).

4. **Segnalare il bug di cache 20s** lato AzuraCast (istanza
   `radioe45.ddns.net`) se non già tracciato — non causa il sintomo
   principale ma è comunque un difetto per qualsiasi consumer di `elapsed`.

## Come rieseguire le misure

```bash
# Trova i dati stazione dal DB locale dell'app
sqlite3 -header -column \
  /Users/delfo/Library/Containers/com.radioe45.app/Data/Library/radioe45.db \
  "SELECT Id, Name, UrlBase, StationId, ShortName FROM RadioStations;"

# Verifica prima il bug di cache (se non già confermato per l'host in questione)
docs/tools/test-fix.sh <shortcode>

# Misura latenza Icecast-vs-API (default già puntati a RadioE45; per un'altra stazione passare --api-url/--station)
python3 docs/tools/measure-latency.py --api-url https://<url-base-stazione> --station <shortcode> --samples 5
```

Nota: `test-fix.sh` di riferimento (in
`/Users/delfo/Lavori/AzuraCast/buildDelfo/test-fix.sh`) è pensato per
un'istanza locale (`localhost:8880`); va invocato con l'host reale come
argomento o adattato allo stesso modo di `measure-latency.py` se serve
puntarlo di default a un host remoto specifico.
