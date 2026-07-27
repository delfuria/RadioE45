# RadioE45 — Playback Layer Rebuild Report (Android Auto / Bluetooth / Background Service)

**Audience:** author of the RadioE45 app
**Subject:** ground-up rebuild of the Android audio layer — migration to AndroidX **Media3 / ExoPlayer**
**Branch:** `android-auto-media3` (created from `main`)
**Platform:** .NET MAUI 10, `net10.0-android` (Android; minSdk 26 / targetSdk 36), package `com.radioe45.app`
**Starting point:** version 0.20 (versionCode 20)
**Report date:** 2026-07-20

> This document describes **what was changed and why**, relative to the original version of the app. The starting point was the earlier technical analysis (`ResumeRadioE45_EN.md`), which identified the root causes of the Android Auto, Bluetooth and background-playback problems. This report is its continuation: a description of the fix that was carried out.

---

## 1. Executive summary

The Android playback layer was **redesigned from scratch**. The previous design relied on a player (`CommunityToolkit.Maui.MediaElement`) **living inside the UI** (`OnAirPage`) plus **two–three independent `MediaSession`s** with manual state synchronization through a static store. That was the root cause of most symptoms: a "dead" Play button when started from the car, no next/prev, no artwork in AA, and ambiguous Bluetooth button routing.

The new architecture is **a single Media3 service (`MediaLibraryService`) with one `ExoPlayer` and one `MediaLibrarySession`** as the single source of truth. Android Auto, Bluetooth (AVRCP), the media notification and the lock screen all bind to **the same session**. The phone UI **no longer owns a player** — it drives the session through a `MediaController`, exactly as the car does.

Result: play/pause/stop, **next/prev between stations**, the browse tree (root → stations), artwork and "now playing" metadata are now consistent across every surface (UI, AA, BT, lock screen, notification).

