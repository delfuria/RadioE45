# RadioE45 — Guida al numero di versione (Nerdbank.GitVersioning)

Il versioning è **automatico** tramite [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) (NBGV). Non si editano più a mano `ApplicationVersion`/`ApplicationDisplayVersion` nel `.csproj` ad ogni release.

---

## Fonte di verità: `version.json`

```json
{
  "version": "0.32",
  ...
}
```

Il campo `version` è l'unico punto da toccare per alzare la versione pubblica (es. `0.32` → `0.33`).

**Regola pratica**: `version.json` deve sempre essere **committato**. Se resta non tracciato, `GitVersionHeight` rimane 0 e il build number (quarta cifra / build counter per piattaforma) non avanza.

---

## Come funziona

- `Nerdbank.GitVersioning` è referenziato come `PackageReference` in `RadioE45/RadioE45.csproj` e calcola versione + build number da `version.json` + altezza del commit corrente ad ogni build, senza intervento manuale.
- I target MAUI built-in di NBGV (`NBGV_SetVersionForMauiAndroid` / `...iOS` / `...Windows`) valorizzano `ApplicationVersion` e `ApplicationDisplayVersion` per piattaforma:
  - **Android**: `ApplicationVersion` → `versionCode` (bit-packed da NBGV).
  - **iOS/macOS**: build counter incrementale.
  - **Windows**: NBGV produrrebbe di default `ApplicationVersion` vuoto e display version a 4 parti — non valido per lo Store, che richiede revision `0`. Per questo `RadioE45.csproj` (target `RestoreWindowsVersionConvention`, dopo `NBGV_SetVersionForMauiWindows` e prima di `MauiGeneratePackageAppxManifest`) forza `ApplicationVersion=0` e `ApplicationDisplayVersion=$(MajorMinorVersion)` (preso da `version.json`).
- La pagina Impostazioni (`SettingsViewModel.cs`) mostra versione, build e hash commit corto (`ThisAssembly.GitCommitId`) automaticamente — nessun aggiornamento manuale necessario lì.

---

## Per alzare la versione

1. Modifica `version.json` → campo `"version"`.
2. Commit (obbligatorio, vedi sopra).
3. Basta. Nessun altro file da editare a mano.

---

## File auto-generati (NON modificare)

Questi file vengono rigenerati ad ogni build: modificarli manualmente è inutile.

| File | Generato da |
|---|---|
| `RadioE45/store-packages/AppxManifest.xml` | `dotnet publish` MSIX (Windows) |
| `RadioE45/store-packages/ForBundle/AppxManifest.xml` | `dotnet publish` MSIX (Windows) |
| `RadioE45/Platforms/Windows/Package.appxmanifest` | Template MAUI — usa placeholder `0.0.0.0` |
| `RadioE45/obj/*/resizetizer/m/Package.appxmanifest` | Intermediati di build |
| `RadioE45/store-packages/RadioE45_X.Y.Z.Z_*.msix` | Pacchetti finali MSIX |
| `RadioE45.Version.cs` (generato da NBGV) | Costanti compile-time `ThisAssembly.*`, incluso `GitCommitId` |

> **Nota**: la cartella `store-packages/` contiene i binari dell'ultima build Windows.
> Se vedi una versione vecchia lì dentro, ignorala: viene cancellata e ricreata dal
> passo `rmdir /s /q` in `build-store-windows.bat` ad ogni nuovo publish.

> **Nota su script installer**: eventuali script Inno Setup (`.iss`) o batch di build Windows che referenziano la versione **non sono presenti in questo repository** al momento. Se servono per il packaging, vanno individuati e allineati separatamente a `version.json` prima di fare affidamento su questa guida per quel passo.

---

## Checklist di release

- [ ] `version.json` → `version` aggiornato (se serve bump) e **committato**
- [ ] Build Android
- [ ] Build Windows MSIX
- [ ] Build macOS
