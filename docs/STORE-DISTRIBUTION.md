# RadioE45 - Guida alla distribuzione sugli Store

Questa guida descrive come generare i pacchetti di rilascio per Google Play Store, Apple App Store (iOS) e Mac App Store, usando gli script presenti in questa cartella.

---

## Google Play Store (Android)

### Prerequisiti

1. **.NET 10 SDK** installato
2. **Java JDK** installato (necessario per `keytool`)
3. Un **keystore** con la tua upload key (vedi sotto)

### Creare il keystore (solo la prima volta)

```bash
keytool -genkey -v \
  -keystore ~/.android/radioe45-upload.jks \
  -alias radioe45 \
  -keyalg RSA -keysize 2048 -validity 10000
```

> Conserva il file `.jks` in un posto sicuro. Perderlo significa non poter più aggiornare l'app sul Play Store.

### Configurazione dello script

Apri `build-store-android.sh` e compila le variabili in cima al file:

| Variabile | Dove trovarla |
|---|---|
| `KEYSTORE_PATH` | Percorso del file `.jks` creato sopra |
| `KEY_ALIAS` | L'alias scelto durante la creazione (`radioe45`) |
| `STORE_PASS` | Password del keystore |
| `KEY_PASS` | Password della chiave |

### Build

```bash
./build-store-android.sh
```

L'output è `scripts/store-packages/RadioE45.aab`.

### Upload su Play Console

