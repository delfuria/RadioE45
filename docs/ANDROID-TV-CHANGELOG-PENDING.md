# Android TV — voci di changelog in sospeso

Decisione (2026-10-23): la TV **non si annuncia** in `CHANGELOG.md` prima del rilascio TV. Le sessioni T1–T9 non aggiungono righe sotto `## [Unreleased]`: le modifiche si accumulano qui, e la riga (o le righe) in inglese si scrive/scrivono al rilascio della TV (`/release-prep`).

Eccezione alla regola 11 di `CLAUDE.md`, valida solo per le modifiche **visibili solo sulla TV**. Una modifica che cambia anche telefono/desktop/web resta soggetta alla regola 11.

## Bozza della riga di rilascio (inglese)

> **Android TV:** the app now runs on Android TV and Google TV with a remote-friendly screen: now playing with previous/next station and favorite, the schedule, and adding, editing and deleting stations with the on-screen keyboard.

Da aggiornare con le funzioni delle sessioni successive (richieste, podcast, brano successivo, cronologia, controlli multimediali di sistema).

## Modifiche associate per sessione

| Sessione | Cosa vede l'utente TV | Note per il changelog |
|---|---|---|
| T2–T3 | Pagina radice con barra On Air / Canali, player con play/pausa, lista canali, Termini d'uso | Base della riga «Android TV» |
| T4 | Stazione precedente/successiva, cuore dei preferiti, Palinsesto a schermo intero, canali: aggiungi, modifica, elimina (con conferma), aggiorna; tastiera su schermo | Inclusa nella bozza sopra |

Modifiche di T4 su codice condiviso con le altre piattaforme (nessun effetto visibile fuori dalla TV): `AddStationViewModel.Reset()`, `EditStationViewModel.ReloadAsync()`, comandi `ToggleSelected` e `ToggleCustomInfo`, stringa `RadioList_Refresh` (it, en, pl), `ITvHost.GoBack/ShowEditStation`, `XamlScanner` di ParityCheck.

Aggiornare la tabella a ogni sessione TV.
