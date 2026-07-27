# Ambiente di sviluppo — branch `android-auto-media3`

Come compilare ed eseguire questo branch, cosa cambia rispetto a `main` e perché.

---

## 1. In breve: cosa cambia rispetto a `main`

Questo branch ha **un solo target framework: `net10.0-android`**. iOS, MacCatalyst e
Windows sono stati tolti dal `.csproj` di proposito: il lavoro riguarda il livello audio
Android (Media3 / Android Auto / Bluetooth) e tenere un solo TFM permette di fissare in
modo coerente le versioni AndroidX (vedi §5).

Conseguenze pratiche, da sapere **prima** di aprire la soluzione:

| | `main` | `android-auto-media3` |
|---|---|---|
| Target | android, ios, maccatalyst, windows | **solo** android |
| Profilo di avvio "Windows Machine" | disponibile | **non compare più** |
| Codice in `Platforms/{iOS,MacCatalyst,Windows}` | compilato | presente ma **non compilato** |
| Firma iOS / Mac App Store nel `.csproj` | presente | rimossa (va ripristinata per il merge — §6) |

> **Il profilo "Windows Machine" non è stato cancellato**: `Properties/launchSettings.json`
> è intatto. Visual Studio semplicemente non lo propone, perché quel profilo richiede il
> TFM Windows che qui non esiste. Non è un bug e non serve modificare `launchSettings.json`:
> per Android **non esiste** un profilo di avvio, il dispositivo si sceglie dal menu a
> tendina dei device.

---

## 2. Prerequisiti

Versioni con cui il branch è stato sviluppato e verificato:

- **.NET SDK 10.0.302** (qualunque 10.0.1xx+ va bene) — `dotnet --version`
- **Workload `android` 36.1.43** — `dotnet workload list`
  Se manca: `dotnet workload install android` (oppure `maui`, che include anche gli altri).
- **JDK 17** — installato da Visual Studio come *Microsoft Build of OpenJDK*
- **Android SDK Platform 36** + Build-Tools + Emulator (SDK Manager)
  `minSdk 26` (Android 8.0) · `targetSdk 36`
- **Visual Studio 2026 (18.8)** con il carico *.NET Multi-platform App UI development*
  Rider e `dotnet` da riga di comando funzionano ugualmente.

Su Windows basta il workload `android`: non serve installare `ios`/`maccatalyst` per
compilare questo branch.

---

## 3. Primo avvio

```bash
# 1. Segreti — il file è escluso dal repo, va creato dal template
cp RadioE45/AppSecrets.template.cs RadioE45/AppSecrets.cs
#    poi aprirlo e inserire il DSN Sentry reale. Senza questo file NON compila.

# 2. Ripristino pacchetti
dotnet restore RadioE45/RadioE45.csproj

# 3. Build (nessun -f serve: c'è un solo TFM)
dotnet build RadioE45/RadioE45.csproj
```

Il primo `restore` scarica l'intera famiglia AndroidX Media3: mettere in conto qualche
minuto.

---

## 4. Eseguire

### Visual Studio
Avviare un emulatore (o collegare un telefono con debug USB attivo), sceglierlo dal menu
dei device e premere F5. Non c'è nessun profilo da selezionare.

### Riga di comando

```bash
adb devices                                   # verifica che il device sia visto
dotnet build -t:Run RadioE45/RadioE45.csproj  # build + deploy + avvio

# Release / APK firmato
dotnet publish -c Release RadioE45/RadioE45.csproj \
  -p:AndroidSigningKeyStore=key.jks -p:AndroidSigningKeyAlias=<alias> \
  -p:AndroidSigningKeyPass=<pass>  -p:AndroidSigningStorePass=<pass>
```

Log utili durante il test:

```bash
adb logcat -s RadioE45:V MediaSession:V ExoPlayer:V
```

