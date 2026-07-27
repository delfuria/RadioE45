# Development environment — branch `android-auto-media3`

How to build and run this branch, what differs from `main`, and why.

---

## 1. In short: what differs from `main`

This branch has **a single target framework: `net10.0-android`**. iOS, MacCatalyst and
Windows were removed from the `.csproj` on purpose: the work concerns the Android audio
layer (Media3 / Android Auto / Bluetooth), and a single TFM is what makes it possible to
pin the AndroidX versions coherently (see §5).

Practical consequences, worth knowing **before** opening the solution:

| | `main` | `android-auto-media3` |
|---|---|---|
| Targets | android, ios, maccatalyst, windows | **android only** |
| "Windows Machine" launch profile | available | **no longer listed** |
| Code under `Platforms/{iOS,MacCatalyst,Windows}` | compiled | present but **not compiled** |
| iOS / Mac App Store signing in `.csproj` | present | removed (restore it for the merge — §6) |

> **The "Windows Machine" profile was not deleted**: `Properties/launchSettings.json` is
> untouched. Visual Studio simply doesn't offer it, because that profile requires the
> Windows TFM, which doesn't exist here. This is not a bug, and `launchSettings.json` does
> not need editing: Android has **no** launch profile — the target is picked from the
> device dropdown.

---

## 2. Prerequisites

Versions this branch was developed and verified against:

- **.NET SDK 10.0.302** (any 10.0.1xx+ works) — `dotnet --version`
- **Workload `android` 36.1.43** — `dotnet workload list`
  If missing: `dotnet workload install android` (or `maui`, which includes the others).
- **JDK 17** — installed by Visual Studio as the *Microsoft Build of OpenJDK*
- **Android SDK Platform 36** + Build-Tools + Emulator (SDK Manager)
  `minSdk 26` (Android 8.0) · `targetSdk 36`
- **Visual Studio 2026 (18.8)** with the *.NET Multi-platform App UI development* workload
  Rider and the plain `dotnet` CLI work just as well.

On Windows the `android` workload alone is enough: `ios`/`maccatalyst` are not needed to
build this branch.

---

## 3. First run

```bash
# 1. Secrets — the file is excluded from the repo, create it from the template
cp RadioE45/AppSecrets.template.cs RadioE45/AppSecrets.cs
#    then open it and fill in the real Sentry DSN. Without this file it will NOT compile.

# 2. Restore packages
dotnet restore RadioE45/RadioE45.csproj

# 3. Build (no -f needed: there is only one TFM)
dotnet build RadioE45/RadioE45.csproj
```

The first `restore` pulls the whole AndroidX Media3 family — expect a few minutes.

---

## 4. Running

### Visual Studio
Start an emulator (or plug in a phone with USB debugging on), pick it from the device
dropdown and hit F5. There is no profile to select.

### Command line

```bash
adb devices                                   # check the device is visible
dotnet build -t:Run RadioE45/RadioE45.csproj  # build + deploy + launch

# Release / signed APK
dotnet publish -c Release RadioE45/RadioE45.csproj \
  -p:AndroidSigningKeyStore=key.jks -p:AndroidSigningKeyAlias=<alias> \
  -p:AndroidSigningKeyPass=<pass>  -p:AndroidSigningStorePass=<pass>
```

Useful logs while testing:

```bash
adb logcat -s RadioE45:V MediaSession:V ExoPlayer:V
```

