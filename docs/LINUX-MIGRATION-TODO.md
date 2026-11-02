# Migrazione Linux e Web (Uno Platform) — cosa resta da fare

Stato al 2026-11-02. Dettagli per sessione in [`LINUX-UNO-ROADMAP.md`](LINUX-UNO-ROADMAP.md) (§12). Già chiuso: S0–S8 (S8.7 incluso), S9a, S9b, S9c.

## 1. Prove utente pendenti

- [x] **Web, podcast:** prova a mano con la scheda visibile — ok in Chrome e Safari (2026-11-02; Safari richiedeva un `<audio>` al posto del `<video>` di Uno).
- [x] **PWA, Chrome/Edge:** installazione dall'icona nella barra degli indirizzi e avvio in finestra propria — ok.
- [x] **PWA, Safari:** Archivio → Aggiungi al Dock — ok.
- [x] **Media di sistema, VM Ubuntu:** scheda multimediale di GNOME (pausa, play, avanti, indietro) e tasti — ok (2026-11-02). Controlli sulla schermata di blocco: non compaiono, ma nemmeno per altri programmi nella VM, quindi è un limite della VM e non dell'app.
- [x] **Media di sistema, browser:** controllo multimediale della barra di Chrome e tasti della tastiera — ok.

## 2. S9c — test sugli ambienti (VM + Mac) — ✅ completata 2026-11-02

- [x] Linux, GNOME Wayland: ok (VM Ubuntu 24.04 ARM64, 2026-11-02).
- [x] Linux, "Ubuntu on Xorg": finestra 500×855 subito e non ridimensionabile (min = max), radio, MPRIS (pausa, play, avanti, indietro) ok (2026-11-02).
- [x] Linux, Xfce (Ubuntu 24.04 arm64, 2026-11-02): trovato un crash all'avvio (`symbol lookup error: FT_Get_BDF_Property` in `libSkiaSharp.so`, causato dai font bitmap di `xfonts-base` installati con Xfce), corretto caricando `libfreetype.so.6` nello scope globale in `Platforms/Desktop/Program.cs`; poi finestra 500×855 fissa, radio e MPRIS ok. Da verificare su x64 (CI).
- [x] Linux, KDE Plasma X11 e Wayland (2026-11-02): finestra 500×855 fissa, radio, MPRIS e widget a schermo bloccato ok.
- [x] **Regressione MAUI dei podcast** (iOS, Android, Mac, Windows): ok dappertutto (il Core è stato toccato: `PodcastPlaybackService`).
- [x] Browser: Chrome, Firefox, Safari.

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
