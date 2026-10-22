# Profili di provisioning — copia e rigenerazione per CarPlay

> Scritto il 21 ottobre 2026, dopo l'approvazione dell'entitlement CarPlay Audio. Dati misurati sul Mac di sviluppo quel giorno.

## 1. Cosa c'è oggi

Sullo stesso App ID `VZWMBQL256.com.radioe45.app` ci sono **tre** profili, non due:

| Profilo | Tipo | Piattaforma | Gestito da | Scadenza | File locale |
|---|---|---|---|---|---|
| `iOS Team Provisioning Profile: com.radioe45.app` | Development (2 dispositivi) | iOS | **Xcode** (automatico) | 13 giugno 2027 | `87b544af-269e-4adb-8380-53b5b1c41475.mobileprovision` |
| `Radio E45 AppStore` | Distribution (App Store) | iOS | manuale (portale) | 14 giugno 2027 | `45311f79-04d0-4090-9e98-8a6d4fc69e0c.mobileprovision` |
| `Radio E45 Mac AppStore` | Distribution (Mac App Store) | macOS | manuale (portale) | 14 giugno 2027 | `Radio_E45_Mac_AppStore.provisionprofile` |

Cartella dei file locali: `~/Library/Developer/Xcode/UserData/Provisioning Profiles/`

Tre conseguenze:
- **Il profilo Mac** usa lo stesso App ID. Quando aggiungi la capability CarPlay all'App ID, Apple segna come non validi **tutti** i profili di quell'App ID, compreso quello Mac. Se non lo rigeneri, la prossima release per Mac fallisce in firma.
- **Il profilo Development è gestito da Xcode** e dal portale non si modifica. Ti propongo di sostituirlo con un profilo Development **manuale** (§4.3), così la firma delle build Debug su device diventa esplicita e controllabile da `dotnet build`.
- **I certificati non cambiano.** Rigenerare un profilo non tocca i certificati "Apple Development" e "Apple Distribution" nel Portachiavi.

## 2. Perché la copia è solo una rete di sicurezza

I profili nuovi sono un **sovrainsieme** dei vecchi: hanno tutte le capability di prima più CarPlay. Un'app che **non** richiede CarPlay si firma senza problemi anche con il profilo nuovo.

Il ritorno indietro, se servisse, non passa dai vecchi profili. Basta togliere `com.apple.developer.carplay-audio` da `Platforms/iOS/Entitlements.plist` e continuare con i profili nuovi.

La copia serve per due cose:
- confrontare i vecchi profili con i nuovi;
- avere i file se qualcosa va storto durante la rigenerazione.

**Non è garantito** che Apple accetti un caricamento su App Store firmato con un profilo segnato "Invalid" nel portale.

## 3. Copia dei profili attuali

### 3.1 Copia dei file locali

Da Terminale (la cartella di destinazione è fuori dal repo: i profili **non** vanno nel repository):

```bash
DEST="$HOME/Documents/RadioE45-profili-backup-$(date +%Y%m%d)"
mkdir -p "$DEST"
SRC="$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles"
cp "$SRC/87b544af-269e-4adb-8380-53b5b1c41475.mobileprovision" \
   "$SRC/45311f79-04d0-4090-9e98-8a6d4fc69e0c.mobileprovision" \
   "$SRC/Radio_E45_Mac_AppStore.provisionprofile" \
   "$DEST/"
ls -l "$DEST"
```

Controllo (stampa nome e scadenza di ogni copia):

```bash
for f in "$DEST"/*; do
  security cms -D -i "$f" > /tmp/profilo.plist
  echo "$(plutil -extract Name raw /tmp/profilo.plist) — scade $(plutil -extract ExpirationDate raw /tmp/profilo.plist)"
done
```

Devono comparire i tre nomi della tabella §1.

### 3.2 Copia dal portale (facoltativa, conferma che i file locali sono aggiornati)

1. Vai su <https://developer.apple.com/account/resources/profiles/list>.
2. Per **Radio E45 AppStore** e **Radio E45 Mac AppStore**: apri il profilo, premi **Download** e salva il file nella stessa cartella `DEST`, aggiungendo `-portale` al nome.
3. Il profilo Team gestito da Xcode compare nella lista con l'indicazione "Managed by Xcode" e non si scarica da lì: basta la copia locale.

### 3.3 Certificati (facoltativa, ma utile una volta)

Se non hai mai esportato i certificati, fallo ora. Non cambiano con questa operazione, ma senza la chiave privata non si firma più da un altro Mac.
1. Apri **Accesso Portachiavi** → *Portachiavi login* → *I miei certificati*.
2. Seleziona "Apple Distribution: Stefano Del Furia (VZWMBQL256)", poi tasto destro → **Esporta** → formato `.p12`, con una password robusta.
3. Ripeti per "Apple Development: Stefano Del Furia (G2686Y5924)".
4. Conserva i `.p12` in un posto sicuro (gestore di password o disco cifrato), **non** nel repo e non in chiaro nel cloud.

## 4. Rigenerazione