### Test Android Auto
Va fatto con il **DHU (Desktop Head Unit) su un telefono reale**, non con l'AVD
Automotive: l'AVD è Android Automotive OS (sistema operativo dell'auto), mentre qui si
tratta di Android Auto *projection* (telefono che proietta sull'unità di bordo) — sono due
cose diverse e l'app va validata sulla seconda.

```bash
# SDK Manager → SDK Tools → "Android Auto Desktop Head Unit Emulator"
# Sul telefono: app Android Auto → modalità sviluppatore → "Avvia server head unit"
adb forward tcp:5277 tcp:5277
<sdk>/extras/google/auto/desktop-head-unit.exe
```

Nota: lo stream configurato di default punta a un indirizzo LAN; da emulatore può
risultare irraggiungibile. Il servizio prova più URL candidati in fallback, ma per un test
pulito conviene usare l'URL pubblico della stazione.

---

## 5. Pacchetti: cosa è cambiato e perché

Il dettaglio esatto delle versioni è nel `.csproj`, già commentato riga per riga. Qui il
*perché*, che è la parte che non si deduce dal diff.

### Aggiunti — lo stack Media3

| Pacchetto | Ruolo |
|---|---|
| `Xamarin.AndroidX.Media3.Session` | `MediaLibraryService`, `MediaSession`, `MediaController` — la sessione unica che serve Android Auto, Bluetooth e la notifica |
| `Xamarin.AndroidX.Media3.ExoPlayer` | il player vero e proprio, indipendente dalla UI |
| `Xamarin.AndroidX.Media3.ExoPlayer.Hls` | supporto stream HLS |
| `Xamarin.AndroidX.Media3.Common` | `MediaItem` / `MediaMetadata` |

Tenere la famiglia Media3 **allineata sulla stessa minor** (1.10.x) quando si aggiorna.

### Aggiunti — pin che risolvono conflitti, non funzionalità

Questi non aggiungono nulla all'app: servono solo a far quadrare il grafo delle dipendenze.
Non vanno rimossi "perché sembrano inutili".

- **`Xamarin.AndroidX.Core.Core.Ktx` 1.19.0.1** — Media3 tira `AndroidX.Core` alla 1.19,
  dove le classi di `core-ktx` sono state assorbite in `core`. Con un `core-ktx` più
  vecchio il dexing R8 fallisce con *duplicate class* `androidx.core.animation.AnimatorKt`.
  Pinnandolo alla versione corrispondente il pacchetto diventa uno stub vuoto e il
  conflitto sparisce.
- **Famiglia `Lifecycle.*` 2.11.0.1, `Collection.*` 1.6.0.1, `SavedState.*` 1.5.0.1** —
  Media3 alza Lifecycle e Collection, ma lo stack MAUI referenzia ancora i satelliti
  vecchi, i cui intervalli di versione vanno in conflitto: NuGet emette **NU1608** per ogni
  satellite disallineato. Pinnare l'intera famiglia alla stessa versione rende il grafo
  coerente. **Da tenere in lock-step con Media3 a ogni aggiornamento.**

### Sostituiti

- **`SQLitePCLRaw.bundle_green` 2.1.11 → `SQLitePCLRaw.bundle_e_sqlite3` 3.0.4**, con
  `sqlite-net-pcl` 1.9.172 → 1.11.285. Motivo: **sicurezza**. Il vecchio stack 2.1.x
  trascina `lib.e_sqlite3.android` 2.1.11, segnalato da **GHSA-2m69-gcr7-jv3q** (NU1903 in
  build). La linea 3.x non ha il pacchetto RID vulnerabile e chiude l'avviso — utile anche
  in ottica Play Console.
- `Refit` e `Refit.HttpClientFactory` 11.0.1 → 13.1.0
- `Microsoft.Maui.Controls` / `.Core` 10.0.70 → 10.0.80
- `Microsoft.Extensions.Http` / `.Logging.Debug` 10.0.8 → 10.0.9

### Non rimossi (anche se potrebbe sembrare)

- **`CommunityToolkit.Maui.MediaElement` resta referenziato.** Su Android non viene più
  usato per riprodurre — `MauiProgram` registra `Media3AudioService` sotto `#if ANDROID` e
  il `MediaElement` in `OnAirPage` viene staccato nel costruttore, così non nasce una
  seconda `MediaSession`. Ma `Services/Audio/AudioService.cs` (il player di iOS/Windows)
  compila ancora contro quel tipo, quindi il pacchetto serve. **Sarà indispensabile al
  ritorno del multi-piattaforma.**
- `Sentry.Maui` resta, senza più la condizione che lo escludeva su MacCatalyst — quel TFM
  qui non esiste. La condizione va rimessa al merge.

Nessun pacchetto è stato eliminato. Dal `.csproj` sono spariti solo blocchi legati alle
piattaforme non compilate: firma iOS/Mac App Store, i target CarPlay (già commentati),
l'identità pacchetto Microsoft Store, `MtouchLink`, l'alias bundle per Rider su MacCatalyst.

---

## 6. Tornare al multi-piattaforma (per il merge in `main`)

Il branch è pensato per essere valutato e testato così com'è. Per reintegrarlo in `main`
serve rimettere nel `PropertyGroup` iniziale del `.csproj`:

```xml
<TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">net10.0-windows10.0.19041.0</TargetFrameworks>
```

e, insieme, i blocchi condizionali che erano legati a quei target — sono tutti recuperabili
dalla versione su `main`:

- `SupportedOSPlatformVersion` / `TargetPlatformMinVersion` per piattaforma
- `ApplicationId` e `ApplicationVersion` condizionati per Windows Store
- `WindowsPackageType`, `MtouchLink`
- i due `PropertyGroup` di firma iOS / Mac App Store
- il blocco commentato dell'entitlement CarPlay e i due target post-build
- il target `CreateRiderBundleAlias`
- la condizione MacCatalyst su `Sentry.Maui`

I pin AndroidX del §5 vanno invece **mantenuti**: sono già confinati nell'`ItemGroup`
Android e non toccano gli altri target.

---

*Questo documento è disponibile anche in inglese (`DEV-SETUP_EN.md`).*
