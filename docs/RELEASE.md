# RadioE45 — Release su GitHub

Le build si fanno in locale (firme Apple, Inno Setup, keystore Android). GitHub gestisce solo changelog, bozza, tag e pubblicazione, con il workflow **Release** (`.github/workflows/release.yml`, solo lancio manuale, solo da `main`).

## Regole

- Versione e tag: due cifre, da `version.json` (es. `0.47` → tag `v0.47`).
- `CHANGELOG.md` (inglese) ha `## [Unreleased]` sempre in cima; ogni modifica visibile all'utente aggiunge una riga lì. Alla release diventa `## [0.47] - AAAA-MM-GG`. Il corpo della release GitHub è la sezione di quella versione, più il testo "Linux (preview)".
- Claude prepara i file e propone i comandi (skill `release-prep`); non esegue commit, tag, release.

## Sequenza

1. **Preparare**: `/release-prep` (changelog, controlli, proposta di commit). Eventuale bump di `version.json`. Commit e push su `main`.
2. **Bozza**: GitHub → Actions → Release → Run workflow → `create-draft` (opzione `prerelease` per le anteprime). Controlla che `version.json` abbia due cifre, che il tag non esista e che il changelog abbia la sezione; crea la release **in bozza**. Una bozza non ha ancora il tag ed è ignorata dal controllo aggiornamenti dell'app (`GitHubReleaseUpdateSource`), quindi nessun utente vede una versione incompleta.
3. **Pacchetti**: build locali e caricamento sulla bozza (es. `./build-deb-linux.sh --upload v0.47`; per gli altri file `gh release upload v0.47 <file>`). File attesi: `RadioE45.apk`, `RadioE45.dmg`, `radioe45_<ver>_amd64.deb`, `radioe45_<ver>_arm64.deb`, `RadioE45_Setup_x64.exe`, `RadioE45_Setup_arm64.exe`, `RadioE45_<ver>_portable_win-x64.zip`, `RadioE45_<ver>_portable_win-arm64.zip`.
4. **Smoke test Linux** (facoltativo): Actions → Linux .deb → Run workflow con `release_tag` = `v0.47`.
5. **Pubblicazione**: Actions → Release → Run workflow → `publish`. Se manca un pacchetto, fallisce e lo elenca; altrimenti pubblica, crea il tag sul commit della bozza e la marca `latest`.
6. **Store**: Google Play, App Store, Mac App Store e Microsoft Store (vedi `STORE-DISTRIBUTION.md`; testi in `store-descriptions.md`, in inglese). Il sito web si aggiorna con `pages.yml` al push su `main`.

## Se qualcosa va storto

- Bozza sbagliata: eliminala (`gh release delete v0.47`, nessun tag creato) e rilancia `create-draft`.
- Changelog da correggere dopo la bozza: modifica `CHANGELOG.md`, push, elimina la bozza e ricreala (i pacchetti vanno ricaricati).
