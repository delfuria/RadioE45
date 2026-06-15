# Prompt: Implementazione Android Auto e Apple CarPlay

> Prompt da incollare in Claude Code nella root del progetto RadioE45.

---

## Contesto del progetto

Questo è un'app .NET MAUI (net10.0-android / net10.0-ios / net10.0-maccatalyst / net10.0-windows) per lo streaming di una webradio basata su AzuraCast. L'app ID è `com.radioe45.app`.

### Architettura audio rilevante

- **`IAudioService` / `AudioService`** — gestisce la riproduzione via `CommunityToolkit.Maui.MediaElement`. Espone `PlayAsync(AzuraStation)`, `PauseAsync()`, `ResumeAsync()`, `StopAsync()`, `UpdateMetadata(artist, title, artworkUrl, elapsedSeconds, durationSeconds)`.
- **`IPlatformNowPlayingService`** — interfaccia piattaforma per aggiornare lo stato Now Playing nativo:
  - **Android** → `AndroidNowPlayingService` aggiorna `AndroidNowPlayingStateStore` e chiama `AndroidMediaNotificationService.RequestRefresh()`
  - **iOS/macCatalyst** → `IosNowPlayingService` pubblica su `MPNowPlayingInfoCenter.DefaultCenter`
- **`AndroidMediaNotificationService`** — foreground `Service` con `MediaSession` e notifica media (azioni: Pause/Resume/Stop). Ha già `MediaSession.Callback` con `OnPause()`, `OnPlay()`, `OnStop()`.
- **`AndroidLifecycleService`** — foreground `Service` che intercetta `OnTaskRemoved` per stoppare l'audio quando l'app viene rimossa dai recenti.
- **`AzuraStation`** — modello stazione con `Id`, `StationId`, `Name`, `Description`, `LogoUrl`, `StreamUrl`, `HlsUrl`, `OnAirStreamUrl`.
- **`NowPlayingInfo`** — modello traccia con `Artist`, `Title`, `ArtworkUrl`, `TrackElapsedSeconds`, `TrackDurationSeconds`, `IsLive`, `StreamerName`.
- **`IAzuraStationCatalog`** — catalogo stazioni disponibili (già iniettato come singleton).

Tutti i servizi sono registrati come singleton in `MauiProgram.cs` con DI standard .NET.

---

## Obiettivo

Implementa il supporto completo per **Android Auto** (phone projection, non Automotive OS) e **Apple CarPlay** (audio app) in modo che l'utente possa:

1. Vedere la lista delle stazioni radio disponibili nell'interfaccia del veicolo.
2. Selezionare una stazione e avviarne la riproduzione.
3. Vedere titolo, artista e artwork della traccia in riproduzione.
4. Usare i controlli di trasporto (Play/Pause/Stop) dal display del veicolo.

L'implementazione deve integrarsi con l'architettura esistente **senza modificare** `IAudioService`, `IPlatformNowPlayingService` o i ViewModel MAUI esistenti. Usa sempre la DI via `IPlatformApplication.Current.Services` dove non è possibile usare il costruttore.

---

## Android Auto — requisiti dettagliati

### 1. `MediaBrowserServiceCompat` (`Platforms/Android/Services/RadioMediaBrowserService.cs`)

Crea un nuovo `Android.Support.V4.Media.MediaBrowserServiceCompat` (o il suo binding .NET per AndroidX: `AndroidX.Media.MediaBrowserServiceCompat`) che:

