# CarPlay e scene — prove su iPhone reale

> Scritto il 21 ottobre 2026, dopo le Fasi A, C, D e B sul simulatore (vedi [`piano-operativo.md`](piano-operativo.md) §11 e §12).
> Vincolo: **nessuna auto o autoradio CarPlay disponibile.** Le prove CarPlay usano l'app **CarPlay Simulator** del Mac con l'iPhone collegato via cavo. Le prove che richiedono un'auto vera sono elencate a parte (parte D) come rischio residuo.

## Indice

- [0. Prima di iniziare](#0-prima-di-iniziare)
- [Parte A — App sul telefono, senza CarPlay](#parte-a--app-sul-telefono-senza-carplay)
- [Parte B — CarPlay Simulator con iPhone via cavo](#parte-b--carplay-simulator-con-iphone-via-cavo)
- [Parte C — Build TestFlight](#parte-c--build-testflight)
- [Parte D — Cosa resta senza un'auto vera](#parte-d--cosa-resta-senza-unauto-vera)
- [Riepilogo esiti](#riepilogo-esiti)

---

## 0. Prima di iniziare

### 0.1 Prerequisiti

| # | Cosa | Chi | Stato |
|---|---|---|---|
| P1 | Capability CarPlay sull'App ID e profili rigenerati, vedi [`provisioning-profiles.md`](provisioning-profiles.md) | tu | [x] |
| P2 | `com.apple.developer.carplay-audio` in `Platforms/iOS/Entitlements.plist`, e firma esplicita per le build Debug su device (oggi usano il profilo Team scelto da Xcode e **nessun** file entitlement) | Claude, dopo P1 | [x] |
| P3 | iPhone collegato al Mac via cavo, sbloccato, con "Autorizza questo computer" accettato | tu | [ ] |
| P4 | Modalità sviluppatore attiva sull'iPhone (*Impostazioni → Privacy e sicurezza → Modalità sviluppatore*) | tu | [ ] |
| P5 | Un secondo telefono per chiamare l'iPhone (prove di interruzione) | tu | [ ] |
| P6 | Cuffie Bluetooth o con filo (prova A8) | tu | [ ] |

### 0.2 Build e installazione

```bash
# UDID dell'iPhone collegato
xcrun devicectl list devices

# Build Debug per device (dopo P2)
dotnet build RadioE45 -f net10.0-ios -c Debug -p:RuntimeIdentifier=ios-arm64 --no-incremental

# Controllo: l'entitlement deve essere nella FIRMA (su device conta la firma, non la sezione del simulatore)
codesign -d --entitlements - RadioE45/bin/Debug/net10.0-ios/ios-arm64/RadioE45.app | grep carplay-audio

# Installazione
xcrun devicectl device install app --device <UDID> RadioE45/bin/Debug/net10.0-ios/ios-arm64/RadioE45.app
```

Se `codesign` non stampa `carplay-audio`, fermarsi: CarPlay non comparirà. `--no-incremental` serve dopo ogni modifica agli entitlement, vedi [`simulator-testing.md`](simulator-testing.md) §5.1.

### 0.3 Come raccogliere i log

**Opzione 1, Console.app (consigliata):**
1. Apri Console sul Mac e seleziona l'iPhone nella barra laterale.
2. Menu *Azione*: attiva **Includi messaggi Info** e **Includi messaggi Debug**.
3. Premi **Avvia streaming** e filtra con `process:RadioE45`. Per lo stato del Now Playing filtra con `process:mediaremoted radioe45`.
4. Prima di ogni prova annota l'ora, così ritrovi le righe.

**Opzione 2, solo stdout dell'app** (le righe `dbug:`/`info:` del logger):

```bash
xcrun devicectl device process launch --console --device <UDID> com.radioe45.app
```

Righe da cercare, le stesse viste sul simulatore:

| Evento | Riga |
|---|---|
| Riproduzione | `StateChanged: ... → Playing` |
| Riconnessione | `Watchdog: state=... reconnecting`, `Connectivity restored, reconnecting` |
| Player assente (non deve comparire) | `Play requested for ... but no player is attached` |
| CarPlay legge l'entitlement | `Application declares audio entitlement.` |
| Scena CarPlay | `Connecting to listener endpoint for scene identifier: Car[...]` |
| Lista CarPlay | `Setting root template <CPListTemplate ...>` |
| Catalogo | `Station loaded: <nome> online=True` |
| Scollegamento | `disconnecting Car[...]`, `Connection invalidated` |
| Stato Now Playing (processo `mediaremoted`) | `PlaybackState changed from ... to ...`, `playbackRate changed to ...` |

### 0.4 Come registrare l'esito

Per ogni prova annota: esito (OK / KO / parziale), ora, una frase su cosa hai visto e, se KO, uno screenshot. Sull'iPhone: tasto laterale + volume su. Sul Mac: solo la finestra (`Cmd+Shift+4`, poi `Spazio`, poi click sulla finestra). Le righe di log vanno copiate nella colonna note.

---

## Parte A — App sul telefono, senza CarPlay

Verifica che le Fasi A (scene) e C (player AVPlayer) non abbiano rotto l'app per chi non usa CarPlay. **È la parte più importante**, perché riguarda tutti gli utenti iOS.

### A1. Avvio a freddo
- **Passi:** chiudi l'app dal multitasking, poi aprila.
- **Atteso:** parte la stazione preferita, o la prima se non c'è una preferita, come nella versione in produzione.
- **Misura:** stazione mostrata; nel log `StateChanged: ... → Playing` entro pochi secondi.

### A2. Comandi base
- **Passi:** pausa, play, stazione successiva e precedente, volume.
- **Atteso:** ogni comando risponde entro 1-2 secondi; dopo la pausa il play riparte dalla diretta, non da un buffer vecchio.
- **Misura:** una riga `StateChanged` per ogni comando; nessun `MediaFailed`.

### A3. Background breve
- **Passi:** con la radio che suona, vai alla home per 1 minuto, poi torna nell'app.
- **Atteso:** l'audio non si interrompe; al rientro stessa stazione e pulsante su pausa.
- **Misura:** nessun passaggio a `Stopped` o `Paused` nel log.

### A4. Ricreazione della scena (punto non verificato sul simulatore)
- **Scopo:** iOS può distruggere la scena dell'app in background e ricrearla con il processo vivo. Con la Fase A la stazione non deve ripartire da zero.
- **Passi:**
  1. Fai suonare una stazione **diversa** dalla preferita.
  2. Vai alla home e apri molte app pesanti (fotocamera, giochi, Safari con molte schede) per 5-10 minuti, mentre la radio continua.
  3. Torna in RadioE45.
- **Atteso:** stessa stazione, audio mai interrotto.
- **Misura:** se nel log compaiono nuove righe `UIWindowScene` senza un nuovo processo, la scena è stata ricreata e la prova vale. Se non compaiono, la scena non è stata distrutta: prova non conclusiva, annotalo.

### A5. Chiusura forzata e riavvio
- **Passi:** con una stazione non preferita in riproduzione, chiudi l'app dal multitasking e riaprila.
- **Atteso:** riparte la preferita, comportamento di un avvio vero.

### A6. Lock screen e Centro di Controllo (diagnosi del problema del Now Playing)
- **Passi:** con la radio che suona, blocca lo schermo; poi apri il Centro di Controllo.
- **Atteso:** titolo, artista e copertina corretti; il pulsante mostra **pausa** (cioè "sta suonando") e il tempo avanza; pausa e play dalla lock screen funzionano.
- **Misura (decisiva):** in Console, `process:mediaremoted radioe45` → deve comparire `PlaybackState changed from Paused to Playing`.
  - Se compare, il "Paused" visto sul simulatore è un limite del simulatore.
  - Se **non** compare e il pulsante mostra play, il problema è reale e serve la correzione con `MPNowPlayingSession` (vedi piano §11, Fase B).

### A7. Interruzioni audio
Ripeti per ognuna: **telefonata in arrivo** (accetta, parla 20 secondi, chiudi), **telefonata rifiutata**, **Siri**, **sveglia o timer**, **un'altra app che riproduce audio** (Musica).
- **Atteso:** l'audio si ferma durante l'interruzione e non riparte da solo mentre è in corso; alla fine annota se riparte da solo o serve premere play (va bene entrambi, purché sia coerente).
- **Misura:** cerca `Watchdog: ... reconnecting` **durante** la telefonata. Se compare, il watchdog prova a riaprire lo stream mentre la chiamata è in corso: da correggere.

### A8. Cuffie e uscite audio (anticipa lo scollegamento dell'auto)
- **Passi:** fai suonare la radio in cuffia (Bluetooth o con filo), poi scollega le cuffie o spegnile.
- **Atteso secondo le linee guida Apple:** la riproduzione va in pausa e non passa all'altoparlante.
- **Misura:** annota cosa succede. Se l'audio continua dall'altoparlante, lo stesso succederà spegnendo l'auto: da correggere gestendo il cambio di route audio (`OldDeviceUnavailable`).

### A9. Rete
- **Passi:** con la radio che suona, passa da Wi-Fi a dati cellulari; poi attiva la modalità aereo per 30 secondi e disattivala.
- **Atteso:** l'audio riprende da solo entro circa 10-15 secondi dal ritorno della rete.
- **Misura:** `Connectivity restored, reconnecting` o `Watchdog: ... reconnecting`, poi `Playing`.

### A10. Rotazione
- **Passi:** ruota il telefono durante la riproduzione.
- **Atteso:** layout corretto e audio continuo.

### A11. Podcast e radio insieme
- **Scopo:** su iOS la radio ora usa `AVPlayer` mentre i podcast usano ancora il `MediaElement`.
- **Passi:** con la radio che suona, apri un podcast e avvia un episodio; poi torna su In Onda.
- **Atteso:** mai due audio insieme; la posizione del podcast viene salvata; al ritorno su In Onda la radio riparte come prima.

### A12. iPad (solo se ne hai uno)
- **Scopo:** il manifest ora ha `UIApplicationSupportsMultipleScenes=true`, necessario per CarPlay.
- **Passi:** prova ad aprire una seconda finestra di RadioE45: tieni premuta l'icona nel Dock, cerca "Mostra tutte le finestre" o "Nuova finestra", oppure trascina l'icona di lato con Split View o Stage Manager.
- **Atteso (deciso):** una sola finestra.
- **Misura:** se si riesce ad aprirne una seconda, annota come. Va limitata prima della release.

---

## Parte B — CarPlay Simulator con iPhone via cavo

**CarPlay Simulator** (`/Applications/CarPlay Simulator.app`, dagli *Additional Tools for Xcode*) fa da autoradio. L'iPhone vero si collega al Mac via cavo e il Mac mostra lo schermo CarPlay. È la prova più vicina a un'auto disponibile ora.

### B0. Preparazione
1. Apri CarPlay Simulator sul Mac.
2. Collega l'iPhone con il cavo e sbloccalo.
3. Se l'iPhone chiede se consentire CarPlay, anche con il telefono bloccato, rispondi **Consenti**.
4. Controlla che CarPlay sia attivo: *Impostazioni → Generali → CarPlay*.

Se la connessione non parte, controlla le istruzioni nella finestra di CarPlay Simulator e annota cosa chiede. Non ho verificato questa procedura su questo Mac.

### B1. Icona nella home CarPlay
- **Atteso:** l'icona RadioE45 compare. Se è su una seconda pagina, scorri.
- **Misura:** `Application declares audio entitlement.` quando apri l'app. Se l'icona manca, torna al controllo `codesign` del §0.2.

### B2. Lista stazioni
- **Passi:** apri RadioE45 da CarPlay.
- **Atteso:** titolo "Stazioni", tutte le tue stazioni; quella in onda ha come sottotitolo il brano, le altre la descrizione.

### B3. Riproduzione e Now Playing (problema aperto)
- **Passi:** tocca una stazione diversa da quella in onda.
- **Atteso:** l'audio parte; si apre il Now Playing con titolo, artista e copertina; **il pulsante mostra pausa e il tempo avanza.**
- **Misura:** `StateChanged: ... → Playing`; in `mediaremoted` lo stato passa a `Playing` (confronta con A6).

### B4. Comandi incrociati
- **Passi:** pausa e play dal Now Playing di CarPlay; poi dal telefono; poi cambia stazione dal telefono.
- **Atteso:** CarPlay e telefono restano coerenti (stessa stazione, stesso stato); la lista CarPlay, tornando indietro, mostra la stazione nuova con il brano.
- **Nota:** il sottotitolo della lista si aggiorna solo quando cambia il catalogo, non a ogni brano. È noto (piano §12): annota se ti dà fastidio.

### B5. Avvio a freddo da CarPlay
1. Chiudi l'app dal multitasking.
2. Con CarPlay collegato, apri RadioE45 da CarPlay e tocca una stazione.
- **Atteso:** lista visibile entro pochi secondi e audio che parte. È il caso corretto nelle Fasi C e D.
- **Misura:** `Setting root template`, poi `Station loaded`, poi `Playing`; **nessuna** riga `no player is attached`.

### B6. Avvio a freddo con telefono bloccato
- **Passi:** come B5, ma blocca l'iPhone prima di aprire l'app da CarPlay.
- **Atteso:** come B5. In auto il telefono è quasi sempre bloccato in tasca.
- **Misura:** come B5; in più cerca errori di accesso al database o ai file.

### B7. Catalogo senza rete
1. Attiva la modalità aereo sull'iPhone, lasciando il cavo collegato.
2. Chiudi l'app e aprila da CarPlay.
3. Dopo 30 secondi disattiva la modalità aereo.
- **Atteso:** la lista mostra le stazioni (anche offline) oppure "Nessuna stazione disponibile", mai "Caricamento stazioni..." bloccato per sempre; al ritorno della rete la lista si aggiorna da sola.
- **Annota:** toccando una stazione offline cosa succede. Serve per decidere se nasconderle (piano §12).

### B8. Scollegamento
- **Passi:** con la radio che suona da CarPlay, scollega il cavo.
- **Atteso Apple:** la riproduzione va in pausa (l'utente ha "spento l'auto").
- **Misura:** annota se l'audio passa all'altoparlante dell'iPhone (stessa questione di A8); nel log `disconnecting Car[...]`; l'app sul telefono resta utilizzabile.

### B9. Ricollegamento
- **Passi:** ricollega il cavo.
- **Atteso:** l'app è ancora nella home CarPlay; aprendola, la lista è corretta. Annota se l'audio riparte da solo.

### B10. Lingua
- **Passi:** imposta l'iPhone in inglese, poi in polacco, e riapri l'app da CarPlay.
- **Atteso:** "Stations" / "Stacje" e i testi di caricamento tradotti.

### B11. Sessione lunga
- **Passi:** lascia suonare da CarPlay per 30 minuti cambiando stazione ogni tanto.
- **Atteso:** nessun blocco o crash, metadati aggiornati; annota se il telefono si scalda in modo anomalo.

---

## Parte C — Build TestFlight

Le parti A e B usano una build Debug firmata Development. La release userà il profilo App Store: il percorso di firma è diverso e va provato prima di inviare ad Apple.

1. Build Release firmata App Store, caricata su TestFlight (procedura in [`../RELEASE.md`](../RELEASE.md); il caricamento lo fai tu).
2. Installala dall'app TestFlight.
3. Ripeti almeno **A1, A6, B1, B3, B5**.
- **Misura:** l'icona compare in CarPlay anche con questa firma. Se manca, il profilo App Store non contiene la capability.

---

## Parte D — Cosa resta senza un'auto vera

Queste cose CarPlay Simulator non le riproduce, o non in modo affidabile. Restano un rischio fino a una prova in auto.

| Cosa | Perché conta | Mitigazione possibile ora |
|---|---|---|
| Tasti al volante e manopola | Comandi play/pausa/avanti dall'auto | Già gestiti da `MPRemoteCommandCenter`; equivalenti alle cuffie Bluetooth (A8) |
| CarPlay wireless e Bluetooth dell'auto | Route audio diverse dal cavo | Nessuna; prova A8 come indicazione |
| Spegnimento dell'auto | Scollegamento brusco con audio attivo | B8 con il cavo |
| Schermi di forma e risoluzione diverse, tema chiaro e scuro | Leggibilità della lista | Si usano solo template di sistema: il rischio è basso |
| Limiti durante la guida (lunghezza liste) | Con circa 4 stazioni non dovrebbe contare | Nessuna |
| Rete in movimento (cambio cella, gallerie) | Riconnessioni frequenti | A9 |

Proposta: prima della release CarPlay, fare almeno una prova in un'auto vera (di un amico o in concessionaria, anche 15 minuti: B1, B3, B5, B8). Se non è possibile, rilasciare comunque e annotarlo come rischio accettato.

---

## Riepilogo esiti

| Prova | Esito | Data e ora | Note e righe di log |
|---|---|---|---|
| A1 Avvio a freddo | | | |
| A2 Comandi base | | | |
| A3 Background breve | | | |
| A4 Ricreazione scena | | | |
| A5 Chiusura forzata | | | |
| A6 Lock screen e Now Playing | | | |
| A7 Interruzioni | | | |
| A8 Cuffie scollegate | | | |
| A9 Rete | | | |
| A10 Rotazione | | | |
| A11 Podcast e radio | | | |
| A12 iPad | | | |
| B1 Icona CarPlay | | | |
| B2 Lista | | | |
| B3 Riproduzione e Now Playing | | | |
| B4 Comandi incrociati | | | |
| B5 Avvio a freddo da CarPlay | | | |
| B6 Avvio a freddo, telefono bloccato | | | |
| B7 Catalogo senza rete | | | |
| B8 Scollegamento | | | |
| B9 Ricollegamento | | | |
| B10 Lingua | | | |
| B11 Sessione lunga | | | |
| C TestFlight | | | |

Quando hai gli esiti, passameli (anche solo questa tabella con le note): aggiorno il piano §12 e decidiamo cosa correggere prima della release.
