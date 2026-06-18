# RadioE45 — Implementation Plan: Android Auto, Bluetooth, Background Service

**Based on:** `ResumeRadioE45_EN.md` (external technical analysis, 2026-06-14)
**Drafted:** 2026-06-17
**Scope:** Phases 0–2 (Phase 3 / Media3 migration deferred to a future release)
**Primary platform:** Android · **Other platforms:** iOS, macOS, Windows (impact verified)

---

## Current Status

All bugs described in the external analysis are **confirmed present** in the codebase at the time of writing.

| Area | Current Rating |
|---|:---:|
| Background service (foreground) | 🟠 Medium |
| Android Auto | 🔴 Low / risky |
| Bluetooth / media button | 🔴 Low |
| Core audio (code quality) | 🟢 High |

---

## Legend

| Symbol | Meaning |
|---|---|
| 🟢 | Zero risk |
| 🟡 | Low risk — isolated file or additive logic |
| 🟠 | Medium risk — touches critical paths but with a bounded scope |
| 🔴 | High risk — architectural change |
| ★☆☆☆☆ | Minimum difficulty |
| ★★★★★ | Maximum difficulty |

---

## Cross-platform Considerations

Before diving into the phases, a summary of the impact on non-Android platforms.

Files under `Platforms/Android/` are compiled **exclusively** for Android — iOS, macOS, and Windows do not see them. The only exceptions are shared files:

| Fix | Shared File | Impact on iOS/macOS/Windows |
|---|---|---|
| #12 | `AudioService.cs` | Touched — beneficial (less aggressive timeout everywhere) |
| #14 | `AudioService.cs` | Touched — beneficial (thread safety on all platforms) |
| #4  | `AudioService.cs` | Touched — no-op (no external callers on desktop/iOS) |
| #9  | `AudioService.cs` + `MauiProgram.cs` | **Requires IAudioFocusManager pattern** (see Phase 1.C) |

**Solution for Bug #9:** add `IAudioFocusManager` + `NullAudioFocusManager` (always-granted) with conditional DI registration in `MauiProgram.cs` — identical to the pattern already used for `IPlatformNowPlayingService`. No `#if` in `AudioService.cs`.

---

## Phase 0 — Cleanup (estimate: 15–30 min)

1–2 line changes, zero impact on runtime logic. All in a single commit.

### #12 — Watchdog timeout too aggressive
- **File:** `Services/Audio/AudioService.cs:19`
- **Change:** `BufferingTimeoutSeconds = 1.0` → `12.0`
- **Rationale:** 1 second causes continuous reconnects on a variable connection in-car
- **Platforms:** all (beneficial everywhere)
- **Difficulty:** ★☆☆☆☆ · **Risk:** 🟢

### #13 — Unused storage permissions
- **File:** `Platforms/Android/AndroidManifest.xml:31-32`
- **Change:** remove `READ_EXTERNAL_STORAGE` and `WRITE_EXTERNAL_STORAGE`
- **Rationale:** unused, ignored on API 30+, flagged by Play Console
- **Platforms:** Android only
- **Difficulty:** ★☆☆☆☆ · **Risk:** 🟢

### #15 — README: wrong API version
- **File:** `README.md`
- **Change:** "Android 8.0+ (API 21)" → "Android 8.0+ (API 26)"
- **Platforms:** documentation
- **Difficulty:** ★☆☆☆☆ · **Risk:** 🟢

---

## Phase 1 — Standalone Android Fixes (estimate: 2–3 hours)

Each fix is independent of the others and can be committed separately. Recommended order: A → B → D → C (C last because it requires a physical device for full testing).

