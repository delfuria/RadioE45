# Migrazione Linux e Web (Uno Platform) — cosa resta da fare

Stato al 2026-11-02. Dettagli per sessione in [`LINUX-UNO-ROADMAP.md`](LINUX-UNO-ROADMAP.md) (§12). Già chiuso: S0–S8 (S8.7 incluso), S9a, S9b (codice).

## 1. Prove utente pendenti

- [ ] **Web, podcast:** prova a mano con la scheda visibile (la prova automatica non è valida: Chrome sospende i media nelle schede nascoste).
- [ ] **PWA, Chrome/Edge:** installazione dall'icona nella barra degli indirizzi e avvio in finestra propria.
- [ ] **PWA, Safari:** Archivio → Aggiungi al Dock.
- [ ] **Media di sistema, VM Ubuntu:** pannello multimediale di GNOME e tasti multimediali reali.
- [ ] **Media di sistema, browser:** controllo multimediale della barra di Chrome e tasti della tastiera.

## 2. S9c — test sugli ambienti (VM + Mac)

- [ ] Linux: GNOME Wayland, KDE, Xfce su X11 puro ("Ubuntu on Xorg", in sospeso da S1).
- [ ] Browser: Chrome, Firefox (audio mai provato), Safari.
- [ ] Correzioni emerse; regressione MAUI se si tocca il Core.

## 3. S10.0 — pacchetti `.deb` su GitHub Releases

- [ ] **VM, utente:** `sudo apt install` del `.deb` arm64; voce "Radio E45" nel menu con icona, icona giusta nella dock, audio, podcast, MPRIS; poi `sudo apt remove radioe45`.
- [ ] **CI:** primo giro verde di `.github/workflows/linux-deb.yml` (push del branch); screenshot x64 e arm64 controllati.
- [ ] **CI x64:** verifica del fix SkiaSharp `NoDependencies` (rinviata da S1).
- [ ] **Prima release:** `build-deb-linux.sh --upload <tag>` e verifica CI sui file della release (workflow su `main`).
- [ ] **Branch:** confermare che `S9b` e i sotto-branch siano integrati (la repo è su `main`).

## 4. S10.1 — snap, AppImage, CI completa, hosting

- [ ] Snap arm64 in VM; snap x64 in CI.
- [ ] AppImage x64 e arm64.
- [ ] Workflow GitHub Actions: build, test, `ParityCheck`, publish desktop e web, artefatti.
- [ ] **Decisione D8, hosting web:** GitHub Pages, Cloudflare o Azure SWA; dominio.
- [ ] Aggiornare `docs/VERSION-BUMP.md` e `docs/STORE-DISTRIBUTION.md`.

## 5. S11 — rilascio

- [ ] `ParityCheck --strict` verde: oggi resta il debito dichiarato `settings.diagnostics.crash-reports` (desktop, web).
- [ ] Beta: snap channel `beta`; web su URL di staging.
- [ ] Testi e screenshot store in inglese; README con installazione Linux (già fatta) e link web.
- [ ] Rilascio stable (tag e release lanciati dall'utente, mai da Claude).
- [ ] Merge del branch su `main` (fatto dall'utente).