- Override di `OnGetRoot`: restituisce una `BrowserRoot` con root ID `"ROOT"` solo se il caller è autorizzato (whitelist per `com.google.android.projection.gearhead` e `com.google.android.mediasimulator`; per tutti gli altri restituisce `null`).
- Override di `OnLoadChildren("ROOT")`: carica le stazioni da `IAzuraStationCatalog` e le restituisce come lista di `MediaBrowserCompat.MediaItem` con `MediaDescriptionCompat` contenente `MediaId = station.Id.ToString()`, `Title = station.Name`, `Subtitle = station.Description`, e se `station.LogoUrl` non è null imposta `IconUri = Android.Net.Uri.Parse(station.LogoUrl)`.
- Override di `OnLoadChildren("<stationId>")`: restituisce lista vuota (le stazioni sono leaf, non cartelle).
- Il servizio deve esporre il token di sessione dell'`AndroidMediaNotificationService` esistente. Poiché `AndroidMediaNotificationService` è già un `Service` Android separato che possiede la `MediaSession`, il `RadioMediaBrowserService` deve ottenere il token via `AndroidNowPlayingStateStore` (aggiungi lì un metodo statico `GetSessionToken()`) oppure tenere una propria `MediaSession` separata solo per il browsing, delegando le azioni a `IAudioService`.

  **Scelta consigliata**: crea una `MediaSession` di proprietà di `RadioMediaBrowserService`, registrala come `SessionToken` del service, e nel suo `MediaSession.Callback`:
  - `OnPlay()` → riprendi l'ultima stazione o la prima disponibile
  - `OnPlayFromMediaId(mediaId, extras)` → ottieni la stazione con quell'ID da `IAzuraStationCatalog`, chiama `IAudioService.PlayAsync(station)`
  - `OnPause()` → `IAudioService.PauseAsync()`
  - `OnStop()` → `IAudioService.StopAsync()`

  Sincronizza lo stato di questa `MediaSession` con gli aggiornamenti da `AndroidNowPlayingService`: aggiungi un evento statico `OnSnapshotChanged` su `AndroidNowPlayingStateStore` e sottoscrivi in `RadioMediaBrowserService` per aggiornare metadata e playback state della propria sessione.

### 2. `AndroidManifest.xml` (`Platforms/Android/AndroidManifest.xml`)

Aggiungi:

```xml
<meta-data
    android:name="com.google.android.gms.car.application"
    android:resource="@xml/automotive_app_desc" />

<service
    android:name="com.radioe45.app.RadioMediaBrowserService"
    android:exported="true">
    <intent-filter>
        <action android:name="android.media.browse.MediaBrowserService" />
    </intent-filter>
</service>
```

### 3. `Resources/xml/automotive_app_desc.xml` (`Platforms/Android/Resources/xml/automotive_app_desc.xml`)

```xml
<?xml version="1.0" encoding="utf-8"?>
<automotiveApp>
    <uses name="media" />
</automotiveApp>
```

### 4. Permessi Android

Verifica che in `AndroidManifest.xml` siano già presenti (o aggiungili):

```xml
<uses-permission android:name="android.permission.FOREGROUND_SERVICE" />
<uses-permission android:name="android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK" />
```

### 5. Aggiornamento `AndroidNowPlayingStateStore`

Aggiungi:
- Un evento statico `public static event Action<PlatformNowPlayingSnapshot>? SnapshotChanged;`
- Invocalo dentro `UpdateSnapshot()` e `Clear()`.
- Un metodo statico `GetSessionToken()` se si opta per la condivisione del token (altrimenti ometti).

---

## Apple CarPlay — requisiti dettagliati

### 1. Entitlement CarPlay (`Platforms/iOS/Entitlements.plist`)

Aggiungi (o crea il file se non esiste):

```xml
<key>com.apple.developer.carplay-audio</key>
<true/>
```

> **Nota**: questo entitlement richiede approvazione Apple tramite il CarPlay Entitlement Request. Per lo sviluppo in locale con simulatore CarPlay il provisioning profile di sviluppo può includere l'entitlement se richiesto ad Apple. Documenta questo requisito in un commento nel file.

### 2. `Info.plist` (`Platforms/iOS/Info.plist`)

Aggiungi la dichiarazione del supporto CarPlay:

```xml
<key>UIBackgroundModes</key>
<array>
    <string>audio</string>
</array>
```

(Se `audio` è già presente in `UIBackgroundModes`, non duplicare.)