### 1.A — #2: try/catch on `StartForegroundService`
- **File:** `Platforms/Android/Services/AndroidMediaNotificationService.cs` — method `StartService` (line 82)
- **Change:** wrap `StartForegroundService`/`StartService` calls in `try/catch` to handle `ForegroundServiceStartNotAllowedException` (Android 12+, API 31+)
- **Rationale:** a background start without a catch causes an unhandled crash
- **Platforms:** Android only
- **Difficulty:** ★☆☆☆☆ · **Risk:** 🟢
- **Emulator test:** ✅ verifiable

```csharp
// Patch to apply in StartService():
try
{
    if (preferForeground && OperatingSystem.IsAndroidVersionAtLeast(26))
        context.StartForegroundService(intent);
    else
        context.StartService(intent);
}
catch (Exception ex) when (
    ex is Java.Lang.IllegalStateException ||
    ex is Android.App.ForegroundServiceStartNotAllowedException)
{
    System.Diagnostics.Debug.WriteLine($"FGS start blocked: {ex.Message}");
}
```

---

### 1.B — #10: `BecomingNoisyReceiver` (BT disconnect / headphones unplugged)
- **New file:** `Platforms/Android/Services/BecomingNoisyReceiver.cs`
- **Modified file:** `AndroidMediaNotificationService.cs` (`OnCreate`/`OnDestroy` only)
- **Change:** new `BroadcastReceiver` for `ACTION_AUDIO_BECOMING_NOISY` → calls `PauseAsync()`; registered/unregistered in the service lifecycle
- **Rationale:** without this fix, disconnecting BT or headphones blasts audio through the speaker at full volume
- **Platforms:** Android only
- **Difficulty:** ★★☆☆☆ · **Risk:** 🟡
- **Emulator test:** ⚠️ partial (simulatable via ADB: `adb shell input keyevent KEYCODE_HEADSETHOOK`); full test requires physical device

---

### 1.C — #9: `AudioFocusManager` (audio focus / ducking)
- **New file:** `Platforms/Android/Services/AudioFocusManager.cs`
- **New file:** `Services/Audio/IAudioFocusManager.cs` (shared interface)
- **New file:** `Services/Audio/NullAudioFocusManager.cs` (no-op for iOS/macOS/Windows)
- **Modified file:** `MauiProgram.cs` (conditional DI registration `#if ANDROID`)
- **Modified file:** `AudioService.cs` (inject `IAudioFocusManager`, call `RequestFocus` before opening the stream, `AbandonFocus` in `StopAsync`/`Shutdown`, save/restore volume for ducking)
- **Change:** handle `RequestAudioFocus` / `AbandonAudioFocus`, react to `AudioFocus.Loss` / `LossTransient` / `LossTransientCanDuck` / `Gain`
- **Rationale:** without audio focus, GPS navigation and calls don't duck/pause the radio — a common cause of rejection in the AA review
- **Platforms:** Android only for implementation; interface on all platforms (null-safe)
- **Difficulty:** ★★★☆☆ · **Risk:** 🟠
- **Emulator test:** ⚠️ limited (focus grant/deny verifiable with AA Media Simulator); real ducking on calls/navigation requires physical device
- **Note:** store the user volume in a `_userVolume` field instead of restoring hardcoded `1.0` after duck

---

### 1.D — #14: Thread-safety of `_reconnectCts`
- **File:** `Services/Audio/AudioService.cs`
- **Change:** add `_ctsLock`, three helper methods (`RenewReconnectCts`, `CancelReconnect`, `CurrentReconnectToken`), replace 4 direct accesses to `_reconnectCts`
- **Rationale:** `Cancel()` after `Dispose()` from different threads can cause `ObjectDisposedException`
- **Platforms:** all (improves thread safety everywhere)
- **Difficulty:** ★★☆☆☆ · **Risk:** 🟠
- **Emulator test:** ✅ verifiable — test rapid pause/resume and network changes

