# Android Auto e Apple CarPlay — Implementazione RadioE45

> Data: 12 giugno 2026  
> Branch: `Auto-Support`  
> Target: `net10.0-android`, `net10.0-ios`

---

## Obiettivo

Aggiungere supporto **Android Auto** (phone projection) e **Apple CarPlay** (audio app) all'app RadioE45, permettendo all'utente di:

- Sfogliare la lista delle stazioni radio dall'interfaccia del veicolo
- Avviare la riproduzione selezionando una stazione
- Vedere titolo, artista e artwork della traccia in corso
- Controllare la riproduzione (Play / Pause / Stop) dal display del veicolo

L'implementazione si integra con l'architettura esistente **senza modificare** `IAudioService`, `IPlatformNowPlayingService` o i ViewModel MAUI.

---

## File creati e modificati

### Android

#### `Platforms/Android/Services/AndroidNowPlayingStateStore.cs` — Modificato

Aggiunto un evento statico per notificare i sottoscrittori (tra cui il nuovo `RadioMediaBrowserService`) quando lo snapshot del "now playing" cambia:

```csharp
public static event Action<PlatformNowPlayingSnapshot>? SnapshotChanged;
```

L'evento viene invocato **fuori dal lock** (best practice per evitare deadlock) alla fine di `UpdateSnapshot()` e `Clear()`.

---

#### `Platforms/Android/Services/RadioMediaBrowserService.cs` — Creato

Implementa `Android.Service.Media.MediaBrowserService` (API 26+, framework nativo, nessun NuGet aggiuntivo richiesto).

Responsabilità:

| Metodo/componente | Comportamento |
|---|---|
| `OnGetRoot` | Restituisce `BrowserRoot("ROOT")` solo ai caller autorizzati (`com.google.android.projection.gearhead`, `com.google.android.mediasimulator`); per tutti gli altri restituisce `null` |
| `OnLoadChildren("ROOT")` | Chiama `result.Detach()` per risposta asincrona, carica le stazioni da `IAzuraStationCatalog` (invoca `LoadAsync()` se la lista è vuota), restituisce `MediaBrowser.MediaItem[]` con `FlagPlayable` |
| `OnLoadChildren(altro)` | Restituisce lista vuota (le stazioni sono leaf, non cartelle) |
| `MediaSession` propria | Creata in `OnCreate`, usata come `SessionToken` del servizio; sincronizzata con `AndroidNowPlayingStateStore.SnapshotChanged` per mantenere metadata e playback state aggiornati |
| `AutoMediaCallback` | `OnPlay` → `ResumeAsync()`, `OnPlayFromMediaId` → `PlayAsync(station)`, `OnPause` → `PauseAsync()`, `OnStop` → `StopAsync()` |

Tutti i servizi (`IAudioService`, `IAzuraStationCatalog`) sono ottenuti via `IPlatformApplication.Current.Services` perché il service Android viene istanziato direttamente dal sistema operativo, fuori dal ciclo DI di MAUI.

---

#### `Platforms/Android/AndroidManifest.xml` — Modificato

Aggiunto dentro `<application>`:

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

Il flag `android:exported="true"` è obbligatorio: Android Auto deve potersi connettere al servizio dall'esterno dell'app.

I permessi `FOREGROUND_SERVICE` e `FOREGROUND_SERVICE_MEDIA_PLAYBACK` erano già presenti.

---

#### `Platforms/Android/Resources/xml/automotive_app_desc.xml` — Creato

Risorsa richiesta dal Play Store e da Android Auto per dichiarare che l'app è di tipo media:

```xml
<?xml version="1.0" encoding="utf-8"?>
<automotiveApp>
    <uses name="media" />
</automotiveApp>
```

---

### iOS

#### `Platforms/iOS/Entitlements.plist` — Creato

```xml
<key>com.apple.developer.carplay-audio</key>
<true/>
```