### 3. `CarPlaySceneDelegate.cs` (`Platforms/iOS/CarPlaySceneDelegate.cs`)

Crea un `NSObject` che implementa `ICPTemplateApplicationSceneDelegate`:

- `DidConnect(CPTemplateApplicationScene, CPInterfaceController)`: salva il riferimento a `CPInterfaceController`, costruisce e presenta un `CPNowPlayingTemplate.SharedTemplate` come root template. Configura i pulsanti Now Playing se necessario.
- `DidDisconnect(CPTemplateApplicationScene, CPInterfaceController)`: rilascia il riferimento.

Poiché CarPlay per app audio usa principalmente `CPNowPlayingTemplate`, non serve implementare un browse tree completo: CarPlay attinge automaticamente ai dati `MPNowPlayingInfoCenter` già gestiti da `IosNowPlayingService`. Tuttavia, per la selezione della stazione aggiungi un `CPListTemplate` come schermata secondaria:

- Un pulsante nella Now Playing bar con icona "lista" che presenta (via `CPInterfaceController.PushTemplate`) un `CPListTemplate` con la lista delle stazioni (caricata da `IAzuraStationCatalog` via `IPlatformApplication.Current.Services`).
- Al tap su una stazione (`CPListItem.Handler`): chiama `IAudioService.PlayAsync(station)` e fa `PopTemplate`.

### 4. Registrazione della scene in `Info.plist`

```xml
<key>UIApplicationSceneManifest</key>
<dict>
    <key>UIApplicationSupportsMultipleScenes</key>
    <true/>
    <key>UISceneConfigurations</key>
    <dict>
        <key>CPTemplateApplicationSceneSessionRoleApplication</key>
        <array>
            <dict>
                <key>UISceneConfigurationName</key>
                <string>CarPlay Configuration</string>
                <key>UISceneDelegateClassName</key>
                <string>RadioE45.CarPlaySceneDelegate</string>
            </dict>
        </array>
    </dict>
</dict>
```

### 5. `AppDelegate.cs` iOS — nessuna modifica richiesta

La configurazione `AVAudioSession` e `BeginReceivingRemoteControlEvents` esistenti sono già sufficienti per CarPlay.

---

## Vincoli e note implementative

- **Non modificare** `IAudioService`, `IPlatformNowPlayingService`, `AudioService`, `IosNowPlayingService`, `AndroidMediaNotificationService`, né i ViewModel MAUI.
- Tutto il codice Android va in `Platforms/Android/`, tutto il codice iOS in `Platforms/iOS/`. Usa le direttive `#if ANDROID` / `#if IOS` solo in file `.cs` già condivisi (es. `MauiProgram.cs`) se strettamente necessario.
- Per Android, il nuovo `RadioMediaBrowserService` deve essere dichiarato con l'annotation `[Service]` e il nome completo `com.radioe45.app.RadioMediaBrowserService`.
- Per iOS, usa i namespace `CarPlay`, `MediaPlayer`, `Foundation`, `UIKit` già disponibili nel target `net10.0-ios`.
- L'app target Android minimo è API 26 (`SupportedOSPlatformVersion = 26`), quindi non servono guard `OperatingSystem.IsAndroidVersionAtLeast(26)` per i canali di notifica già presenti; usa comunque guard per API ≥ 29 dove necessario (es. `ForegroundServiceType`).
- iOS minimum target: 15.0. `CPNowPlayingTemplate` è disponibile da iOS 14, quindi nessun guard di versione richiesto.
- Gestisci con `try/catch` e log (`ILogger`) i casi in cui i servizi non siano disponibili al momento della connessione veicolo.

---

## Output atteso

Al termine dell'implementazione il progetto deve compilare senza errori per i target `net10.0-android` e `net10.0-ios`. Fornisci un riepilogo dei file creati/modificati e delle eventuali azioni manuali richieste (es. richiesta entitlement CarPlay ad Apple, test con Android Auto Desktop Head Unit).