```csharp
// Structure to implement in AudioService.cs:
private readonly object _ctsLock = new();

private void RenewReconnectCts() { lock (_ctsLock) { _reconnectCts.Cancel(); _reconnectCts.Dispose(); _reconnectCts = new(); } }
private void CancelReconnect() { lock (_ctsLock) { _reconnectCts.Cancel(); } }
private CancellationToken CurrentReconnectToken() { lock (_ctsLock) { return _reconnectCts.Token; } }
```

---

## Phase 2 — Architectural Bridge (estimate: 4–6 hours)

**This is the production solution** (not a temporary bridge), since the Media3 migration is deferred. The order is binding: **2.A before 2.B**, then 2.C and 2.D in parallel.

### 2.A — #1: Guarantee `startForeground` is always called ⚠️ Most critical fix
- **File:** `Platforms/Android/Services/AndroidMediaNotificationService.cs`
- **Change:** add `EnsureForegroundStarted()` at the beginning of `OnStartCommand`; if the snapshot is empty, publish a placeholder notification ("RadioE45") before deciding whether to stop
- **Rationale:** without this fix, if `startForegroundService` is called with an empty snapshot (typical in the first ~500ms of stream startup), the system throws `ForegroundServiceDidNotStartInTimeException` — process crash
- **Platforms:** Android only
- **Difficulty:** ★★☆☆☆ · **Risk:** 🟠
- **Emulator test:** ✅ — test explicitly: start the stream on a station with no "now playing" metadata
- **Order:** **must precede 2.B**

---

### 2.B — #5/#11: `SharedMediaSessionHolder` (single session)
- **New file:** `Platforms/Android/Services/SharedMediaSessionHolder.cs`
- **Modified file:** `AndroidMediaNotificationService.cs` (assigns `Session` in `OnCreate`, null in `OnDestroy`)
- **Modified file:** `RadioMediaBrowserService.cs` (removes its own `"RadioE45Auto"` session creation, uses `SessionToken` from the shared session)
- **Rationale:** three active MediaSession instances cause ambiguous BT routing and inconsistent state; AA, lock screen, and BT must see a single source of truth
- **Platforms:** Android only
- **Difficulty:** ★★★☆☆ · **Risk:** 🟠
- **Race condition:** if `RadioMediaBrowserService.OnCreate` runs before the session exists, subscribe to `SessionReady` to assign `SessionToken` asynchronously
- **Emulator test:** ✅ — verify with AA Media Simulator that Play/Pause from the simulator controls the radio

```csharp
// SharedMediaSessionHolder.cs
internal static class SharedMediaSessionHolder
{
    private static MediaSession? _session;
    public static event Action<MediaSession>? SessionReady;

    public static MediaSession? Session
    {
        get => _session;
        set { _session = value; if (value is not null) SessionReady?.Invoke(value); }
    }
}
```

---

### 2.C — #4: `_pendingStation` bridge (Play from AA with UI closed)
- **File:** `Services/Audio/AudioService.cs` (field `_pendingStation`, changes to `PlayAsync` and `Initialize`)
- **File:** `Platforms/Android/Services/RadioMediaBrowserService.cs` (method `EnsureUiLaunched` in `OnPlay`/`OnPlayFromMediaId`)
- **Change:** if `PlayAsync` is called with `_mediaElement == null`, store the station in `_pendingStation`; when `Initialize()` is called from the UI, execute the pending play
- **Rationale:** without this fix, Play from Android Auto with the UI not launched silently does nothing
- **Platforms:** `AudioService.cs` shared — no-op on iOS/macOS/Windows (no external callers on desktop)
- **Difficulty:** ★★★☆☆ · **Risk:** 🟠
- **Known limitation:** `EnsureUiLaunched()` (launching an Activity from the background) is **unreliable on Android 10+** due to system policy. The app works correctly if the UI is already launched; a cold-start purely from AA may not start playback. Definitive solution: Media3 migration (Phase 3, out of scope).
- **Emulator test:** ✅ partial — test with UI in foreground; AA cold-start not verifiable without physical device

---

