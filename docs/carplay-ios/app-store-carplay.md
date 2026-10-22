# CarPlay — App Store, TestFlight e App Review

> Scritto il 22 ottobre 2026 (Fase G). Testi per lo store in inglese, come da regola del progetto.
> Caricamenti, tag e release li esegue l'utente: qui ci sono solo testi e comandi da lanciare a mano. Procedura generale: [`../RELEASE.md`](../RELEASE.md) e [`../STORE-DISTRIBUTION.md`](../STORE-DISTRIBUTION.md).

## Indice

- [1. Controlli prima dell'invio](#1-controlli-prima-dellinvio)
- [2. TestFlight (prova C)](#2-testflight-prova-c)
- [3. What's New (note di versione)](#3-whats-new-note-di-versione)
- [4. Descrizione App Store](#4-descrizione-app-store)
- [5. App Review Information → Notes](#5-app-review-information--notes)
- [6. Se App Review fa domande su CarPlay](#6-se-app-review-fa-domande-su-carplay)

---

## 1. Controlli prima dell'invio

| # | Controllo | Come | Stato |
|---|---|---|---|
| 1 | Capability CarPlay Audio sull'App ID, profili rigenerati | [`provisioning-profiles.md`](provisioning-profiles.md) §4.4 | fatto il 22 ottobre |
| 2 | `carplay-audio` in `Platforms/iOS/Entitlements.plist` | commit `9432d63` | fatto |
| 3 | Versione e changelog | skill `/release-prep` (sposta `[Unreleased]` nella versione, controlli, build) | da fare |
| 4 | L'IPA firmata App Store contiene l'entitlement | comando del §2, punto 3 | da fare |
| 5 | Prove su TestFlight (parte C di [`device-test-plan.md`](device-test-plan.md)) | §2 | da fare |
| 6 | Prove B6, B7, B10, B11 | [`device-test-plan.md`](device-test-plan.md) | da fare |
| 7 | iPad: una sola finestra | [`device-test-plan.md`](device-test-plan.md) A12 | da verificare se c'è un iPad |
| 8 | Video dimostrativo (facoltativo, utile se App Review chiede) | registrazione della finestra di CarPlay Simulator con l'audio: lista, tocco, Now Playing, successiva | facoltativo |

## 2. TestFlight (prova C)

1. Build App Store. `build-store-ios.sh`, citato in `STORE-DISTRIBUTION.md`, non esiste più (cartella `scripts/` cancellata a giugno 2026). Comando dalla documentazione Microsoft ([Publish an iOS app using the command line](https://learn.microsoft.com/dotnet/maui/ios/deployment/publish-cli?view=net-maui-10.0)); chiave, profilo `Radio E45 AppStore` ed entitlement vengono dal csproj (configurazione Release):

   ```bash
   dotnet publish RadioE45/RadioE45.csproj -f net10.0-ios -c Release -p:ArchiveOnBuild=true -p:RuntimeIdentifier=ios-arm64
   ```

   L'IPA finisce in `RadioE45/bin/Release/net10.0-ios/ios-arm64/publish/`. Non l'ho eseguito: da provare alla prima release.
2. Caricamento con Transporter o `xcrun altool` (sezione "Upload su App Store Connect" di [`../STORE-DISTRIBUTION.md`](../STORE-DISTRIBUTION.md)).
3. Prima di caricare, verificare che l'IPA contenga l'entitlement CarPlay:

   ```bash
   T=$(mktemp -d)
   unzip -q RadioE45/bin/Release/net10.0-ios/ios-arm64/publish/*.ipa -d "$T"
   codesign -d --entitlements - "$T"/Payload/*.app | grep carplay-audio
   security cms -D -i "$T"/Payload/*.app/embedded.mobileprovision | plutil -extract Name raw -
   ```

   Atteso: la riga con `com.apple.developer.carplay-audio` e il profilo `Radio E45 AppStore`. Se manca, non caricare: il profilo App Store installato non è quello rigenerato.
4. Installare da TestFlight sull'iPhone e ripetere almeno A1, A6, B1, B3, B5, B8 (con CarPlay Simulator).

## 3. What's New (note di versione)

Dalla sezione `[Unreleased]` di `CHANGELOG.md`. Da riallineare se il changelog cambia prima della release.

```
• Apple CarPlay: choose a station on the car screen and see the track, artist and cover art in Now Playing. The car's buttons pause, resume and change station.
• Next and previous on the lock screen, in Control Center, in CarPlay and on headphones now switch station.
• MP3 stations start in about a second on CarPlay.
• Unplugging headphones or disconnecting from CarPlay pauses the radio instead of restarting it on the phone speaker.
```

## 4. Descrizione App Store

Paragrafo da aggiungere alla descrizione esistente in App Store Connect (in inglese; per italiano e polacco, se la scheda è localizzata, tradurre allo stesso modo):

```
Apple CarPlay: listen in the car with the station list on the CarPlay screen and Now Playing with track, artist and cover art. Use the car's controls to pause, resume and switch station.
```

Parola chiave da valutare nel campo Keywords: `carplay`. Il campo ha un limite di 100 caratteri: controllare quante ne restano.

Gli screenshot dello store sono solo per iPhone e iPad: non ci sono screenshot CarPlay da caricare.

## 5. App Review Information → Notes

Testo completo da incollare: le note già usate per la guideline 5.2.3 ([`../APP-REVIEW-5.2.3.md`](../APP-REVIEW-5.2.3.md)) più la parte CarPlay.

```
The app contains no third-party content or catalog. It ships only with the Radio E45
station (operated by the developer) and the official AzuraCast demo station. Any other
station is added by the user via public URL, like adding a podcast feed by URL in a
podcast player; the app does not host, store or rebroadcast audio.

On first launch the app shows Terms of Use (also available in Settings > Info) that make the user responsible for the streams
added and give rights holders a contact for reports and removal requests:
support@radioe45.it

CarPlay (CarPlay Audio App entitlement)
Radio E45 is a live internet radio app. In CarPlay it uses only the list and now playing templates.
To test:
1. Install the app and launch it once on the iPhone: accept the Terms of Use and the prompt to add the sample stations (Radio E45 and AzuraTest Radio).
2. Connect the iPhone to a CarPlay system or to CarPlay Simulator (Additional Tools for Xcode).
3. Open Radio E45 on the CarPlay home screen. The "Stations" list appears.
4. Choose a station: live audio starts and the Now Playing screen shows the track, artist, cover art and elapsed time.
5. Play/pause and next/previous (switch station) work from the Now Playing screen, the car controls and the iPhone lock screen.
No account or login is required. Playback is live streaming, so seeking is disabled.
```

## 6. Se App Review fa domande su CarPlay

- **"The app is not primarily an audio app":** l'app è una radio dal vivo; CarPlay mostra solo stazioni e Now Playing; nessuna funzione non audio in CarPlay.
- **"We could not test CarPlay":** allegare il video del §1 punto 8 e ripetere i passi del §5; specificare che serve almeno una stazione (le due di esempio si aggiungono al primo avvio).
- **"Playback does not start automatically":** è voluto. Si sceglie la stazione dalla lista, come nell'esempio della CarPlay Developer Guide; alla riconnessione dell'auto l'app esegue il comando Play inviato dal sistema.
- **Errori di rete in auto:** oggi l'app riprova da sola, ma non mostra un avviso in CarPlay (Fase E del piano). Se App Review lo chiede, aggiungere un `CPAlertTemplate` breve senza istruzioni che invitino a usare l'iPhone (regola della guida CarPlay: "Never instruct people to pick up their iPhone").
