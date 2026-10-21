---
name: release-prep
description: Prepara una release di RadioE45 senza eseguirla: controlla version.json e CHANGELOG.md, sposta [Unreleased] nella sezione della versione, lancia i controlli (ParityCheck --strict, test, build) e stampa la proposta di commit in inglese e la sequenza dei passi manuali. Usare quando l'utente chiede di "preparare la release", "chiudere il changelog", "rilasciare 0.xx" o scrive /release-prep. Input opzionale: la nuova versione a due cifre (es. 0.47).
---

# Preparazione release

Riferimenti: `docs/RELEASE.md`, `docs/VERSION-BUMP.md`, `CHANGELOG.md`, `.github/workflows/release.yml`.

**Non eseguire mai** `git commit`, `git push`, `git tag`, `gh release create/edit` né il workflow Release: si preparano i file e si propongono i comandi. Tutto il testo per l'utente (changelog, commit, note) è in inglese.

## 1. Versione

1. Leggere `version.json` (`version`, due cifre) e l'ultimo tag (`git tag --sort=-v:refname | head -3`, `git ls-remote --tags origin`).
2. Se l'utente ha indicato una versione diversa da `version.json`, o se il tag `v<version>` esiste già: proporre il bump di `version.json` e fermarsi finché l'utente non conferma. `version.json` va modificato solo su sua richiesta.
3. Il tag è `v<version>` (due cifre).

## 2. Changelog

1. `CHANGELOG.md` deve avere `## [Unreleased]` con almeno una voce. Se è vuoto, confrontare `git log <ultimo-tag>..HEAD` con le voci presenti e proporre quelle mancanti (solo modifiche visibili all'utente, una riga ciascuna, niente nomi di file o classi). Mostrare le aggiunte prima di scriverle.
2. Rinominare `## [Unreleased]` in `## [<version>] - <data di oggi, YYYY-MM-DD>` e inserire sopra una nuova `## [Unreleased]` vuota.
3. Controllare che la sezione sia estraibile come fa il workflow:
   `awk -v ver="<version>" '/^## \[/ { if (found) exit; if (index($0, "## [" ver "]") == 1) found = 1; next } found { print }' CHANGELOG.md`
4. Controllare che le sezioni di versioni già rilasciate non siano state modificate.

## 3. Controlli

Eseguire e riportare l'esito (se qualcosa fallisce, dirlo con l'output decisivo e fermarsi):

```bash
dotnet run --project tools/ParityCheck -- --strict --json   # in release anche il debito è errore
dotnet test --project RadioE45.Core.Tests/RadioE45.Core.Tests.csproj
dotnet build RadioE45.slnx
dotnet build RadioE45.Uno -f net10.0-desktop
dotnet build RadioE45.Uno -f net10.0-browserwasm
```

Con `--strict` un debito di parità dichiarato è un errore: elencarlo e chiedere all'utente se rilasciare comunque (allora i `Parity-Debt` finiscono nelle note) o chiuderlo prima con `ui-parity-port`.

## 4. Output finale

Stampare, in inglese e come testo:

1. **COMMIT PROPOSAL**: titolo imperativo (max 72 caratteri, es. `Prepare release 0.47`), descrizione con cosa cambia (`CHANGELOG.md`, eventuale `version.json`), trailer `Parity: maui, desktop, web`, e l'elenco `git add` dei file.
2. **Sequenza manuale** (da `docs/RELEASE.md`, con tag e versione reali):
   1. Commit e push su `main`.
   2. GitHub → Actions → Release → Run workflow → `create-draft`.
   3. Build locali e `--upload <tag>` (Android, macOS, Windows, `.deb`).
   4. (facoltativo) Actions → Linux .deb → Run workflow con `release_tag`.
   5. Actions → Release → Run workflow → `publish`.
   6. Pubblicazione negli store con i testi di `docs/store-descriptions.md`.
3. Le verifiche non fatte (piattaforme non provate su questa macchina).
