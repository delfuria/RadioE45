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

### Android TV / Google TV

La TV usa lo **stesso AAB** del telefono (stesso `com.radioe45.app`): non c'è un secondo pacchetto. Il manifest dichiara `leanback` e `touchscreen` con `required="false"` e il banner di avvio, quindi Play lo mostra anche sui dispositivi TV.

**ABI (controllo obbligatorio prima di caricare).** Il `RadioE45.csproj` fissa per Release `RuntimeIdentifiers = android-arm;android-arm64;android-x64`. Senza `android-arm` l'AAB conteneva solo `arm64-v8a` e `x86_64`, e i TV a 32 bit (il Sony BRAVIA 4K VH21 di prova è `armeabi-v7a`) non potevano installarlo. Dopo `./build-store-android.sh` verificare:

```bash
unzip -l scripts/store-packages/RadioE45.aab | awk '{print $4}' | grep -E '^base/lib/[^/]+/' | cut -d/ -f3 | sort | uniq -c
# atteso: arm64-v8a, armeabi-v7a, x86_64 (circa 150 file ciascuno)
```

**Play Console** (una tantum):
1. **Rilascio → Configurazione → Impostazioni avanzate → Fattori di forma → Android TV**: aggiungere il form factor e abilitare la distribuzione. Compilare il questionario/controlli di qualità TV che la console propone (navigazione col D-pad, nessuna dipendenza dal touch, banner presente).
2. **Presenza sullo store → Scheda dello store principale → Android TV**: caricare gli asset TV:
   - **Banner TV** 1280x720: `docs/images/tv/banner-1280x720.png` (logo E45, senza trasparenza).
   - **Screenshot TV** 16:9 (1920x1080, PNG senza alfa): `docs/images/tv/01-home.png`, `02-request.png`, `03-schedule.png`, `04-channels.png` (stessa numerazione delle altre piattaforme).
3. Testi: la scheda è quella del telefono. In **Descrizione completa** aggiungere in inglese (vedi sotto) una riga sulla TV.

Testo da aggiungere alla descrizione completa (inglese):

> **On your TV:** Radio E45 also runs on Android TV and Google TV, designed for the remote control — now playing with previous/next station and favorites, song requests, podcasts, the schedule, and adding or editing stations with the on-screen keyboard.

**Test prima di caricare:** installare l'AAB (o un APK `android-arm`) sul TV Sony e sull'emulatore `Television_1080p`, come in `ANDROID-TV-ROADMAP.md` §2. Il gate di parità TV (`dotnet run --project tools/ParityCheck -- --strict-tv`) deve restituire 0 (nessun `tv todo`).

> Gli screenshot si fanno sempre con **Radio E45** sintonizzata (Canali → Radio E45), mai con le stazioni di prova; `adb exec-out screencap -p`, poi togliere l'alfa.

---

## Apple App Store (iOS)

> **Aggiornamento 22 ottobre 2026.** Gli script `build-*.sh` non sono nel repository: stanno in `~/Library/CloudStorage/OneDrive-Personale/Lavori/RadioE45-Scripts/scripts/` (per iOS App Store: `build-applestore-ios.sh`, che scrive `store-packages/RadioE45.ipa` in quella cartella). Con CarPlay il profilo App Store deve contenere la capability CarPlay Audio: controlli e testi in [`carplay-ios/app-store-carplay.md`](carplay-ios/app-store-carplay.md); profili in [`carplay-ios/provisioning-profiles.md`](carplay-ios/provisioning-profiles.md).

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
