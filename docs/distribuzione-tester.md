# RadioE45 — Distribuzione ai Tester (senza App Store)

## Prerequisiti

- Build completata con `dotnet publish -f net10.0-maccatalyst -c Release`
- Il file `RadioE45.app` si trova in:
  ```
  RadioE45/bin/Release/net10.0-maccatalyst/RadioE45.app
  ```

---

## Step 1 — Esegui il build di Release

```bash
dotnet publish -f net10.0-maccatalyst -c Release RadioE45/RadioE45.csproj
```

Attendi il completamento. Il file `.app` viene generato in:
```
RadioE45/bin/Release/net10.0-maccatalyst/RadioE45.app
```

---

## Step 2 — Crea il file ZIP da distribuire

Apri il Terminale e lancia:

```bash
cd RadioE45/bin/Release/net10.0-maccatalyst/

zip -r RadioE45-v0.5.1.zip RadioE45.app
```

Il file `RadioE45-v0.5.1.zip` viene creato nella stessa cartella.

> Aggiorna il numero di versione nel nome del file ad ogni nuova build
> (corrisponde a `ApplicationDisplayVersion` nel file `.csproj`).

---

## Step 3 — Distribuisci il file ZIP

Invia `RadioE45-v0.5.1.zip` ai tester tramite:

- Email
- WeTransfer / Dropbox / iCloud Drive / Google Drive
- Qualsiasi altro canale di condivisione file

Includi nell'invio il file `istruzioni-tester.html` (vedi cartella `docs/`).

---

## Step 4 — Per ogni nuova versione

1. Aggiorna `ApplicationDisplayVersion` nel file `RadioE45.csproj`
2. Esegui di nuovo il build (Step 1)
3. Crea un nuovo ZIP con il numero di versione aggiornato (Step 2)
4. Invia ai tester

---

## Note

- L'app **non è firmata con Developer ID**: i tester devono sbloccarla manualmente
  seguendo le istruzioni nel file `istruzioni-tester.html`.
- Il file ZIP contiene solo il bundle `.app`, non un installer `.pkg`.
- Ogni tester deve ripetere lo sblocco solo la prima volta che installa l'app.
- Se si aggiorna l'app (stesso nome, stessa cartella), di solito non serve
  ripetere lo sblocco.
