# RadioE45 — Guida all'aggiornamento del numero di versione

Ogni volta che si rilascia una nuova versione, vanno aggiornati **manualmente** i seguenti file.
I file auto-generati dalla build NON vanno toccati: vengono rigenerati automaticamente.

---

## File da aggiornare manualmente

### 1. `RadioE45/RadioE45.csproj` (righe 32–34)

**Fonte di verità** per tutte le piattaforme.

```xml
<ApplicationDisplayVersion>0.20</ApplicationDisplayVersion>
<ApplicationVersion Condition="...=='windows'">0</ApplicationVersion>
<ApplicationVersion Condition="...!='windows'">20</ApplicationVersion>
```

| Campo | Descrizione | Note |
|---|---|---|
| `ApplicationDisplayVersion` | Versione visibile all'utente (es. `0.20`) | Uguale su tutte le piattaforme |
| `ApplicationVersion` (Windows) | Deve essere **sempre 0** | Vincolo Microsoft Store: il quarto campo MSIX deve essere 0 |
| `ApplicationVersion` (Android/iOS/macOS) | Build counter intero ≥ 1 | Per Android è il `versionCode`; incrementare ad ogni release |

**Regola pratica**: se la `ApplicationDisplayVersion` passa da `0.20` a `0.21`, impostare `ApplicationVersion` (non-Windows) a `21`.

---

### 2. `scripts/RadioE45_x64.iss` (riga 3)

Installer Inno Setup per Windows x64.

```ini
AppVersion=0.20
```

---

### 3. `scripts/RadioE45_arm64.iss` (riga 3)

Installer Inno Setup per Windows arm64.

```ini
AppVersion=0.20
```

---

### 4. `CLAUDE.md` (riga 9)

Documentazione di progetto — aggiornare per coerenza.

```markdown
**App ID:** `com.radioe45.app` | **Version:** 0.20
```

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

> **Nota**: la cartella `store-packages/` contiene i binari dell'ultima build Windows.
> Se vedi una versione vecchia lì dentro, ignorala: viene cancellata e ricreata dal
> passo `rmdir /s /q` in `build-store-windows.bat` ad ogni nuovo publish.

---

## Checklist di release

- [ ] `RadioE45.csproj` → `ApplicationDisplayVersion` aggiornata
- [ ] `RadioE45.csproj` → `ApplicationVersion` (non-Windows) incrementata
- [ ] `RadioE45_x64.iss` → `AppVersion=` aggiornata
- [ ] `RadioE45_arm64.iss` → `AppVersion=` aggiornata
- [ ] `CLAUDE.md` → riga `Version:` aggiornata
- [ ] Commit su `Nightly-Build`
- [ ] Build Android: `1.build-allstore.sh`
- [ ] Build Windows MSIX: `2.build-mac2winstore.sh`
- [ ] Build macOS: `0.build-all.sh`