| Area | Before | After |
|--------|-------|-----|
| Player | `MediaElement` in the UI (`OnAirPage`) | one `ExoPlayer` in the service |
| Media sessions | 2–3 competing `MediaSession`s | one `MediaLibrarySession` |
| Play from car with UI closed | silent `return` (nothing plays) | works — player lives independently of the UI |
| Next / Prev | absent | full station queue, seek-to-next/prev |
| Artwork in AA | absent | `ArtworkUri` in metadata |
| State sync | manual static store | native, via a single session |
| API | framework `Android.Media.Session` | **AndroidX Media3** (Google's recommendation for AA) |

---

## 2. Why the rebuild (diagnosis recap)

The full diagnosis is in `ResumeRadioE45_EN.md`. The key root causes:

1. **Player pinned to the UI.** `AudioService` required a live `MediaElement`, set only in `OnAirPage.OnAppearing()`. Android Auto connects to the service **without launching the phone UI** → commands hit `null` → Play did nothing.
2. **Multiple competing `MediaSession`s.** `RadioMediaBrowserService` ("RadioE45Auto"), `AndroidMediaNotificationService` ("RadioE45Playback"), and the toolkit's internal session. The system routes BT/AVRCP buttons to a single active session — with several active, behavior was unpredictable.
3. **No next/prev** in the code or in the `IAudioService` interface; no artwork in the AA tree; legacy API instead of Media3.

The analysis's conclusion (Priority 0): **move the player out of the UI into a service and unify onto a single Media3 session.** That is exactly what was done.

---

## 3. New architecture

```
                    ┌──────────────────────────────────────────────┐
                    │ RadioPlaybackService : MediaLibraryService     │
   Android Auto ───▶│                                                │
   Bluetooth   ───▶│   one ExoPlayer  +  one MediaLibrarySession     │
   Notification ──▶│                                                │
   Lock screen ───▶│   • browse tree (root → stations)              │
                    │   • queue = full station list (next/prev)      │
   Phone UI ─────┐  │   • stream-URL fallback                        │
                 │  │   • artwork / metadata                         │
                 │  └──────────────────────────────────────────────┘
                 │                     ▲
                 └──── MediaController ─┘   (Media3AudioService : IAudioService)
```

**Principle:** exactly one player and one session. Everything — including the phone UI — is just a **controller** of that session.

---

## 4. What was implemented (details)

### 4.1 `RadioPlaybackService` — the new Media3 service *(new file)*
`Platforms/Android/Services/RadioPlaybackService.cs`

The core of the rebuild. A `MediaLibraryService` containing:

- **One `ExoPlayer`** built in `OnCreate` with `AudioAttributes` (usage=media, content=music), `SetHandleAudioBecomingNoisy(true)` (pause on BT/headphone unplug — implements recommendation #10 from the analysis) and `SetWakeMode(WakeModeNetwork)`.
- **One `MediaLibrarySession`** with `SetSessionActivity(...)` → tapping the notification / AA card / lock screen opens the app.
- **Browse tree** for Android Auto/Assistant via `LibraryCallback` (`OnGetLibraryRoot`, `OnGetChildren`, `OnGetItem`): root → the station list from the catalog.
- **Next/Prev.** `OnSetMediaItems` expands a single station selection into the **full queue of all stations** positioned on the chosen one → Seek-to-Next/Previous from the car or steering wheel moves between stations.
- **URI resolution.** Media3 strips the URI when media items cross the binder, so `OnAddMediaItems` re-resolves it from the catalog while **preserving** the "now playing" metadata sent by the controller.
- **Stream-URL fallback.** Candidate list in the order `OnAirStreamUrl → StreamUrlFallback → HlsUrl → StreamUrl`. The public `StreamUrlFallback` (`https://{UrlBase}{StreamUrl}`) takes priority over the API's `ListenUrl`, which returns a LAN address (`192.168.1.100`) reachable only inside the station's network. On a playback error the service advances to the next candidate and retries; on success (`StateReady`) it resets the counter.
- **Background playback.** `OnTaskRemoved`: when the user swipes the app off recents while playback is ongoing — **it keeps playing**; the service stops only when nothing is playing.

### 4.2 `Media3AudioService` — the `IAudioService` implementation for the UI *(new file)*
`Platforms/Android/Services/Media3AudioService.cs`

A thin layer connecting the UI to the session via a **`MediaController`**:

- `Initialize(MediaElement)` — the `MediaElement` argument is now **unused on Android** (the player lives in the service); the method only starts the controller connection so the session is ready before the first Play.
- `PlayAsync` → `SetMediaItem` (the service expands to the full queue), `Prepare`, `Play`. `PauseAsync`/`ResumeAsync`/`StopAsync`/`StopImmediate`/`SetVolume` → controller commands.
- **Live metadata** (`UpdateMetadata`) → `ReplaceMediaItem(currentIndex)` keeping the media id and stream (updates without restarting playback); field caching to skip redundant round-trips to the session.
- State listening via `IPlayerListener` (`OnIsPlayingChanged`, `OnPlaybackStateChanged`, `OnPlayerError`).
- **Sync with the car:** `OnMediaItemTransition` — when the station changes from the car/steering wheel (next/prev), the service updates `_currentStation` and raises the new `StationChanged` event, so the UI switches its display and "now playing" polling to the correct station.

### 4.3 `IAudioService` interface — new event *(change)*
`Services/Audio/IAudioService.cs`

Added `event EventHandler<AzuraStation> StationChanged` — a station change from outside the phone UI (AA / car display / steering-wheel button). The rest of the interface is unchanged, so `AudioService` on iOS/desktop still works as before.

### 4.4 DI registration *(change)*
`MauiProgram.cs`

```csharp
#if ANDROID
    builder.Services.AddSingleton<IAudioService, Media3AudioService>();
#else
    builder.Services.AddSingleton<IAudioService, AudioService>();
#endif
```
On Android the `IPlatformNowPlayingService → AndroidNowPlayingService` registration was removed (Media3 takes over the notification and "now playing"). iOS/macOS/desktop unchanged.

### 4.5 Manifest *(change)*
`Platforms/Android/AndroidManifest.xml`

- Removed the `<service>` declarations of the old services.
- `RadioPlaybackService` declared via `[Service]` + `[IntentFilter]` attributes with the actions `androidx.media3.session.MediaLibraryService` and (backward compatibility) `android.media.browse.MediaBrowserService`; `ForegroundServiceType = mediaPlayback`.

### 4.6 Deleted files *(deletions)*
The entire old integration layer was removed — Media3 took over its role:
- `Platforms/Android/Services/RadioMediaBrowserService.cs` ("RadioE45Auto" session)
- `Platforms/Android/Services/AndroidMediaNotificationService.cs` ("RadioE45Playback" session + notification)
- `Platforms/Android/Services/AndroidNowPlayingService.cs` (bridge to the store)
- `Platforms/Android/Services/AndroidNowPlayingStateStore.cs` (static snapshot store)
- `Platforms/Android/AudioLifecycleService.cs` (swipe-stop — replaced by `OnTaskRemoved` in the new service)

### 4.7 Playback UI *(change)*
`Views/OnAirPage.xaml` + `.xaml.cs`, `ViewModels/OnAirViewModel.cs`

- Control row: **⏮ previous · play/pause · ⏭ next** (the Stop button was removed).
- `NextStationCommand` / `PreviousStationCommand` walk through `_catalog.Stations` (via `SelectStationAsync`).
- The leftover `MediaElement` is removed from the visual tree on Android in the `OnAirPage` constructor (`#if ANDROID`), so it doesn't create a second, empty session.

### 4.8 NuGet packages / build *(change)*
`RadioE45.csproj`

- Added the Media3 family (consistent 1.10.x line): `Xamarin.AndroidX.Media3.Session` 1.10.1.2, `…Media3.ExoPlayer` / `…ExoPlayer.Hls` / `…Media3.Common` 1.10.1.1.
- Media3 bumps `Xamarin.AndroidX.Core` to 1.19.x → a pin of `Xamarin.AndroidX.Core.Core.Ktx` = 1.19.0.1 is required (otherwise R8 reports a duplicate `androidx.core.animation.AnimatorKt`).
- **Build note (Windows):** the branch is single-TFM (Android only) — do **not** pass `-p:TargetFrameworks` on `restore`/`build`.

---

## 5. Additional changes (beyond the Media3 core)

Alongside the rebuild, some general improvements were made:

- **Localization (RESX).** New `Resources/Strings/AppResources.resx` (default), `.en`, `.pl` + `Services/Localization/` (`LocalizationResourceManager`, `TranslateExtension`). UI strings via `{loc:Translate Key}`. Eases a multi-language release (including IT).
- **ViewModel cleanups** (`RadioListViewModel`, `SettingsViewModel`, `AddStationViewModel`, `ScheduleViewModel`): simplifications, error handling (incl. reload on 429/RateLimiting), user-managed stations.
- **UI/navigation:** `AppShell.xaml`, `SettingsPage.xaml`, `RadioListPage.xaml` (flyout action button), `AddStationPage.xaml` (Cancel button), styles.
- **App icon** (`appiconfg.svg`) and minor fixes.

---

## 6. Mapping: original bug → fix

| # from analysis | Problem | Status on this branch |
|---|---|---|
| #4 | Cold-start Play from AA does nothing (player in UI) | **Fixed** — player in the service, independent of the UI |
| #5 / #11 | Multiple `MediaSession`s / ambiguous BT buttons | **Fixed** — one `MediaLibrarySession` |
| #6 | Legacy API instead of Media3 | **Fixed** — migrated to AndroidX Media3 |
| next/prev, artwork | Absent in AA | **Fixed** — full queue + `ArtworkUri` |
| #1 / #2 | Fragile `startForeground` / FGS start from background | **Structurally resolved** — Media3 manages foreground and the notification itself |
| #10 | No reaction to BT disconnect | **Fixed** — `SetHandleAudioBecomingNoisy(true)` |
| #3 | Background audio continuity tied to the UI | **Fixed** — `OnTaskRemoved` + player in the service |
| #9 | Audio focus (ducking/pause for navigation/calls) | **Partial** — Media3 handles audio focus via `AudioAttributes`, but **needs in-car verification** (see §8) |

---

## 7. Build & test status

**Validated on `emulator-5554` (phone AVD, API 37) — logcat/dumpsys:**
- the app starts without a crash;
- **exactly one** Media3 session is created (`ExoPlayerImpl Init` + `MediaSessionImpl Init` + `addSession`);
- the `MediaController` (UI) connects to the session; playback runs through controller → session → player;
- **the queue expands to the full catalog → next/prev** (queue size = number of stations);
- Media3 notification with "Seek to previous / Pause / Seek to next" actions (foreground, transport category);
- the AVRCP/BT layer registers the player (`MediaPlayerList: Adding wrapped media player`).

**Validated in a real car — physical phone + Android Auto on the head unit (update 2026-07-27):**
- **Android Auto interface on the car screen**: station list browsing, starting playback from the browse tree, cover art and metadata, play/pause commands;
- **next/prev from the car**, with the phone UI staying in sync (no metadata overwrite from the previous station);
- **Bluetooth / AVRCP**: head unit display and steering-wheel buttons;
- **background playback**, continuing after the app is closed.

In short: the three problems that motivated the rebuild — no controls in AA, no cover art, no next/prev — are resolved in the field, not just on the emulator.

**⚠️ One crash observed during in-car testing — cause not yet identified.** It happened once; diagnosis is under way. Until it is understood, **this branch must not be published to Google Play**: treat it as ready for review and testing, not for release.

**Still to confirm:**
- audio focus in interruption scenarios: navigation prompt (ducking) and incoming call (pause + auto-resume) — see §8.1;
- behaviour on head units from makes/models other than the one tested.

> Test note: the default `ListenUrl` from the API is the LAN address `192.168.1.100:8000` — unreachable from the emulator, so a public stream URL is needed for testing (hence the priority of `StreamUrlFallback`).

---

## 8. Known limitations and TODO for the author

1. **In-car audio focus — to be confirmed.** Media3 handles focus via `AudioAttributes`, but the real scenarios should be tested: navigation prompt (ducking) and phone call (pause + auto-resume) in the car / over BT.
2. **Android Auto certification (independent of the code!).** "Media"-category AA apps require **Google review and distribution through Google Play**. A sideloaded (APK) build appears in AA **only** in AA developer mode with "unknown sources" enabled. Production path: publish to Play + apply to the Android for Cars program.
3. **Station configuration / stream URL.** The API's `ListenUrl` can be a LAN address — keep a correct public `StreamUrlFallback` in the station data (there is a TODO in `AzuraStationCatalog.Map`).
4. **Pause on live.** Pause = `ExoPlayer.Pause()`; on resume the live stream jumps to the live edge anyway (correct for live radio, unlike the old connection-closing behavior).
5. **Content style / AA extensions** (AA list layout) — cosmetic, optional before certification.
6. iOS/macOS/desktop stay on the existing `AudioService` (`MediaElement`) — the rebuild is Android-only.

---

## 9. Changed files (summary)

**New:**
- `Platforms/Android/Services/RadioPlaybackService.cs`
- `Platforms/Android/Services/Media3AudioService.cs`
- `Resources/Strings/AppResources{.resx,.en.resx,.pl.resx}`
- `Services/Localization/{LocalizationResourceManager,TranslateExtension}.cs`

**Changed (key):**
- `MauiProgram.cs`, `Services/Audio/IAudioService.cs`
- `Platforms/Android/AndroidManifest.xml`, `Platforms/Android/MainActivity.cs`
- `Views/OnAirPage.xaml(.cs)`, `ViewModels/OnAirViewModel.cs`
- `RadioE45.csproj`
- UI ViewModels and views (Settings, RadioList, AddStation, Schedule, AppShell)

**Deleted:**
- `RadioMediaBrowserService.cs`, `AndroidMediaNotificationService.cs`, `AndroidNowPlayingService.cs`, `AndroidNowPlayingStateStore.cs`, `AudioLifecycleService.cs`

Net vs `HEAD`: roughly +372 / −1166 lines (a net simplification of the Android layer).

---

## 10. Recommended next steps

1. **Track down the cause of the crash seen in the car** (§7) — a prerequisite to any publication. Sentry is already wired into the app: the matching event, with its stack trace, is the first place to look.
2. **Audio focus in interruption scenarios** — navigation prompt (ducking) and incoming call (pause + auto-resume), in the car and over BT.
3. **Clean up station data** — public stream URLs (avoid LAN addresses in `ListenUrl`).
4. **Production AA path:** release on Google Play + apply to Android for Cars (media category).
5. Optional: content style for AA, finishing the IT localization.

---

*This report is also available in Italian (`RefactorReport_RadioE45_IT.md`).*
