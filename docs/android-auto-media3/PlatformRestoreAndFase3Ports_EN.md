# RadioE45 — Multi-Platform Restore & Selected Ports from `Android-Auto-Fase3`

**Audience:** author of the RadioE45 app
**Subject:** restore iOS / MacCatalyst / Windows build targets on `android-auto-media3`, and port a handful of fixes/features from the parallel `Android-Auto-Fase3` branch
**Branch:** `android-stop-on-app-close` (built on top of `android-auto-media3`)
**Compared against:** `main` (multi-platform baseline) and `origin/Android-Auto-Fase3` (parallel Media3/Android Auto rebuild)
**Platform:** .NET MAUI 10, `net10.0-android;net10.0-ios;net10.0-maccatalyst` (+ `net10.0-windows10.0.19041.0` on Windows hosts)
**Date:** 2026-07-29

> This document describes a single work session, not a full rebuild: the Android Media3 layer itself is unchanged (see `RefactorReport_RadioE45_EN.md`). What changed here is (1) the project file, which had been temporarily reduced to `net10.0-android` only so the Media3 rebuild could be developed and tested in isolation, and (2) three features/fixes that exist on the sibling `Android-Auto-Fase3` branch but were missing here.

---

## 1. Why this session happened

Two branches independently rebuilt Android playback around AndroidX Media3:

- `android-auto-media3` → `android-stop-on-app-close` (this branch): a from-scratch rewrite, one `MediaLibraryService` (`RadioPlaybackService`) as the single source of truth for ExoPlayer, the browse tree, Android Auto, Bluetooth and the notification. Built Android-only on purpose (see `DEV-SETUP_EN.md` §1) to keep the dependency graph simple while the rewrite was being validated.
- `Android-Auto-Fase3`: an incremental layer added on top of the pre-existing legacy session stack (`RadioMediaBrowserService`, `AndroidMediaNotificationService`, `AudioLifecycleService`), never fully cleaned up (its own "Fase 3.9" consolidation step was never done). It kept the full `main` multi-platform `.csproj` throughout, and along the way picked up a few fixes and features this branch never had.

A comparison of the two (session context, not reproduced here) found four things worth carrying over into this branch: the multi-platform targets themselves, a MacCatalyst startup crash fix, two `AudioService` hardening fixes, and two Android Auto features (proactive stream-URL probing, voice search). All four are described below.

---

## 2. Multi-platform restore

### 2.1 What changed

`RadioE45.csproj` had been trimmed to a single TFM:

```xml
<TargetFrameworks>net10.0-android</TargetFrameworks>
```

Restored to:

```xml
<TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">net10.0-windows10.0.19041.0</TargetFrameworks>
```

Everything that came back with it, following the recipe already documented in `DEV-SETUP_EN.md` §6:

| Block | Restored from `main` |
|---|---|
| `SupportedOSPlatformVersion` / `TargetPlatformMinVersion` | per-platform, split by `GetTargetPlatformIdentifier` |
| `ApplicationId` / `ApplicationVersion` | Windows Store–conditioned variants |
| `WindowsPackageType`, `MtouchLink` | Windows / iOS build properties |
| iOS App Store signing `PropertyGroup` | `CodesignKey` / `CodesignProvision` / `CodesignEntitlements` |
| Mac App Store signing `PropertyGroup` | same, for MacCatalyst |
| CarPlay entitlement + post-build targets | restored **commented out** (no CarPlay certificate yet) |
| `CreateRiderBundleAlias` target | MacCatalyst-only Rider workaround |

Two things were **not** restored mechanically from `main`, by design:

- **Package versions** — kept at this branch's upgraded values (`Microsoft.Maui.Controls`/`.Core` 10.0.80, `Refit` 13.1.0, `sqlite-net-pcl` 1.11.285 / `SQLitePCLRaw.bundle_e_sqlite3` 3.0.4), not reverted to `main`'s older ones. The SQLite change in particular is security-motivated (clears `GHSA-2m69-gcr7-jv3q`) and applies to every platform.
- **AndroidX/Media3 pins** — moved under `<ItemGroup Condition="...android">` instead of being restored globally, since they don't exist in `main` at all and must not be evaluated for the other TFMs (NuGet would otherwise try to resolve Android-only packages for iOS/MacCatalyst/Windows and fail).

