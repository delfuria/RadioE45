# RadioE45

Cross-platform .NET MAUI 10 and Uno Platform app for AzuraCast webradio streaming.

## Download

[![Get it from Microsoft Store](https://img.shields.io/badge/Microsoft%20Store-Download-0078D4?logo=microsoft&logoColor=white&style=for-the-badge)](https://apps.microsoft.com/detail/9pj0lkl6mnvq)
[![Download on the App Store](https://img.shields.io/badge/App%20Store-Download-0D96F6?logo=apple&logoColor=white&style=for-the-badge)](https://apps.apple.com/it/app/radio-e45/id6780274998)

## Join the beta

Want to try RadioE45 before the public release? Join the preliminary tests:

[![Android beta](https://img.shields.io/badge/Android-Beta%20testers-3DDC84?logo=android&logoColor=white&style=for-the-badge)](https://groups.google.com/g/radioe45-testers)
[![Linux preview](https://img.shields.io/badge/Linux-Preview%20(.deb)-FCC624?logo=linux&logoColor=black&style=for-the-badge)](https://github.com/delfuria/RadioE45/releases)

- 📱 Android beta: https://groups.google.com/g/radioe45-testers
- 🐧 Linux preview (.deb, x64 and arm64): https://github.com/delfuria/RadioE45/releases

## Platforms
- iOS 15+
- Android 8.0+ (API 26)
- macOS (Mac Catalyst) 15+
- Windows 10 (build 17763+)
- Linux (preview): Debian/Ubuntu-based distributions, `.deb` package for x64 and arm64

## Features
- Background audio streaming with `CommunityToolkit.Maui.MediaElement`
- Now Playing updated every 10 seconds from the AzuraCast API
- Cover art, artist, and current track title
- LIVE indicator with streamer name
- Listener count
- Player with play/pause/stop and volume control
- Station list with quick selection
- Podcast support with saved playback position (resume where you left off) and filters by season, episode, and listening status
- Song requests, when enabled by the station's AzuraCast platform
- Recently played tracks list
- Local persistence with SQLite
- Dark theme by default
- Update check: once a day, on the first launch, the app tells you when a newer version is available (App Store on iOS, Microsoft Store or GitHub Releases on Windows, GitHub Releases for the Linux `.deb`, the site itself on the web), and Settings has a manual check
- Apple CarPlay support (iOS): in progress, not yet enabled (waiting for Apple's CarPlay entitlement)
- Android Auto support

## Android Auto

Android Auto is now implemented. The Android audio layer was rebuilt: playback moved from
`CommunityToolkit.Maui.MediaElement` to **AndroidX Media3 / ExoPlayer** with a single
`MediaLibraryService`, enabling Android Auto, Bluetooth/AVRCP controls, and reliable
background playback. Full details (technical analysis, rebuild report, dev setup, in
Italian and English) are in [`docs/android-auto-media3/`](docs/android-auto-media3/README.md).

## Documentation
All documentation lives in [`docs/`](docs/):

| Topic | Path |
|-------|------|
| Android Auto / Bluetooth → Media3 rebuild (IT + EN) | [`docs/android-auto-media3/`](docs/android-auto-media3/README.md) |
| CarPlay / Android Auto — original implementation | [`docs/carplay/`](docs/carplay/android-auto-carplay-implementation.md) |
| UI localization (`.resx`) | [`docs/LOCALIZATION.md`](docs/LOCALIZATION.md) |
| Store distribution | [`docs/STORE-DISTRIBUTION.md`](docs/STORE-DISTRIBUTION.md) · [`docs/store-descriptions.md`](docs/store-descriptions.md) |
| Version bump procedure | [`docs/VERSION-BUMP.md`](docs/VERSION-BUMP.md) |
| Windows build notes | [`docs/WINDOWS-README.md`](docs/WINDOWS-README.md) |
| App Store review 5.2.3 (third-party content, Terms of Use) | [`docs/APP-REVIEW-5.2.3.md`](docs/APP-REVIEW-5.2.3.md) |
| App Store review 5.1.1 (Local Network purpose string, private hosts) | [`docs/APP-REVIEW-5.1.1.md`](docs/APP-REVIEW-5.1.1.md) |
| Tester distribution | [`docs/tester/distribuzione-tester.md`](docs/tester/distribuzione-tester.md) |

## Preloaded stations
The app ships with no third-party catalog. The only stations it can add on first launch are these two;
every other station is added by the user via public URL.

| Name | Server |
|------|--------|
| RadioE45 (operated by the developer) | radioe45.ddns.net |
| AzuraTest Radio (official AzuraCast demo) | demo.azuracast.com |

On first launch the app also shows Terms of Use once (see [`docs/APP-REVIEW-5.2.3.md`](docs/APP-REVIEW-5.2.3.md)).

## Development

### Secrets configuration

`RadioE45/AppSecrets.cs` contains sensitive data (e.g. the Sentry DSN) and is excluded from the repository via `.gitignore`. Before building, create it from the template:

```bash
cp RadioE45/AppSecrets.template.cs RadioE45/AppSecrets.cs
```

Then open `AppSecrets.cs` and fill in the real values:

```csharp
internal static class AppSecrets
{
    public const string SentryDsn = "https://..."; // your Sentry DSN
}
```

> Without this file the project will not compile. Never commit `AppSecrets.cs`.

```bash
# Restore packages
dotnet restore RadioE45/RadioE45.csproj
```

### macOS (Mac Catalyst)

```bash
# Debug build
dotnet build -f net10.0-maccatalyst RadioE45/RadioE45.csproj

# Run on macOS
dotnet build -t:Run -f net10.0-maccatalyst RadioE45/RadioE45.csproj

# Release — universal binary (x64 + arm64, default in Release config)
dotnet publish -f net10.0-maccatalyst -c Release RadioE45/RadioE45.csproj

# Release — x64 only (Intel Mac), self-contained
dotnet publish -f net10.0-maccatalyst -c Release -r maccatalyst-x64 --self-contained RadioE45/RadioE45.csproj

# Release — arm64 only (Apple Silicon), self-contained
dotnet publish -f net10.0-maccatalyst -c Release -r maccatalyst-arm64 --self-contained RadioE45/RadioE45.csproj
```

Output: `RadioE45/bin/Release/net10.0-maccatalyst/`

> In Release config the default is already a universal binary (x64 + arm64). The Mac App Store requires either both architectures or x64 only — never arm64 alone.  
> With `--self-contained` the .NET 10 runtime is bundled in the `.app`; without it the target machine must have the runtime installed.

### Windows

> Must be built on a Windows machine.

```bash
# Release — x64, self-contained (.exe, no MSIX)
dotnet publish -f net10.0-windows10.0.19041.0 -c Release -r win-x64 --self-contained RadioE45/RadioE45.csproj

# Release — arm64, self-contained (.exe, no MSIX)
dotnet publish -f net10.0-windows10.0.19041.0 -c Release -r win-arm64 --self-contained RadioE45/RadioE45.csproj
```

Output: `RadioE45/bin/Release/net10.0-windows10.0.19041.0/win-x64/publish/`

> `WindowsPackageType=None` is already set in the project, so the output is an unpackaged `.exe` with no MSIX wrapper.  
> Remove `--self-contained` (or pass `--self-contained false`) to produce a smaller, framework-dependent executable that requires .NET 10 installed on the target machine.

### iOS

> Requires macOS with Xcode installed. Physical device deployment requires a valid Apple Developer account.

```bash
# Debug build
dotnet build -f net10.0-ios RadioE45/RadioE45.csproj

# Release — physical device (arm64), signed
dotnet publish -f net10.0-ios -c Release -r ios-arm64 --self-contained RadioE45/RadioE45.csproj \
  -p:CodesignKey="iPhone Distribution: <Your Name>" \
  -p:CodesignProvision="<Provisioning Profile Name>"

# Release — simulator arm64 (Apple Silicon Mac), no signing required
dotnet publish -f net10.0-ios -c Release -r iossimulator-arm64 --self-contained RadioE45/RadioE45.csproj

# Release — simulator x64 (Intel Mac)
dotnet publish -f net10.0-ios -c Release -r iossimulator-x64 --self-contained RadioE45/RadioE45.csproj
```

> The .NET runtime is always embedded in the `.ipa` bundle — `--self-contained` is effectively implicit for iOS.  
> Obtain the distribution certificate and provisioning profile from [Apple Developer](https://developer.apple.com).

### Android

```bash
# Debug build
dotnet build -f net10.0-android RadioE45/RadioE45.csproj

# Release APK — all ABIs (arm64-v8a, armeabi-v7a, x86_64)
dotnet publish -f net10.0-android -c Release RadioE45/RadioE45.csproj

# Release APK — arm64 only (modern devices)
dotnet publish -f net10.0-android -c Release -r android-arm64 RadioE45/RadioE45.csproj

# Release APK — x64 only (emulators, Intel Chromebooks)
dotnet publish -f net10.0-android -c Release -r android-x64 RadioE45/RadioE45.csproj
```

Output: `RadioE45/bin/Release/net10.0-android/publish/`

> The Mono runtime is always bundled inside the APK — `--self-contained` is implicit and has no effect here.  
> To sign the APK for store distribution, add:
> ```
> -p:AndroidSigningKeyStore=key.jks \
> -p:AndroidSigningKeyAlias=<alias> \
> -p:AndroidSigningKeyPass=<pass> \
> -p:AndroidSigningStorePass=<pass>
> ```

### Linux and Web (Uno Platform)

> The Linux desktop app and the web app are built with **Uno Platform** from the `RadioE45.Uno` project, which shares the logic in `RadioE45.Core`. Build them on Linux (Ubuntu/Debian recommended) or on macOS for the desktop and web targets.  
> `AppSecrets.cs` is not needed: the Uno projects do not use it.

Requirements (Ubuntu/Debian):

```bash
# System packages (libVLC for playback, X11 and fontconfig for the desktop renderer)
sudo apt update && sudo apt install -y git curl \
  vlc libvlc-dev libvlc5 libx11-dev libfontconfig1 libice6 libsm6

# .NET 10 SDK (or: dotnet-install.sh --channel 10.0)
sudo apt install -y dotnet-sdk-10.0

# Uno Platform checks and WebAssembly workload (web target)
dotnet tool install -g uno.check && uno-check --target linux --non-interactive
dotnet workload install wasm-tools
```

Packaging a `.deb` also needs `dpkg-deb` (included in `dpkg` on Linux; on macOS: `brew install dpkg`).

```bash
# Restore packages
dotnet restore RadioE45.Uno/RadioE45.Uno.csproj

# Desktop debug build and run
dotnet build RadioE45.Uno -f net10.0-desktop
dotnet run --project RadioE45.Uno -f net10.0-desktop

# Web debug build and run (http://localhost:5045)
dotnet build RadioE45.Uno -f net10.0-browserwasm
dotnet run --project RadioE45.Uno -f net10.0-browserwasm

# Release .deb package (x64 or arm64)
packaging/linux/build-deb.sh linux-x64 <output-dir>
packaging/linux/build-deb.sh linux-arm64 <output-dir>
```

Output: `<output-dir>/radioe45_<version>_<amd64|arm64>.deb`. The `.deb` is self-contained for .NET; libVLC and the other libraries come from the system and are declared as package dependencies.

> The Windows libVLC binaries (pulled in by the Uno media player package) are excluded from the Linux publish, so the `.deb` stays around 40–45 MB.  
> The Linux package is a preview; the MAUI app remains the main app on macOS and Windows.

## Tech stack
- **Uno Platform** — Linux desktop and web app (shares `RadioE45.Core` with the MAUI app)
- **.NET MAUI 10** — cross-platform framework
- **CommunityToolkit.Mvvm** — MVVM with source generators
- **CommunityToolkit.Maui.MediaElement** — audio streaming + background
- **Refit** — typed HTTP client for the AzuraCast API
- **sqlite-net-pcl** — local station persistence
- **Uno Platform** — Linux desktop and web app (shares `RadioE45.Core` with the MAUI app)