### 2.D — #7/#8: Caller allowlist + content style hints (30 min)
- **File:** `Platforms/Android/Services/RadioMediaBrowserService.cs`

**Bug #7 — Allowlist too restrictive:**
- Add to `AllowedCallers`: `com.google.android.carassistant`, `com.android.bluetooth`, `com.google.android.wearable.app`
- Difficulty: ★☆☆☆☆ · Risk: 🟡

**Bug #8 — No content style hints:**
- Return `extras` with `CONTENT_STYLE_BROWSABLE_HINT` / `CONTENT_STYLE_PLAYABLE_HINT` in `OnGetRoot`
- Improves list layout in AA (cosmetic, impacts certification rating)
- Difficulty: ★☆☆☆☆ · Risk: 🟢

---

## Phase 3 — Media3 Migration (future release, out of current scope)

Migrating to `androidx.media3.session.MediaLibraryService` with ExoPlayer in the service definitively resolves bugs #3, #4, #5, #6, #11. Phases 0–2 do not create blocking technical debt for this migration — the Phase 2 bridges are removed and replaced.

**Prerequisites before starting Phase 3:**
- NuGet `Xamarin.AndroidX.Media3.Session`, `Xamarin.AndroidX.Media3.ExoPlayer`
- Physical Android device for testing
- Google Play publication (required for AA production distribution)

---

## Recommended Execution Order

```
Phase 0   ──▶  #12, #13, #15                     (1 commit, 15 min)
  │
  ▼
Phase 1A  ──▶  #2  try/catch FGS                 (low risk, start here)
Phase 1B  ──▶  #10 BecomingNoisyReceiver
Phase 1D  ──▶  #14 CTS thread-safety
Phase 1C  ──▶  #9  AudioFocusManager             (last, physical device test)
  │
  ▼
Phase 2A  ──▶  #1  EnsureForegroundStarted        (BEFORE 2B — mandatory)
Phase 2B  ──▶  #5/#11 SharedMediaSessionHolder
Phase 2C  ──▶  #4  _pendingStation bridge
Phase 2D  ──▶  #7/#8 allowlist + hints            (parallel to 2C)
  │
  ▼
Phase 3   ──▶  Media3 migration                   (future release)
```

---

## Bug Summary by Priority

| Priority | Bug | Description | Phase | Risk |
|---|---|---|---|---|
| P0 | #1 | `startForeground` crash on empty snapshot | 2A | 🟠 |
| P0 | #2 | FGS start from background without catch | 1A | 🟢 |
| P0 | #9 | No audio focus (ducking/pause) | 1C | 🟠 |
| P0 | #10 | No reaction to BT disconnect | 1B | 🟡 |
| P1 | #4 | Play from AA with UI closed does nothing | 2C | 🟠 |
| P1 | #5/#11 | Three independent MediaSessions, ambiguous BT routing | 2B | 🟠 |
| P1 | #12 | Watchdog timeout 1s too aggressive | 0 | 🟢 |
| P1 | #13 | Unused storage permissions | 0 | 🟢 |
| P2 | #6 | Deprecated MediaSession API (→ Media3) | Phase 3 | 🔴 |
| P2 | #7 | AA caller allowlist too restrictive | 2D | 🟡 |
| P2 | #8 | No content style hints for AA | 2D | 🟢 |
| P2 | #14 | Thread-safety `_reconnectCts` | 1D | 🟠 |
| P2 | #15 | README says API 21 instead of 26 | 0 | 🟢 |

---

## Final Notes

- **AA Certification:** even after phases 0–2, Android Auto will work **exclusively in developer mode** with "Unknown sources" enabled. Production distribution requires Google Play publication + review in the Android for Cars program (media category).
- **Physical device:** fixes #9 and #10 can be implemented now but require real hardware for full validation (ducking on calls, physical BT disconnect).
- **Current build:** debug build unsigned with production key — an additional reason why AA does not appear on standard user devices.