The `Sentry.Maui` package reference was **deliberately not** given back the `Condition="...!= maccatalyst"` guard `main` has. See §3 — the guard was a workaround for a bug that `Android-Auto-Fase3` later root-caused and fixed properly; carrying the old workaround forward would have been a regression.

### 2.2 Verification

Built clean (0 errors, 0 warnings) for:

- `net10.0-android`
- `net10.0-maccatalyst`
- `net10.0-ios`
- full multi-target `dotnet build` (no `-f`)

`net10.0-windows` could not be built from this (macOS) machine — expected, needs a Windows host with the WinUI toolchain.

---

## 3. MacCatalyst startup-crash fix (ported from `Android-Auto-Fase3`)

Not part of the original ask, but restoring the MacCatalyst target from `main` would have reintroduced a crash that `Android-Auto-Fase3` diagnosed and fixed after `main` was last touched. Flagging it here rather than silently including it.

**Symptom (pre-fix, still present in `main`):** app crashes at startup on MacCatalyst. Root cause: under the Mac App Store / TestFlight hardened runtime, `dyld` refuses to load third-party bundled dylibs (SQLitePCLRaw's `libe_sqlite3.dylib`, Sentry's native crash-handler dylib) — `"security policy does not allow @ path expansion"`. `main`'s workaround was to disable Sentry entirely on MacCatalyst (`#if !MACCATALYST` around the Sentry setup, and originally a `Condition="...!= maccatalyst"` on the package reference) — it never touched the actual dylib-loading problem, and didn't cover SQLite.

**Fix carried over** (commits `142d203`, `210d5b8` on `Android-Auto-Fase3`):

- `Platforms/MacCatalyst/Entitlements.plist` — added `com.apple.security.cs.disable-library-validation`. This is the actual fix; it tells the hardened runtime to allow the bundled dylibs regardless of their signature.
- `Services/Diagnostics/CrashDiagnostics.cs` (new) — hooks `AppDomain.CurrentDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`, logs to `crash-diagnostics.log` in app data. Needed because a fire-and-forget `Task` that throws is otherwise invisible on MacCatalyst: Mono treats it as unhandled and calls `abort()` (SIGABRT) from the finalizer thread when the failed `Task` is garbage-collected, with no trace in the native crash report.
- `App.xaml.cs` — the two fire-and-forget calls in the constructor (`InitializeDbLoggingAsync`, and a new `LoadStationCatalogSafeAsync` wrapping `stationCatalog.LoadAsync()`) now catch and log through `CrashDiagnostics` instead of letting exceptions escape unobserved.
- `MauiProgram.cs` — `CrashDiagnostics.Initialize()` called first thing in `CreateMauiApp()`; the `#if !MACCATALYST` guards around the `using Sentry;` and the Sentry setup block were removed, since the entitlement fix means Sentry no longer needs to be disabled on MacCatalyst.

Net effect: Sentry now runs on MacCatalyst (better crash visibility there, matching the other platforms), and the actual dylib-loading crash is fixed at the source instead of being worked around by turning a feature off.

---

## 4. Two fixes ported into `Services/Audio/AudioService.cs`

