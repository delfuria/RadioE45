# Radio E45 — Microsoft Store (Partner Center): testi e controlli

> Scritto il 22 ottobre 2026. Testi per lo store in inglese (regola del progetto); la versione italiana serve per la scheda localizzata in italiano.
> Caricamenti, tag e release li esegue l'utente: qui ci sono solo testi e controlli. Procedura generale: [`RELEASE.md`](RELEASE.md) e [`STORE-DISTRIBUTION.md`](STORE-DISTRIBUTION.md).
> Testo di partenza: la descrizione usata per lo store Apple, senza la parte CarPlay e adattata a Windows.

## Indice

- [1. Controlli prima dell'invio](#1-controlli-prima-dellinvio)
- [2. Pacchetto e caricamento](#2-pacchetto-e-caricamento)
- [3. What's new in this version](#3-whats-new-in-this-version)
- [4. Store listing: descrizione, funzionalità, parole chiave](#4-store-listing-descrizione-funzionalità-parole-chiave)
- [5. Notes for certification](#5-notes-for-certification)
- [6. Se la certificazione fa domande](#6-se-la-certificazione-fa-domande)

---

## 1. Controlli prima dell'invio

| # | Controllo | Come | Stato |
|---|---|---|---|
| 1 | Versione e changelog | skill `/release-prep` (sposta `[Unreleased]` nella versione, controlli, build) | da fare |
| 2 | Pacchetto MSIX/MSI firmato per Windows generato e installato | [`WINDOWS-README.md`](WINDOWS-README.md) | da verificare |
| 3 | Testi della scheda in inglese e in italiano aggiornati alla versione | questo documento §3 e §4 | fatto il 22 ottobre |
| 4 | Ogni funzione elencata in §4 esiste nella build Windows | [`../parity/features.yaml`](../parity/features.yaml) (colonna `desktop`/`maui`) | da verificare |
| 5 | Screenshot Windows (almeno 4, minimo 1 per inviare) | Partner Center → Store listing → Screenshots | da fare |
| 6 | Privacy policy e supporto | [`PrivacyPolicy.html`](PrivacyPolicy.html), support@radioe45.it | fatto |

## 2. Pacchetto e caricamento

1. Generare il pacchetto Windows con gli script fuori dal repository (vedi [`STORE-DISTRIBUTION.md`](STORE-DISTRIBUTION.md)).
2. In Partner Center, nella app Radio E45, creare una nuova submission.
3. Caricare il pacchetto nella sezione *Packages*.
4. Compilare la scheda *Store listing* nelle lingue scelte (inglese come base, italiano come lingua aggiuntiva), usando i testi del §3 e del §4.
5. Compilare *Notes for certification* con il testo del §5.
6. Salvare la bozza, controllare, poi inviare per la certificazione.

## 3. What's new in this version

Dalla sezione `[Unreleased]` e dall'ultima versione di `CHANGELOG.md`. Da riallineare se il changelog cambia prima della release.

**English**

```
• Update check: the app tells you when a newer version is available, with a manual check in Settings.
• On Air: the buffering spinner is replaced by the VU meter while the station plays.
• On Air: the previous and next station buttons are hidden when you have a single station.
• Requests: the list of songs you can request is shuffled.
• Podcasts: the episode count uses the right singular and plural forms.
```

**Italiano**

```
• Controllo aggiornamenti: l'app segnala quando è disponibile una versione più recente, con controllo manuale nelle Impostazioni.
• In onda: durante la riproduzione, al posto dello spinner di caricamento compare il VU meter.
• In onda: i pulsanti stazione precedente e successiva sono nascosti se hai una sola stazione.
• Richieste: l'elenco dei brani richiedibili è mescolato.
• Podcast: il conteggio degli episodi usa il singolare e il plurale corretti.
```

## 4. Store listing: descrizione, funzionalità, parole chiave

Limiti Partner Center: descrizione fino a 10.000 caratteri; short description consigliata sotto 270 caratteri; funzionalità fino a 20, massimo 200 caratteri ciascuna (il negozio le mette già in elenco puntato, senza `•`); parole chiave fino a 7 termini, massimo 40 caratteri ciascuno e 21 parole in totale.

### 4.1 Short description

**English** (96 caratteri)

```
Listen to web radio and internet streams on Windows, with track info and cover art updated live.
```

**Italiano**

```
Ascolta webradio e stream radio su Windows, con brano in onda e copertina aggiornati in diretta.
```

### 4.2 Description

**English**

```
Radio E45 is the player for listening to web radio and internet streams on Windows. Follow our radio and add any AzuraCast-based station with its public address.

Open the app, press Play and enjoy high-quality streaming. The track on air, artist, cover and streamer name update automatically, so you always know what you're listening to.

LISTENING
• Live streaming with background playback and system media controls
• High-quality HLS stream support
• VU meter while the station plays
• Quick switching between your stations, with the names of the previous and next ones
• Built-in volume control

ON AIR
• Current track, artist and cover art in real time
• LIVE indicator with the name of the active streamer, or "Scheduled" when no one is connected
• Station schedule, with streamers and playlists
• List of recently played tracks
• Number of connected listeners

PODCASTS
• Episodes of the station's podcasts, with filters for season, episode and listening status
• Resume from where you stopped

REQUESTS
• Request songs from the station, when requests are enabled

YOUR STATIONS
• Add, edit and remove stations with their public address
• Includes Radio E45 and the AzuraCast demo station
• Dark or light theme
• Available in Italian, English and Polish
• Tells you when a new version is available, with a manual check in Settings

Lightweight and always ready: no sign-up, no account required, no data collected.

Radio E45 is a player for internet radio streams. It does not host or rebroadcast any audio. The rights to each stream belong to its operator. AzuraCast is a trademark of its respective owners; the app works with stations that use it but is not affiliated with the project.
```

**Italiano**

```
Radio E45 è il player per ascoltare webradio e stream radio su Internet direttamente da Windows. Segui la nostra radio e aggiungi qualsiasi stazione basata su AzuraCast con il suo indirizzo pubblico.

Apri l'app, premi Play e goditi lo streaming in alta qualità. Brano in onda, artista, copertina e nome dello streamer si aggiornano da soli, così sai sempre cosa stai ascoltando.

ASCOLTO
• Streaming live con riproduzione in background e controlli multimediali di sistema
• Supporto agli stream HLS ad alta qualità
• Indicatore VU meter durante la riproduzione
• Selezione rapida tra le tue stazioni, con i nomi delle precedenti e successive
• Controllo del volume integrato

IN ONDA
• Brano corrente, artista e copertina in tempo reale
• Indicatore LIVE con il nome dello streamer attivo, oppure "In programma" quando non è collegato
• Palinsesto della stazione, con streamer e playlist
• Elenco dei brani ascoltati di recente
• Numero di ascoltatori connessi

PODCAST
• Episodi dei podcast della stazione, con filtri per stagione, episodio e stato di ascolto
• Riprendi dal punto in cui eri rimasto

RICHIESTE
• Richiedi i brani alla stazione, quando le richieste sono abilitate

LE TUE STAZIONI
• Aggiungi, modifica ed elimina le stazioni con il loro indirizzo pubblico
• Include Radio E45 e la stazione demo di AzuraCast
• Tema scuro o chiaro
• Disponibile in italiano, inglese e polacco
• Ti avvisa quando è disponibile una nuova versione, con controllo manuale nelle Impostazioni

Leggera e sempre pronta: nessuna registrazione, nessun account richiesto, nessun dato raccolto.

Radio E45 è un player per stream radio su Internet. Non ospita né ritrasmette alcun audio. I diritti di ogni stream appartengono al suo gestore. AzuraCast è un marchio dei rispettivi titolari; l'app è compatibile con le stazioni che lo usano ma non è affiliata al progetto.
```

### 4.3 Features (elenco puntato automatico, max 20 voci, max 200 caratteri)

**English**

```
Live streaming with background playback and system media controls
High-quality HLS stream support
VU meter while the station plays
Quick switching between your stations
Built-in volume control
Current track, artist and cover art in real time
LIVE indicator with the active streamer's name
Station schedule with streamers and playlists
List of recently played tracks
Number of connected listeners
Podcast episodes with season, episode and listening filters
Resume podcasts where you stopped
Song requests, when the station enables them
Add, edit and remove stations by public address
Includes Radio E45 and the AzuraCast demo station
Dark or light theme
Available in Italian, English and Polish
Tells you when a new version is available
```

**Italiano**

```
Streaming live con riproduzione in background e controlli multimediali di sistema
Supporto agli stream HLS ad alta qualità
VU meter durante la riproduzione
Selezione rapida tra le tue stazioni
Controllo del volume integrato
Brano, artista e copertina in tempo reale
Indicatore LIVE con il nome dello streamer attivo
Palinsesto della stazione con streamer e playlist
Elenco dei brani ascoltati di recente
Numero di ascoltatori connessi
Episodi dei podcast con filtri per stagione, episodio e ascolto
Riprendi i podcast dal punto in cui eri rimasto
Richieste di brani, quando la stazione le abilita
Aggiungi, modifica ed elimina le stazioni con l'indirizzo pubblico
Include Radio E45 e la stazione demo di AzuraCast
Tema scuro o chiaro
Disponibile in italiano, inglese e polacco
Ti avvisa quando è disponibile una nuova versione
```

### 4.4 Search terms (parole chiave)

**English** (7 termini, 12 parole in totale)

```
web radio
internet radio
radio streaming
live radio
AzuraCast
radio player
podcast
```

**Italiano** (7 termini)

```
webradio
radio italiana
radio streaming
radio in diretta
AzuraCast
player radio
podcast
```

## 5. Notes for certification

Testo in inglese, da incollare nel campo *Notes for certification*:

```
Radio E45 is a live internet radio player. It does not host or rebroadcast audio: it plays public stream URLs.

How to test:
1. Install and launch the app. Accept the Terms of Use shown at first launch.
2. The app adds two sample stations: Radio E45 and the AzuraCast demo station.
3. Choose a station in the list and press Play. Live audio starts and the track, artist and cover art update automatically.
4. Play, pause and volume controls work from the player bar and from the system media controls.
5. To test adding a station: open the station list, press "+" and enter a public stream address (for example the AzuraCast demo station address shown in the app).

No account or login is required. An internet connection is needed to listen. Playback is live streaming, so seeking is disabled.

Contact for rights holders: support@radioe45.it
```

**Italiano** (solo per riferimento interno)

```
Radio E45 è un player per stream radio in diretta. Non ospita né ritrasmette audio: riproduce URL di stream pubblici.

Come provarla:
1. Installa e avvia l'app. Accetta i Termini di utilizzo mostrati al primo avvio.
2. L'app aggiunge due stazioni di esempio: Radio E45 e la stazione demo di AzuraCast.
3. Scegli una stazione nella lista e premi Play. Parte l'audio in diretta e brano, artista e copertina si aggiornano da soli.
4. Play, pausa e volume funzionano dalla barra del player e dai controlli multimediali di sistema.
5. Per provare l'aggiunta di una stazione: apri l'elenco stazioni, premi "+" e inserisci un indirizzo di stream pubblico.

Non serve alcun account. Serve una connessione Internet per ascoltare. La riproduzione è in diretta: il posizionamento non è disponibile.

Contatto per i titolari dei diritti: support@radioe45.it
```

## 6. Se la certificazione fa domande

- **"The app is not clearly described" / funzioni non chiare:** rimandare al §5 e indicare le due stazioni di esempio già presenti al primo avvio.
- **"We could not play a station":** verificare la connessione di rete del revisore e che la stazione Radio E45 sia online; allegare nelle note l'orario di prova e il nome della stazione scelta.
- **Diritti sui contenuti:** la risposta è nel §5 e nella dichiarazione nella scheda (il player non ospita né ritrasmette audio; i diritti dello stream appartengono al gestore). Gli allegati di autorizzazione usati per App Store (vedi [`APP-REVIEW-5.2.3.md`](APP-REVIEW-5.2.3.md)) possono essere riusati se richiesti.
- **Rete locale:** se la certificazione chiede della rete locale, l'app si collega a indirizzi privati solo se l'utente aggiunge a mano una stazione su rete locale (vedi [`APP-REVIEW-5.1.1.md`](APP-REVIEW-5.1.1.md)).