### Testing Android Auto
Do it with the **DHU (Desktop Head Unit) against a real phone**, not the Automotive AVD:
that AVD is Android Automotive OS (the car's own operating system), whereas this is Android
Auto *projection* (a phone projecting onto the head unit). They are different things, and
the app has to be validated against the latter.

```bash
# SDK Manager → SDK Tools → "Android Auto Desktop Head Unit Emulator"
# On the phone: Android Auto app → developer mode → "Start head unit server"
adb forward tcp:5277 tcp:5277
<sdk>/extras/google/auto/desktop-head-unit.exe
```

Note: the default configured stream points at a LAN address, which may be unreachable from
an emulator. The service tries several candidate URLs as a fallback, but for a clean test
use the station's public URL.

---

## 5. Packages: what changed and why

Exact versions live in the `.csproj`, which is commented line by line. What follows is the
*why* — the part the diff doesn't tell you.

### Added — the Media3 stack

| Package | Role |
|---|---|
| `Xamarin.AndroidX.Media3.Session` | `MediaLibraryService`, `MediaSession`, `MediaController` — the single session serving Android Auto, Bluetooth and the notification |
| `Xamarin.AndroidX.Media3.ExoPlayer` | the player itself, independent of the UI |
| `Xamarin.AndroidX.Media3.ExoPlayer.Hls` | HLS stream support |
| `Xamarin.AndroidX.Media3.Common` | `MediaItem` / `MediaMetadata` |

Keep the Media3 family **on the same minor** (1.10.x) when upgrading.

### Added — pins that fix conflicts, not features

These add nothing to the app: they exist purely to make the dependency graph resolve. Do
not drop them because they "look unused".

- **`Xamarin.AndroidX.Core.Core.Ktx` 1.19.0.1** — Media3 pulls `AndroidX.Core` up to 1.19,
  where the `core-ktx` classes were merged into `core`. With an older `core-ktx`, R8 dexing
  fails with a *duplicate class* `androidx.core.animation.AnimatorKt`. Pinning it to the
  matching version turns the package into an empty stub and the conflict disappears.
- **`Lifecycle.*` 2.11.0.1, `Collection.*` 1.6.0.1, `SavedState.*` 1.5.0.1 families** —
  Media3 raises Lifecycle and Collection, but the MAUI stack still references the older
  satellites, whose version ranges then clash: NuGet emits **NU1608** for every mismatched
  satellite. Pinning the entire family to one version makes the graph consistent. **Keep
  these in lock-step with Media3 on every upgrade.**

### Replaced

- **`SQLitePCLRaw.bundle_green` 2.1.11 → `SQLitePCLRaw.bundle_e_sqlite3` 3.0.4**, with
  `sqlite-net-pcl` 1.9.172 → 1.11.285. Reason: **security**. The old 2.1.x stack drags in
  `lib.e_sqlite3.android` 2.1.11, flagged by **GHSA-2m69-gcr7-jv3q** (NU1903 at build
  time). The 3.x line has no vulnerable RID package and clears the advisory — which also
  helps on the Play Console side.
- `Refit` and `Refit.HttpClientFactory` 11.0.1 → 13.1.0
- `Microsoft.Maui.Controls` / `.Core` 10.0.70 → 10.0.80
- `Microsoft.Extensions.Http` / `.Logging.Debug` 10.0.8 → 10.0.9

### Not removed (even though it may look that way)

- **`CommunityToolkit.Maui.MediaElement` is still referenced.** On Android it no longer
  plays anything — `MauiProgram` registers `Media3AudioService` under `#if ANDROID`, and
  the `MediaElement` on `OnAirPage` is detached in the constructor so no second
  `MediaSession` is ever created. But `Services/Audio/AudioService.cs` (the iOS/Windows
  player) still compiles against that type, so the package is required. **It will be
  essential once the other platforms come back.**
- `Sentry.Maui` stays, minus the condition that excluded it on MacCatalyst — that TFM
  doesn't exist here. The condition must go back on merge.

No package was dropped. What disappeared from the `.csproj` is only the blocks tied to the
platforms that are no longer compiled: iOS / Mac App Store signing, the (already commented
out) CarPlay targets, the Microsoft Store package identity, `MtouchLink`, and the Rider
bundle alias for MacCatalyst.

---

## 6. Going back to multi-platform (for the merge into `main`)

The branch is meant to be reviewed and tested as it stands. To fold it back into `main`,
restore in the leading `PropertyGroup` of the `.csproj`:

```xml
<TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">net10.0-windows10.0.19041.0</TargetFrameworks>
```

together with the conditional blocks tied to those targets — all of them recoverable from
the version on `main`:

- per-platform `SupportedOSPlatformVersion` / `TargetPlatformMinVersion`
- Windows-Store-conditioned `ApplicationId` and `ApplicationVersion`
- `WindowsPackageType`, `MtouchLink`
- the two iOS / Mac App Store signing `PropertyGroup`s
- the commented-out CarPlay entitlement block and its two post-build targets
- the `CreateRiderBundleAlias` target
- the MacCatalyst condition on `Sentry.Maui`

The AndroidX pins from §5, on the other hand, should be **kept**: they already live inside
the Android `ItemGroup` and don't affect the other targets.

---

*This document is also available in Italian (`DEV-SETUP_IT.md`).*