> ⚠️ **Azione manuale richiesta**: questo entitlement richiede approvazione esplicita da parte di Apple prima di poter essere incluso in un provisioning profile. Vedi la sezione [Azioni manuali](#azioni-manuali-richieste) più avanti.

---

#### `Platforms/iOS/Info.plist` — Modificato

Aggiunto `UIApplicationSceneManifest` per registrare la scene delegate di CarPlay:

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
                <string>CarPlaySceneDelegate</string>
            </dict>
        </array>
    </dict>
</dict>
```

Il valore `CarPlaySceneDelegate` corrisponde al nome Objective-C registrato con `[Register("CarPlaySceneDelegate")]` nella classe C#.

---

#### `Platforms/iOS/CarPlaySceneDelegate.cs` — Creato

Implementa `ICPTemplateApplicationSceneDelegate` e gestisce l'intera UI CarPlay.

| Metodo | Comportamento |
|---|---|
| `DidConnect` | Crea `CPNowPlayingTemplate.SharedTemplate` come root; aggiunge un pulsante `music.note.list` nella `trailingNavigationBarButtons` tramite `ICPBarButtonProviding` (protocollo Obj-C che `CPNowPlayingTemplate` implementa); presenta la template via `SetRootTemplate` |
| `DidDisconnect` | Rilascia il riferimento a `CPInterfaceController` |
| `OnListButtonTapped` | Carica le stazioni da `IAzuraStationCatalog`, costruisce un `CPListTemplate` con una `CPListSection`, fa push via `PushTemplate` |
| `BuildStationItem` | Crea un `CPListItem` con nome e descrizione; il `Handler` chiama `IAudioService.PlayAsync(station)` e fa `PopTemplate` dopo aver avviato la riproduzione |

`CPNowPlayingTemplate` mostra automaticamente i dati di `MPNowPlayingInfoCenter` già gestiti da `IosNowPlayingService`: titolo, artista, artwork e controlli di trasporto sono disponibili senza codice aggiuntivo.

**Nota implementativa**: la proprietà `TrailingNavigationBarButtons` nel binding .NET iOS 10 non è esposta direttamente su `CPNowPlayingTemplate` o `CPTemplate`, ma è definita sull'interfaccia `ICPBarButtonProviding` (binding del protocollo Obj-C `CPBarButtonProviding`). Il codice usa pattern matching per accedervi in modo sicuro.

---

## Architettura del flusso dati

### Android Auto

```
Android Auto (DHU / veicolo)
    │
    ▼ MediaBrowserService connection
RadioMediaBrowserService
    │── OnGetRoot() ──────────────────────────────► whitelist check
    │── OnLoadChildren("ROOT") ───────────────────► IAzuraStationCatalog.Stations
    │── AutoMediaCallback.OnPlayFromMediaId() ────► IAudioService.PlayAsync(station)
    │── AutoMediaCallback.OnPause/Stop() ─────────► IAudioService.PauseAsync/StopAsync
    │
    │   [AndroidNowPlayingStateStore.SnapshotChanged]
    │◄──────────────────────────────────────────── AndroidNowPlayingService (invariato)
    │
    ▼ MediaSession.SetMetadata / SetPlaybackState
Android Auto UI (metadata + transport controls aggiornati)
```

### Apple CarPlay

```
CarPlay (simulatore / veicolo)
    │
    ▼ CPTemplateApplicationScene connect
CarPlaySceneDelegate
    │── DidConnect() ─────────────────────────────► CPNowPlayingTemplate (root)
    │   [pulsante lista]
    │── OnListButtonTapped() ─────────────────────► CPListTemplate (push)
    │── CPListItem.Handler ────────────────────────► IAudioService.PlayAsync(station)
    │                                                + PopTemplate
    │
    │   [MPNowPlayingInfoCenter — automatico]
    │◄──────────────────────────────────────────── IosNowPlayingService (invariato)
    │
    ▼ CPNowPlayingTemplate
CarPlay UI (metadata + transport controls da MPNowPlayingInfoCenter)
```

---

## Azioni manuali richieste

### 1. Richiedere l'entitlement CarPlay ad Apple (iOS)

Il profilo di provisioning di sviluppo **non include** `com.apple.developer.carplay-audio` per default. Senza approvazione:

- La build funziona normalmente su simulatore iOS
- Il **CarPlay Simulator** in Xcode (`I/O → External Displays → CarPlay`) funziona senza provisioning speciale in sviluppo
- Su **dispositivo fisico** o build **TestFlight/App Store**, la funzionalità CarPlay non è disponibile fino all'approvazione

**Come richiedere:**
1. Vai su https://developer.apple.com/contact/carplay/
2. Compila il form "CarPlay Entitlement Request" scegliendo la categoria **Audio**
3. Una volta approvato, Apple aggiornerà il tuo Apple Developer account
4. Rigenera il provisioning profile di sviluppo e distribuzione includendo l'entitlement

---

### 2. Testare Android Auto con DHU (Desktop Head Unit)

Il modo più rapido per testare su desktop:

```bash
# 1. Installa DHU via Android SDK Manager
sdkmanager "extras;google;auto"

# 2. Abilita "Android Auto" nelle impostazioni del telefono (modalità sviluppatore)
# 3. Connetti il telefono via USB e avvia DHU
cd $ANDROID_HOME/extras/google/auto
./desktop-head-unit

# 4. In alternativa, usa l'emulatore Android con AVD supportato
# (API 28+ con Play Services)
```

Verifica che:
- L'app appaia nell'elenco media di Android Auto
- La lista stazioni si carichi correttamente
- Play/Pause/Stop dal DHU aggiornino lo stato della notifica sul telefono

---

### 3. Testare CarPlay con il simulatore Xcode

1. Avvia l'app sul simulatore iOS (iPhone 15+)
2. In Xcode: `I/O → External Displays → CarPlay`
3. Si apre una seconda finestra che simula il display del veicolo
4. Verifica che la `CPNowPlayingTemplate` appaia come root
5. Avvia la riproduzione dall'app principale e verifica che metadata e controlli si aggiornino
6. Testa il pulsante lista stazioni e la selezione

---

### 4. Verifica lifecycle multi-scene iOS

L'aggiunta di `UIApplicationSupportsMultipleScenes = true` in `Info.plist` attiva il lifecycle basato su scene in iOS. MAUI 10 supporta questo pattern, ma è consigliabile verificare su dispositivo fisico che:

- La finestra principale dell'app si avvii correttamente
- La navigazione tra schermate funzioni normalmente
- Non ci siano regressioni nella gestione del background audio

Se si verificano problemi con la finestra principale, consultare la documentazione MAUI su [scene-based lifecycle](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/app-lifecycle).

---

## Note tecniche

### Perché due MediaSession su Android?

`AndroidMediaNotificationService` (preesistente) ha la propria `MediaSession` per la notifica media sul telefono. `RadioMediaBrowserService` ha una seconda `MediaSession` per esporre il `SessionToken` richiesto da `MediaBrowserService`.

Entrambe controllano lo stesso `IAudioService` sottostante, quindi lo stato di riproduzione è sempre coerente. La separazione è intenzionale: Android Auto usa il token del browser service, la notifica sistema usa quello della notification service.

### Perché `result.Detach()` in `OnLoadChildren`?

`MediaBrowserService.OnLoadChildren` deve rispondere sincronicamente di default. Chiamare `result.Detach()` prima dell'operazione asincrona permette di rispondere in un secondo momento senza bloccare il thread principale. Questo è necessario perché `IAzuraStationCatalog.LoadAsync()` può fare I/O su disco (SQLite) al primo avvio.

### Perché `ICPBarButtonProviding` invece di `CPTemplate`?

Nel binding .NET iOS 10, la proprietà `trailingNavigationBarButtons` è esposta sull'interfaccia `ICPBarButtonProviding` (binding del protocollo Obj-C), non direttamente come membro ereditato di `CPTemplate` o `CPNowPlayingTemplate`. Questo è un dettaglio del generatore di binding; a runtime il comportamento è identico a quanto documentato da Apple.

### Permessi Android già presenti

I permessi `android.permission.FOREGROUND_SERVICE` e `android.permission.FOREGROUND_SERVICE_MEDIA_PLAYBACK` erano già dichiarati nel manifest prima di questa implementazione. Nessun permesso aggiuntivo è necessario per Android Auto.