1. Vai su [play.google.com/console](https://play.google.com/console)
2. Seleziona l'app **RadioE45**
3. **Versioni → Produzione → Crea nuova versione**
4. Carica il file `RadioE45.aab`
5. Compila le note di rilascio e invia per la revisione

---

## Apple App Store (iOS)

> **Aggiornamento 22 ottobre 2026.** Gli script `build-store-*.sh` citati sotto non esistono più (cartella `scripts/` cancellata a giugno 2026). Per iOS usare `dotnet publish RadioE45/RadioE45.csproj -f net10.0-ios -c Release -p:ArchiveOnBuild=true -p:RuntimeIdentifier=ios-arm64` (IPA in `RadioE45/bin/Release/net10.0-ios/ios-arm64/publish/`). Con CarPlay il profilo App Store deve contenere la capability CarPlay Audio: controlli e testi in [`carplay-ios/app-store-carplay.md`](carplay-ios/app-store-carplay.md); profili in [`carplay-ios/provisioning-profiles.md`](carplay-ios/provisioning-profiles.md).

### Prerequisiti

1. **macOS** con Xcode installato
2. **.NET 10 SDK** installato
3. Account **Apple Developer** attivo (99 $/anno)
4. Certificato **iOS Distribution** nel keychain
5. **Provisioning Profile** di tipo *App Store* per `com.radioe45.app`

### Creare certificati e profilo (solo la prima volta)

1. Vai su [developer.apple.com/account/resources/certificates](https://developer.apple.com/account/resources/certificates)
2. Crea un certificato **iOS Distribution**
3. Scaricalo e fai doppio click per aggiungerlo al keychain
4. Vai su [developer.apple.com/account/resources/profiles](https://developer.apple.com/account/resources/profiles)
5. Crea un profilo **App Store** per l'App ID `com.radioe45.app`
6. Scaricalo e aggiungilo a Xcode (Xcode → Settings → Accounts → Download Manual Profiles)

Verifica che il certificato sia installato:

```bash
security find-identity -v -p codesigning | grep "iPhone Distribution"
```

### Configurazione dello script

Apri `build-store-ios.sh` e compila le variabili in cima al file:

| Variabile | Dove trovarla |
|---|---|
| `CODESIGN_KEY` | Output del comando `security find-identity` sopra |
| `PROVISION_PROFILE` | Nome esatto del profilo su Developer Portal |
| `TEAM_ID` | 10 caratteri visibili su [developer.apple.com/account](https://developer.apple.com/account) in alto a destra |

### Build

```bash
./build-store-ios.sh
```

L'output è `scripts/store-packages/RadioE45.ipa`.

### Upload su App Store Connect

**Con Transporter (interfaccia grafica):**

```bash
open -a Transporter
```

Trascina il file `RadioE45.ipa` nella finestra di Transporter e clicca **Deliver**.

**Da riga di comando (API Key):**

```bash
xcrun altool --upload-app \
  -f scripts/store-packages/RadioE45.ipa \
  -t ios \
  --apiKey TUA_API_KEY \
  --apiIssuer TUO_ISSUER_ID
```

> Le credenziali API si generano su App Store Connect → Utenti e accesso → Chiavi API.

Una volta caricato, vai su [appstoreconnect.apple.com](https://appstoreconnect.apple.com), seleziona la build e invia per la revisione.

---

## Mac App Store (macOS)

### Prerequisiti

1. **macOS** con Xcode installato
2. **.NET 10 SDK** installato
3. Account **Apple Developer** attivo (99 $/anno)
4. Certificato **3rd Party Mac Developer Application** nel keychain
5. Certificato **3rd Party Mac Developer Installer** nel keychain
6. **Provisioning Profile** di tipo *Mac App Store* per `com.radioe45.app`

### Creare certificati e profilo (solo la prima volta)

1. Vai su [developer.apple.com/account/resources/certificates](https://developer.apple.com/account/resources/certificates)
2. Crea **entrambi** i certificati:
   - **Mac App Distribution** (firma l'app)
   - **Mac Installer Distribution** (firma il pacchetto `.pkg`)
3. Scaricali e fai doppio click per aggiungerli al keychain
4. Crea un profilo **Mac App Store** per `com.radioe45.app` e aggiungilo a Xcode

Verifica che entrambi i certificati siano installati:

```bash
security find-identity -v -p codesigning | grep "3rd Party Mac Developer"
```

### Configurazione dello script

Apri `build-store-macos.sh` e compila le variabili in cima al file:

| Variabile | Dove trovarla |
|---|---|
| `APP_SIGN_KEY` | Riga "3rd Party Mac Developer **Application**" dall'output sopra |
| `PKG_SIGN_KEY` | Riga "3rd Party Mac Developer **Installer**" dall'output sopra |
| `PROVISION_PROFILE` | Nome esatto del profilo su Developer Portal |
| `TEAM_ID` | 10 caratteri su [developer.apple.com/account](https://developer.apple.com/account) |

### Build

```bash
./build-store-macos.sh
```

L'output è `scripts/store-packages/RadioE45.pkg`.

### Upload su App Store Connect

**Con Transporter (interfaccia grafica):**

```bash
open -a Transporter
```

Trascina il file `RadioE45.pkg` nella finestra di Transporter e clicca **Deliver**.

**Da riga di comando (API Key):**

```bash
xcrun altool --upload-app \
  -f scripts/store-packages/RadioE45.pkg \
  -t osx \
  --apiKey TUA_API_KEY \
  --apiIssuer TUO_ISSUER_ID
```

Una volta caricato, vai su [appstoreconnect.apple.com](https://appstoreconnect.apple.com), seleziona la build macOS e invia per la revisione.

---

## Linux (`.deb` su GitHub Releases, preview)

App Uno (`RadioE45.Uno`, desktop Skia), due pacchetti: `radioe45_<versione>_amd64.deb` (x64) e `radioe45_<versione>_arm64.deb` (arm64, anche Raspberry Pi). Nessuno store: i file vanno nella release di GitHub.

### Prerequisiti

1. **.NET 10 SDK**
2. **dpkg** sul Mac: `brew install dpkg` (fornisce `dpkg-deb`)
3. **gh** autenticato, solo per il caricamento

### Build

```bash
./build-deb-linux.sh                 # crea i due .deb in installer/ (anche dentro 0.build-all.sh)
./build-deb-linux.sh --upload v0.45  # li crea e li carica nella release v0.45 (che deve esistere)
```

Lo script chiama `packaging/linux/build-deb.sh` del repo (publish self-contained per `linux-x64` e `linux-arm64`, pacchetto in `/opt/radioe45`, comando `radioe45`, voce di menu e icone). libVLC arriva dalla distribuzione tramite le dipendenze del pacchetto.

### Verifica (GitHub Actions)

Workflow **Linux .deb** (`.github/workflows/linux-deb.yml`), su runner x64 e arm64: installa il pacchetto con `apt`, avvia l'app sotto Xvfb (screenshot negli artefatti) e prova l'installazione su Ubuntu 22.04, Debian 12 e Debian 13.

- Con `--upload` lo script avvia il workflow sui pacchetti della release (serve il workflow su `main`).
- A mano: Actions → Linux .deb → Run workflow, con il tag (vuoto = build dal branch).
- Ogni push che modifica `packaging/linux/` lo avvia sul branch.

### Testo per la release (in inglese)

```markdown
### Linux (preview)
Download `radioe45_<version>_amd64.deb` (x64) or `radioe45_<version>_arm64.deb` (ARM64, e.g. Raspberry Pi), then:

    sudo apt install ./radioe45_<version>_amd64.deb

Tested on Ubuntu 24.04. Requires a distribution with apt (Ubuntu 22.04+, Debian 12+, Linux Mint, …). This is a preview: please report issues on GitHub.
```

Disinstallazione: `sudo apt remove radioe45` (i dati restano in `~/.local/share/RadioE45/`).

---

## Note generali

- La cartella `store-packages/` è esclusa da Git tramite `.gitignore`.
- I certificati Apple e il keystore Android non vanno mai committati nel repository.
- La versione dell'app si alza solo in `version.json` (vedi `VERSION-BUMP.md`). La release su GitHub (changelog, bozza, pacchetti, pubblicazione) è in `RELEASE.md`.
