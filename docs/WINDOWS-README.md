# RadioE45 - Guida alla distribuzione Windows

## Prerequisiti

1. **.NET 10 SDK** installato sul tuo PC di sviluppo
2. **Inno Setup 6** → scaricalo da https://jrsoftware.org/isinfo.php

---

## Struttura della cartella WinSetup

```
scripts/
├── build-all.bat            ← Build completa: publish + installer x64 e arm64 in un colpo solo
├── publish-x64.bat          ← Script di publish solo per PC Intel/AMD
├── publish-arm64.bat        ← Script di publish solo per PC ARM64 (es. Snapdragon)
├── RadioE45_x64.iss         ← Script Inno Setup per installer x64
├── RadioE45_arm64.iss       ← Script Inno Setup per installer arm64
├── publish/
│   ├── x64/                 ← Generata dal publish (ignorata da Git)
│   └── arm64/               ← Generata dal publish (ignorata da Git)
└── installer/               ← Cartella di output degli installer (ignorata da Git)
    ├── RadioE45_Setup_x64.exe
    └── RadioE45_Setup_arm64.exe
```

---

## Passi per creare gli installer

### Opzione A — Build completa automatica (consigliata)

Apri il Prompt dei comandi nella cartella `WinSetup` ed esegui:

```
build-all.bat
```

Lo script esegue in sequenza: publish x64, publish arm64, build installer x64, build installer arm64. Al termine trovi entrambi gli installer in `installer\`.

> ⚠️ Verifica che il percorso di `ISCC.exe` nella variabile `ISCC` in cima al batch corrisponda alla tua installazione di Inno Setup.

---

### Opzione B — Build manuale per architettura

**1. Pubblica l'applicazione**

Apri il Prompt dei comandi nella cartella `WinSetup` ed esegui:

**Per x64 (PC Intel/AMD):**
```
publish-x64.bat
```

**Per arm64 (PC con processore ARM, es. Snapdragon X):**
```
publish-arm64.bat
```

> ⚠️ Puoi creare entrambi gli installer dallo stesso PC ARM64: la cross-compilazione è supportata da .NET.

**2. Crea gli installer con Inno Setup**

1. Apri **Inno Setup Compiler**
2. **File → Open** → seleziona `RadioE45_x64.iss` (o `RadioE45_arm64.iss`)
3. Premi **Build → Compile** (oppure `Ctrl+F9`)
4. L'installer verrà creato nella cartella `installer\`

Ripeti per l'altra architettura.

---

### 3. Distribuisci

Invia all'utente il file corretto:

- **`RadioE45_Setup_x64.exe`** → per PC con processore Intel o AMD
- **`RadioE45_Setup_arm64.exe`** → per PC con processore ARM (es. Surface Pro X, Snapdragon X Elite)

L'utente fa doppio click sull'installer e segue la procedura guidata. Nessun requisito aggiuntivo.

---

## Note

- Gli installer includono il runtime .NET (self-contained), quindi l'utente non deve installare nulla di extra.
- Per aggiornare la versione, modifica `AppVersion` nei file `.iss`.
- Le cartelle `publish\` e `installer\` sono escluse da Git tramite `.gitignore`.
- L'icona dell'app viene letta direttamente da `RadioE45.exe` installato, quindi compare correttamente nel pannello Programmi di Windows.