`AudioService.cs` is the shared, cross-platform playback engine (`CommunityToolkit.Maui.MediaElement`-based) used on iOS, macOS and Windows. It was not touched by the Android Media3 rewrite, so it never received two fixes that landed independently on `Android-Auto-Fase3` (its own "Fix 1.C" / "Fix 1.D", predating that branch's Media3 work).

### 4.1 Fix #14 — thread-safety of the reconnect `CancellationTokenSource`

Before: `_reconnectCts` was cancelled, disposed and replaced from multiple call sites (`PlayAsync`, `PauseAsync`, `StopAsync`, `StopImmediate`, `Shutdown`, the watchdog) with no synchronization — a `Cancel()`/`Dispose()` race with a concurrent `RenewReconnectCts()` could throw `ObjectDisposedException` or leak a token that never gets observed.

After: a private `_ctsLock` object guards every access. `RenewReconnectCts()` locks around cancel+dispose+replace; two new helpers, `CancelReconnect()` and `CurrentReconnectToken()`, replace every direct `_reconnectCts.Cancel()` / `_reconnectCts.Token` call site with a locked equivalent.

### 4.2 Fix #9 — `IAudioFocusManager`

New files:

- `Services/Audio/IAudioFocusManager.cs` — `RequestFocus()`, `AbandonFocus()`, `NotifyVolumeChanged(double)`.
- `Services/Audio/NullAudioFocusManager.cs` — no-op implementation (always grants focus). Registered for iOS, macOS, Windows.
- `Platforms/Android/Services/AudioFocusManager.cs` — real `AudioManager`-based implementation: requests `AudioFocus.Gain`, ducks to 20% on `LossTransientCanDuck` (e.g. turn-by-turn directions), pauses on `LossTransient` (e.g. phone call) and auto-resumes on `Gain`, stops on permanent `Loss`.

`AudioService.cs` now takes `IAudioFocusManager` in its constructor and calls `RequestFocus()` before playing/resuming (bailing out with a logged warning if focus is denied), and `AbandonFocus()` on every stop/shutdown path. `SetVolume` reports the new value via `NotifyVolumeChanged` so a duck-then-restore returns to the user's actual volume, not the ducked one.

**Honest caveat, stated plainly rather than left implicit:** on Android, this class is currently **unused**. `RadioPlaybackService` (the Media3/ExoPlayer service) requests and abandons audio focus itself, natively, via `SetAudioAttributes(audioAttributes, handleAudioFocus: true)` — see `RefactorReport_RadioE45_EN.md` §4.1. `AudioFocusManager` was ported for parity with `Android-Auto-Fase3` and because `IAudioFocusManager` needed a real Android implementation to exist somewhere for the abstraction to make sense; it is wired into DI (`#if ANDROID`) but nothing on Android currently resolves it through `AudioService`, since Android's registered `IAudioService` is `Media3AudioService`, not `AudioService`.

---

## 5. Proactive stream-URL resolution (`IStreamUrlProber`)

### 5.1 Before

`RadioPlaybackService` picked a station's stream URL **reactively**: `ResolveStreamUrl()` always started at candidate index 0 (`OnAirStreamUrl` → `StreamUrlFallback` → `HlsUrl` → `StreamUrl`) and only moved to the next candidate after ExoPlayer had already tried to play the current one and failed (`OnStreamError`). First tap on a station with a dead primary URL meant an audible failure-then-retry cycle.

### 5.2 After

New shared abstraction, extracted from `AudioService.cs`'s private probing methods (which duplicated it) into:

- `Services/Audio/IStreamUrlProber.cs` — `Task<string?> ProbeFirstReachableAsync(string[] urls, CancellationToken ct)`.
- `Services/Audio/StreamUrlProber.cs` — probes every candidate in parallel via `HttpClient` (`GET`, headers-only, 3s timeout per URL), cancels the rest as soon as one answers with a success status code.

Registered as a singleton in `MauiProgram.cs`, used by both `AudioService.cs` (iOS/macOS/Windows — replaces its own inline probe, no behavior change there) and, new, by `RadioPlaybackService.cs`:

- `BuildStationItemProbedAsync(station, metadataOverride)` — probes all candidates, uses the first reachable one; falls back to candidate 0 if none answer (playback is always attempted, never blocked on a probe failure). Records which candidate won in `_streamAttempts[stationId]`, so if that URL *later* fails during actual playback, `OnStreamError`'s existing reactive fallback resumes from the right index instead of restarting at 0.
- Called from `OnAddMediaItems` (Android Auto browse selection, voice search) and from `OnSetMediaItems`, but **only for the station about to play** — the rest of the Android Auto queue (built so next/prev works) keeps its old lazily-resolved, unprobed URI. Probing all ~N stations before returning from a single tap would have added real latency to every station selection; probing only the one that's about to start doesn't.

---

## 6. Voice search (Gemini / Google Assistant)

Ported from `Android-Auto-Fase3`'s `RadioLibrarySessionCallback`, adapted into `RadioPlaybackService.LibraryCallback`.

**Why this needs a workaround at all:** Media3 has no `OnPlayFromSearch` callback. A spoken "play RadioE45" / "play \<station\> on RadioE45" arrives through the *same* `OnAddMediaItems` / `OnSetMediaItems` callbacks as a normal Android Auto tap, as a `MediaItem` with no resolvable `MediaId` but a populated `RequestMetadata.SearchQuery` — Media3 translates the legacy `playFromSearch` call internally before it reaches session callbacks.

**Second workaround, inside the first:** the Xamarin/.NET binding for `Xamarin.AndroidX.Media3.Common` exposes `MediaItem.Builder.SetRequestMetadata()` but has no bound getter for the Java field `MediaItem.requestMetadata`. `TryGetSearchQuery(MediaItem)` reads it via raw Java reflection (`item.Class.GetField("requestMetadata")`) and casts the result to the (correctly bound) `MediaItem.RequestMetadata` type, whose `SearchQuery` property does work.

**Resolution logic** (`ResolveSearchStation`, shared by both callbacks):

- `TryGetSearchQuery` returns `null` → not a search request at all, handled normally (unresolved MediaId passes through unchanged).
- Returns `""` (empty) → generic voice command ("play something") → favorite station, else the first one.
- Returns non-empty text → case-insensitive substring match on station name; no match → first station, with a logged warning.

---

## 7. In-car crash — investigated, not modified

Asked to look into the crash flagged in `RefactorReport_RadioE45_EN.md` §7 (`reason=5 APP CRASH(NATIVE)`, SIGSEGV, 2026-07-21 07:08, app idle overnight then killed on getting into the car). Finding: **already diagnosed and mitigated**, before this session, in commit `dbd5103` ("root the JNI peers handed to Java, and fix four latent defects"), which this branch already contains.

Diagnosis (from the commit message, since the tombstone had already rotated out of dropbox by the time it was written): a SIGSEGV after hours idle in a MAUI app is consistent with a JNI peer garbage-collected on the managed side while only Java still held a reference to it. Three `Java.Lang.Object` instances were being handed to Java with nothing rooting them:

- `LibraryCallback`, passed to `MediaLibrarySession.Builder` — the prime suspect, since Android Auto calls `OnGetLibraryRoot` on it at the exact moment the phone connects to the car.
- `TaskResolver`, held by `CallbackToFutureAdapter` while a future is pending.
- The connect `Runnable` handed to the `MediaController` future in `Media3AudioService`.

All three are now rooted for as long as Java can call into them — verified present in the current code: `RadioPlaybackService._libraryCallback` (instance field), `TaskResolver._root` (`GCHandle`), `Media3AudioService._connectCallback` (instance field).

**Status: unchanged by this session.** The fix removes a class of causes consistent with the evidence but isn't proven — there's no stack trace to confirm against, only the timing match. The branch stays held back from Google Play until confirmed in the field. Nothing further to do here beyond what `dbd5103` already did; this section documents that the investigation was re-run and reached the same conclusion, not that anything new was found or changed.

---

## 8. Files touched this session

**New:**

- `Services/Audio/IAudioFocusManager.cs`
- `Services/Audio/NullAudioFocusManager.cs`
- `Platforms/Android/Services/AudioFocusManager.cs`
- `Services/Audio/IStreamUrlProber.cs`
- `Services/Audio/StreamUrlProber.cs`
- `Services/Diagnostics/CrashDiagnostics.cs`

**Modified:**

- `RadioE45.csproj` — multi-platform restore (§2)
- `Platforms/MacCatalyst/Entitlements.plist` — `disable-library-validation` (§3)
- `App.xaml.cs` — guarded fire-and-forget startup calls (§3)
- `MauiProgram.cs` — `CrashDiagnostics.Initialize()`, Sentry MacCatalyst guard removed (§3), `IAudioFocusManager` / `IStreamUrlProber` DI wiring (§4, §5)
- `Services/Audio/AudioService.cs` — CTS thread-safety, audio focus, `IStreamUrlProber` (§4, §5)
- `Platforms/Android/Services/RadioPlaybackService.cs` — proactive probing, voice search (§5, §6)

## 9. Build verification

`dotnet build` run for each TFM individually and once for the full multi-target project — 0 errors, 0 warnings in all cases:

- `net10.0-android`
- `net10.0-maccatalyst`
- `net10.0-ios`
- multi-target (no `-f`)

`net10.0-windows` not buildable from this machine (macOS host, no WinUI toolchain) — needs verification on a Windows machine before merge.

Not yet done (out of scope for this session): running the app on an iOS/MacCatalyst simulator or device, in-car Android Auto re-test of the crash fix, and a real voice-search test against Gemini/Assistant.
