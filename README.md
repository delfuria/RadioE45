# RadioE45

Cross-platform .NET MAUI 10 app for AzuraCast webradio streaming.

## Platforms
- iOS 15+
- Android 8.0+ (API 26)
- macOS (Mac Catalyst) 15+
- Windows 10 (build 17763+)

## Features
- Background audio streaming with `CommunityToolkit.Maui.MediaElement`
- Now Playing updated every 10 seconds from the AzuraCast API
- Cover art, artist, and current track title
- LIVE indicator with streamer name
- Listener count
- Player with play/pause/stop and volume control
- Station list with quick selection
- Local persistence with SQLite
- Dark theme by default
- Apple CarPlay support (iOS)
- Android Auto support

## Configured stations
| Name | Stream |
|------|--------|
| Radio Example | https://stream.radioexample.net:8060/live.mp3 |
| Radio Demo FM | https://stream.radioexample.net:8000/live.mp3 |

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

## Tech stack
- **.NET MAUI 10** — cross-platform framework
- **CommunityToolkit.Mvvm** — MVVM with source generators
- **CommunityToolkit.Maui.MediaElement** — audio streaming + background
- **Refit** — typed HTTP client for the AzuraCast API
- **sqlite-net-pcl** — local station persistence
