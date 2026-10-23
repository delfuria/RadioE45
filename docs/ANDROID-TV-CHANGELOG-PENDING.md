# Android TV — voci di changelog in sospeso

Decisione (2026-10-23): la TV **non si annuncia** in `CHANGELOG.md` prima del rilascio TV. Le sessioni T1–T9 non aggiungono righe sotto `## [Unreleased]`: le modifiche si accumulano qui, e la riga (o le righe) in inglese si scrive/scrivono al rilascio della TV (`/release-prep`).

Eccezione alla regola 11 di `CLAUDE.md`, valida solo per le modifiche **visibili solo sulla TV**. Una modifica che cambia anche telefono/desktop/web resta soggetta alla regola 11.

## Bozza della riga di rilascio (inglese)

> **Android TV:** the app now runs on Android TV and Google TV with a remote-friendly screen: now playing with the next track, previous/next station and favorite, song requests, podcasts, the schedule, and adding, editing and deleting stations with the on-screen keyboard.

Bozza completa (T9, 2026-10-23): include richieste, podcast e brano successivo; la cronologia e i controlli multimediali di sistema non ci sono (`n/a`). **Resta qui:** la riga si sposta in `CHANGELOG.md` solo con `/release-prep` al rilascio TV, non prima.

## Modifiche associate per sessione

| Sessione | Cosa vede l'utente TV | Note per il changelog |
|---|---|---|
| T2–T3 | Pagina radice con barra On Air / Canali, player con play/pausa, lista canali, Termini d'uso | Base della riga «Android TV» |
| T5 | Richieste di brani (ricerca con la tastiera, pagine) e podcast (elenco, episodi, filtri, player con ±30 s, ripresa dall'ultima posizione), visibili solo se la stazione li supporta | Da aggiungere alla bozza |
| T7 | Schermata di avvio con la versione; tornando dai podcast a On Air la radio riparte da sola | Da aggiungere alla bozza |
| T6 | Il brano successivo sotto i tempi del lettore | Da aggiungere alla bozza |
| T5 (dopo) | All'avvio parte sempre la stazione preferita, se c'è (la TV non ha impostazioni); altrimenti la prima | Da aggiungere alla bozza |
| T4 | Stazione precedente/successiva, cuore dei preferiti, Palinsesto a schermo intero, canali: aggiungi, modifica, elimina (con conferma), aggiorna; tastiera su schermo | Inclusa nella bozza sopra |

Modifiche di T7 su codice condiviso: `RadioPlaybackService` riapre la connessione dopo una pausa di oltre 5 s (vale anche per telefono e Android Auto: la riga «Fixed» in inglese sotto è da decidere al rilascio); stringa `Splash_Version` (it, en, pl); `TvSplashOverlay` e `TvNowPlayingView.OnShownAsync` solo TV.

Modifiche di T5 su codice condiviso (nessun effetto visibile fuori dalla TV): `PodcastEpisodesViewModel.SeekByCommand` e `CycleStatus/Season/EpisodeNumberCommand`, stringhe `Request_PreviousPage` / `Request_NextPage` (it, en, pl), `ITvHost.ShowPodcastEpisodes`, `ITvSection.OnHiddenAsync`, `IAppEnvironment.AlwaysStartsWithFavorite` (vera solo su TV, usata da `OnAirViewModel`; 3 test).

Modifiche di T4 su codice condiviso con le altre piattaforme (nessun effetto visibile fuori dalla TV): `AddStationViewModel.Reset()`, `EditStationViewModel.ReloadAsync()`, comandi `ToggleSelected` e `ToggleCustomInfo`, stringa `RadioList_Refresh` (it, en, pl), `ITvHost.GoBack/ShowEditStation`, `XamlScanner` di ParityCheck.

Aggiornare la tabella a ogni sessione TV.

## Righe «Fixed» per telefono e Android Auto, nate dal lavoro sulla TV (da decidere al rilascio)

- **Android:** resuming the radio after a long pause (for example after the TV or a media key paused it) now reconnects to the live stream instead of playing the old buffered audio.
