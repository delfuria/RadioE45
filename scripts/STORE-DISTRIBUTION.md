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

## Note generali

- La cartella `store-packages/` è esclusa da Git tramite `.gitignore`.
- I certificati Apple e il keystore Android non vanno mai committati nel repository.
- Per aggiornare la versione dell'app, modifica `ApplicationDisplayVersion` in `RadioE45/RadioE45.csproj`.