### 4.1 Abilitare CarPlay sull'App ID

1. <https://developer.apple.com/account/resources/identifiers/list>, poi apri **com.radioe45.app**.
2. Cerca la capability CarPlay. Dopo l'approvazione può trovarsi nella scheda **Capabilities** oppure in una scheda separata come **Additional Capabilities** (il nome esatto non è verificato: cerca "CarPlay").
3. Spunta **CarPlay Audio App** (dall'email di Apple: "CarPlay Audio App (CarPlay framework)").
4. Premi **Save**. Il portale avvisa che i profili esistenti diventeranno non validi: conferma.

**Misura:** nella lista dei profili, i due profili App Store (iOS e Mac) risultano ora **Invalid**. È atteso.

### 4.2 Rigenerare i profili App Store (iOS e Mac)

Per **Radio E45 AppStore** e poi per **Radio E45 Mac AppStore**:
1. <https://developer.apple.com/account/resources/profiles/list>: apri il profilo.
2. Premi **Edit**.
3. Controlla che il certificato selezionato sia "Apple Distribution: Stefano Del Furia" e che l'App ID sia `com.radioe45.app`. Non cambiare il **nome** del profilo: il progetto lo cerca per nome (`CodesignProvision` in `RadioE45.csproj`).
4. Premi **Save**, poi **Download**.
5. Fai doppio clic sul file scaricato. Se non viene installato da solo, copialo in `~/Library/Developer/Xcode/UserData/Provisioning Profiles/`.

### 4.3 Profilo Development (nuovo, manuale)

Al posto del profilo Team gestito da Xcode:
1. <https://developer.apple.com/account/resources/profiles/add>.
2. Tipo: **iOS App Development**, poi **Continue**.
3. App ID: **com.radioe45.app**.
4. Certificato: **Apple Development: Stefano Del Furia**.
5. Dispositivi: seleziona il tuo iPhone (e l'iPad se c'è). Se l'iPhone non compare, va registrato prima in *Devices*.
6. Nome: **`Radio E45 Development`**. Usa esattamente questo nome, il progetto lo cercherà così.
7. **Generate**, poi **Download**, poi doppio clic.

Il vecchio profilo Team resta sul Mac e Xcode lo aggiornerà da solo quando serve: non va cancellato.

### 4.4 Verifica dei profili nuovi

Ogni profilo nuovo deve contenere l'entitlement CarPlay. Da Terminale:

```bash
cd "$HOME/Library/Developer/Xcode/UserData/Provisioning Profiles"
for f in *.mobileprovision *.provisionprofile; do
  security cms -D -i "$f" > /tmp/profilo.plist 2>/dev/null || continue
  n=$(plutil -extract Name raw /tmp/profilo.plist)
  case "$n" in *"Radio E45"*|*radioe45*)
    c=$(plutil -extract Entitlements xml1 -o - /tmp/profilo.plist | grep -c carplay-audio)
    echo "$n — creato $(plutil -extract CreationDate raw /tmp/profilo.plist) — carplay-audio: $c";;
  esac
done
```

**Atteso:** `Radio E45 AppStore`, `Radio E45 Mac AppStore` e `Radio E45 Development` con data di oggi e `carplay-audio: 1`.

Se nella cartella restano anche le versioni vecchie con lo stesso nome, la firma potrebbe prendere quella sbagliata. Dopo aver controllato che la copia del §3.1 esista, togli i file vecchi (quelli con data di creazione precedente) dalla cartella.

### 4.5 Avvisarmi

Quando la verifica del §4.4 è a posto, scrivimelo. Io:
1. aggiungo `com.apple.developer.carplay-audio` a `Platforms/iOS/Entitlements.plist`;
2. aggiungo in `RadioE45.csproj` la firma esplicita per le build Debug su device (`Radio E45 Development` più `Entitlements.plist`);
3. verifico con `codesign` che la build per device e quella App Store contengano l'entitlement;
4. controllo che la build per Mac (`Radio E45 Mac AppStore` rigenerato) firmi ancora correttamente.

Poi si passa alle prove di [`device-test-plan.md`](device-test-plan.md).

## 5. Se qualcosa va storto

| Sintomo | Causa probabile | Cosa fare |
|---|---|---|
| `dotnet build` per device: "No valid provisioning profile found" o profilo non trovato | Nome diverso da quello nel csproj, oppure profilo non installato | Controlla il nome con il comando del §4.4 |
| Firma riuscita ma CarPlay assente sull'iPhone | Il profilo non contiene `carplay-audio` oppure la build non richiede l'entitlement | §4.4; poi `codesign -d --entitlements - <app>` |
| Errore "entitlements … not permitted by provisioning profile" | L'app richiede CarPlay ma il profilo usato è vecchio | Togli i profili vecchi dalla cartella (§4.4) |
| Release Mac non firma | Profilo Mac non rigenerato dopo il cambio di capability | §4.2 per `Radio E45 Mac AppStore` |
| Serve tornare indietro subito | — | Togli la chiave da `Entitlements.plist` e continua con i profili nuovi (§2) |
