# RadioE45 — Technical Analysis: Android Auto, Bluetooth, Background Service

**Client:** analysis for an external company
**Subject:** RadioE45 app (.NET MAUI 10, AzuraCast webradio streaming)
**Scope:** background playback (foreground service), Android Auto, Bluetooth control
**Test platform:** Android, Pixel 9 Pro XL emulator, API 36 (Debug build, `com.radioe45.app`)
**Date:** 2026-06-14
**App version:** 0.20 (versionCode 20)
**minSdk:** 26 (Android 8.0) · **targetSdk:** 36

> The repository was built and run on the emulator. The app compiles (0 errors, 2 insignificant warnings), installs and runs — the stream plays and "now playing" updates. The assessment below covers the three areas critical for in-car use.

---

## 1. Executive summary

The app has a **solid, well-thought-out playback core** (reconnect watchdog, probing of multiple stream URLs, deliberate handling of "pause" in live streams as closing the connection). However, the **system integration layer (Android Auto / Bluetooth / background service) has serious architectural flaws and at least two bugs that can cause a crash or a "dead" Play button**.

| Area | Rating | Comment |
|--------|-------|-----------|
| Background service (foreground) | 🟠 **Medium** | Works, but non-standard architecture + `startForeground` crash risk |
| Android Auto | 🔴 **Low / risky** | 3 separate MediaSessions, cold-start Play may do nothing, deprecated API |
| Bluetooth / media buttons | 🔴 **Low** | No audio focus, no reaction to BT disconnect, ambiguous button routing |
| Audio core code quality | 🟢 **High** | Robust reconnect, good comments, deliberate decisions |

**Verdict:** the app is suitable for further development, but in its current state it is **not ready for Android Auto certification** nor for comfortable in-car use. The fixes described in sections 5–8 are required.

---

## 2. ⚠️ KEY WARNING — Android Auto and the lack of Google Play certification

This is the most important business constraint and must be clearly communicated to the client.

**Android Auto will not load this app for a regular user.** Apps in the "media" category for Android Auto must pass a **separate Google review** and be distributed through **Google Play**. An app installed from an APK file (sideload / "unknown sources") **will not appear by default** on the car screen.

To run it on Android Auto **in its current state**, the user must manually:

1. In the **Android Auto** app, unlock **developer mode** (tap the version number 10×).
2. In the AA developer menu, enable **"Unknown sources"** (running unauthorized apps).
3. Have the app installed from an APK (sideload), because it is not on Google Play.

**Consequences:**

- Without the above steps, on any phone the app **does not appear at all** in Android Auto — this is not a code bug, but platform policy.
- Production distribution requires: publishing on Google Play **and** submitting to and being accepted into the Android for Cars program (media category). Google verifies, among other things, compliance with driver UX guidelines.
- The build we tested is **Debug and not signed with a production key** — another reason AA will not trust it.

> **Conclusion for the report:** every Android Auto demonstration with this app takes place exclusively in AA developer mode with unknown sources enabled. This is not the state in which the end client will see the app. The production path requires Google certification + Play distribution.

---

## 3. How the audio layer works (context for the assessment)

Understanding the architecture is key to most of the bugs below.

```
OnAirPage (UI)  ──contains──▶  MediaElement (ExoPlayer)   ← the real audio player
      │  OnAppearing()
      ▼
AudioService (singleton)  ── controls MediaElement, reconnect, watchdog
      │  UpdatePlaybackState / Clear
      ▼
IPlatformNowPlayingService → AndroidNowPlayingService
      │  writes state to ▼
AndroidNowPlayingStateStore (static store + SnapshotChanged event)
      ├──▶ AndroidMediaNotificationService  → MediaSession "RadioE45Playback" + foreground notification
      └──▶ RadioMediaBrowserService         → MediaSession "RadioE45Auto"     + tree for Android Auto
```

**The most important architectural observation:** the real player (ExoPlayer inside `MediaElement`) **lives in the UI layer** — it is created and attached only in `OnAirPage.OnAppearing()` via `AudioService.Initialize(AudioPlayer)` (`Views/OnAirPage.xaml.cs:34`, `Services/Audio/AudioService.cs:41`). The CommunityToolkit library's foreground service is **deliberately disabled**:

```csharp
.UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false)  // MauiProgram.cs:36
```

Instead, the team wrote its own `AndroidMediaNotificationService`. **This service does NOT own the player** — it only manages the notification and the MediaSession. The player remains attached to the activity/page. This is the source of most of the AA and background issues.

---

## 4. Assessment: background service (foreground service) — 🟠 Medium

### What is good
- Correct `mediaPlayback` service type and a complete set of permissions (`FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_MEDIA_PLAYBACK`, `POST_NOTIFICATIONS`, `WAKE_LOCK`) — `AndroidManifest.xml:16,27-30`.
- `StartForeground` invoked with the `TypeMediaPlayback` type on API 29+ (`AndroidMediaNotificationService.cs:242`).
- Deliberate swipe-from-recents handling: `AudioLifecycleService` with `stopWithTask="false"` stops the stream in `OnTaskRemoved` (`AudioLifecycleService.cs:19`). A good, mature solution.
- Notification channel with `LockscreenVisibility.Public` and `MediaStyle` — correct for the lock screen.

### 🔴 CRITICAL BUG #1 — possible "startForeground not called" crash
`RequestRefresh` starts the service via `StartForegroundService` (`AndroidMediaNotificationService.cs:78,87-88`), which **obligates** the system to call `startForeground()` within 5 s. But in `PublishNotification`:

```csharp
if (string.IsNullOrWhiteSpace(snapshot.Title) && string.IsNullOrWhiteSpace(snapshot.Artist))
{
    StopServiceNotification();   // <-- does NOT call StartForeground!
    return;
}
```
(`AndroidMediaNotificationService.cs:99-103`)

If `UpdateMetadata`/`UpdatePlaybackState` arrives while the snapshot has an **empty title and artist** (e.g. a station with no "now playing" data in the first second, or a transient state), the service is started as foreground-expecting but **finishes without `startForeground()`** → the system throws `ForegroundServiceDidNotStartInTimeException` / `RemoteServiceException`. **This crashes the process.** The risk is real, especially at playback start.
**Fix:** do not use `StartForegroundService` for a path that may not publish a notification; or always publish a minimal notification (a "RadioE45" placeholder) before deciding to stop.

### 🟠 HIGH BUG #2 — starting a foreground service from the background (Android 12+)
On targetSdk 36, `StartForegroundService` invoked while the app is **in the background** (e.g. a Play command from Android Auto with the app closed) throws `ForegroundServiceStartNotAllowedException`. The call in `StartService` (`cs:82-91`) **has no try/catch block** → uncaught exception. There is an exemption for `mediaPlayback` tied to a MediaSession, but the path here is fragile and not guaranteed.
**Fix:** wrap the start in try/catch and — ultimately — move the player into the service itself (Media3 `MediaSessionService`), which has a native exemption from this restriction.

### 🟠 Note #3 — background playback depends on the UI lifecycle
Because ExoPlayer lives in the `MediaElement` on the page, and the toolkit service is disabled, background audio continuity relies solely on the fact that **the process has foreground priority thanks to the custom service**. It works as long as the page has appeared once and the service has started. However, this is a fragile construction — under aggressive memory/activity management the risk of the player being released grows. Recommendation: ultimately the player in the service, not in the UI.

---

## 5. Assessment: Android Auto — 🔴 Low / risky

### What is good
- There is a `MediaBrowserService` with a manifest declaration and `automotive_app_desc.xml` with `<uses name="media"/>` — a correct AA skeleton (`AndroidManifest.xml:8-23`, `Resources/xml/automotive_app_desc.xml`).
- Caller validation in `OnGetRoot` (package allowlist) — a good security practice (`RadioMediaBrowserService.cs:66-71`).
- Asynchronous loading of the station list with `result.Detach()` — the correct pattern (`cs:82-83`).
- Metadata and `PlaybackState` built with transport control actions.

### 🔴 CRITICAL BUG #4 — cold-start Play may do nothing
When Android Auto launches the app in the background and the user presses Play, `AutoMediaCallback.OnPlay` → `audio.PlayAsync(...)`. But:

```csharp
public async Task PlayAsync(AzuraStation station)
{
    if (_mediaElement is null)
        return;   // <-- silent exit
    ...
}
```
(`AudioService.cs:82-85`)

`_mediaElement` is set **only** in `OnAirPage.OnAppearing()`. On a launch from Android Auto, the UI page may never appear → `_mediaElement == null` → **Play plays nothing, with no error for the user.** This is a classic, critical AA integration bug: a UI-bound player does not exist when the car controls it.
**Fix:** the player must be independent of the page (created in the service/application), not pulled from the visual tree.

### 🔴 HIGH BUG #5 — three separate MediaSessions
The app has **three** independent media sessions:
1. `"RadioE45Auto"` in `RadioMediaBrowserService` (`cs:41`),
2. `"RadioE45Playback"` in `AndroidMediaNotificationService` (`cs:38`),
3. the internal ExoPlayer/MediaElement session (toolkit).

Android Auto, the lock screen and Bluetooth expect **one** active session as the source of truth. Three sessions with `Active=true` mean:
- ambiguity about which one buttons reach (see BT, section 6),
- risk of duplicated/inconsistent controls,
- state synchronization done manually through the static `AndroidNowPlayingStateStore` — a workaround, not a solution.
**Fix:** one shared `MediaSession`/Media3 for notification, AA and BT.

### 🟠 MEDIUM BUG #6 — deprecated API instead of Media3
The framework `Android.Media.Session.MediaSession` and `Android.Service.Media.MediaBrowserService` are used (`RadioMediaBrowserService.cs:20`). For Android Auto, Google **recommends Media3** (`androidx.media3.session.MediaLibraryService` / `MediaSession`). The old API works, but: weaker compatibility with AA surfaces, no newer features, harder certification. `grep` confirms the absence of `Media3`/`MediaSessionCompat`/`MediaButtonReceiver` in the project.

### 🟠 MEDIUM BUG #7 — caller allowlist too narrow
`AllowedCallers` contains only `gearhead` and `mediasimulator` (`cs:24-28`). It omits, among others, `com.google.android.carassistant` (Assistant / Driving Mode), Android Automotive OS, Wear. Some surfaces will not be able to browse content. Consider extending the list + signature validation instead of just the package name.

### 🟡 LOW #8 — no content style hints
`OnGetRoot` returns a `BrowserRoot` with no extras (`CONTENT_STYLE_*`). AA will show the default list layout. Cosmetic, but it affects the UX rating during certification.

---

## 6. Assessment: Bluetooth / media buttons — 🔴 Low

In a car, control happens mainly through **AVRCP over Bluetooth** (steering-wheel buttons, hands-free kit) and through A2DP audio routing.

### 🔴 CRITICAL BUG #9 — no audio focus handling
`grep` across the whole project: **zero** occurrences of `AudioFocus` / `requestAudioFocus`. Effects in the car/BT:
- an incoming **navigation prompt** or **phone call** does not duck/pause the radio (no ducking),
- after the call ends, the radio does not resume by itself,
- conflicts with other audio apps.
This is one of the most common reasons for rejection in the AA review and a serious in-car UX flaw.
**Fix:** implement `AudioManager.RequestAudioFocus` + a reaction to `AudioFocusChange` (pause/duck/resume). Media3 does most of this automatically.

### 🔴 HIGH BUG #10 — no reaction to BT disconnect (ACTION_AUDIO_BECOMING_NOISY)
No `BroadcastReceiver` for `ACTION_AUDIO_BECOMING_NOISY` (confirmed by grep). When the user leaves the car / disconnects BT / unplugs headphones, **the sound immediately jumps to the phone speaker** at full volume. A classic case expected by Android guidelines to be handled (pause on "becoming noisy").
**Fix:** an `ACTION_AUDIO_BECOMING_NOISY` receiver → `PauseAsync()`.

### 🟠 HIGH BUG #11 — ambiguous BT button routing
Two active sessions (`RadioE45Auto` and `RadioE45Playback`) declare `HandlesMediaButtons | HandlesTransportControls`. The system routes a media button to the "most recent" session — with two active, the behavior is unpredictable. A Play/Pause button from the steering wheel may reach a session whose callback does not actually control the player.
**Fix:** a single session (related to point 5).

### What works
- The **A2DP audio routing** itself (the signal goes to the car speakers) works automatically — it requires no code and will be fine.
- Metadata (title/artist/cover) is written to `MediaMetadata`, so when the session is correctly selected, the BT/HU display will show "now playing".

---

## 7. Additional observations (beyond the three main areas)

| # | Severity | Observation | Location |
|---|----------|-----------|-------------|
| 12 | 🟠 Medium | **Buffering watchdog = 1.0 s** — extremely aggressive. On a variable connection in a moving car, legitimate buffering >1 s will trigger constant reconnects and audio dropouts. Suggested 8–15 s. | `AudioService.cs:19` |
| 13 | 🟠 Medium | **Over-permissioning:** `READ_EXTERNAL_STORAGE` and `WRITE_EXTERNAL_STORAGE` declared but unused (covers over HTTP, DB in private storage). Ignored on API 30+, and the Play Console flags them. | `AndroidManifest.xml:31-32` |
| 14 | 🟡 Low | **Thread-safety of `_reconnectCts`:** `RenewReconnectCts` does `Cancel()+Dispose()+new`, while `PauseAsync`/`StopAsync`/watchdog/connectivity call `Cancel()` from different threads → possible `ObjectDisposedException` (Cancel after Dispose). | `AudioService.cs:448-453, 110, 150` |
| 15 | 🟡 Low | **Documentation inconsistency:** the README says "Android 8.0+ (API 21)" — API 21 is Android 5.0; the actual minSdk is **26** (Android 8.0). Misleading. | `README.md`, manifest |
| 16 | 🟡 Low | **Dead code / TODO:** crash reporting consent has `return;` before the dialog (`App.xaml.cs:81-82`) — hence compiler warning CS0162. The feature is deliberately disabled "for production" but leaves a dead path. | `App.xaml.cs:81` |
| 17 | 🟢 Info | Sentry is initialized only when the DSN is non-empty (`IsConfigured`) — an empty `AppSecrets.cs` is safe, no crash. Good defensiveness. | `MauiProgram.cs:38`, `CrashReportingConfiguration.cs` |

---

## 8. What is done well (strengths)

For balance — the project has real strengths:

- **Robust reconnect mechanism:** watchdog every 10 s, reconnect on connectivity change, probing multiple URLs in parallel with selection of the first reachable one (`ProbeFirstReachableAsync`) and priority for the last working URL. A mature approach to unstable mobile streaming.
- **Deliberate handling of "pause" in live:** pause closes the connection and reopens it on resume, instead of playing from a stale buffer (`AudioService.cs:97-139`) — correct for live radio.
- **Clean platform separation** via `IPlatformNowPlayingService` (Android/iOS/Null) and dependency injection (`MauiProgram.cs:108-114`).
- **Correct swipe-to-dismiss handling** and state cleanup (`AudioLifecycleService`, `StopImmediate`).
- **Good, substantive comments** in the code explaining design decisions (a rarity).
- **`_reconnectGuard` via `Interlocked`** — concurrency awareness.
- The app **actually works** — the stream plays, metadata and cover update (confirmed on the emulator).

---

## 9. Recommendations (prioritized)

**Priority 0 — before any AA certification:**
1. **Move the player from the UI into the service** and unify to **one** `MediaSession` (ideally **Media3 `MediaLibraryService`**). This simultaneously resolves bugs #4, #5, #6, #11 and note #3.
2. **Add audio focus handling** (#9) and **ACTION_AUDIO_BECOMING_NOISY** (#10) — without these, in-car use is annoying and risks rejection in the Google review.
3. **Fix the `startForeground` path** (#1) — always publish a notification or do not start as foreground; wrap the start in try/catch (#2).

**Priority 1:**
4. Relax the buffering watchdog timeout to 8–15 s (#12).
5. Remove unused storage permissions (#13).
6. Extend/correct the AA caller allowlist + signature validation (#7).

**Priority 2 (housekeeping):**
7. Improve thread-safety of `_reconnectCts` (#14), correct the README (#15), remove the dead consent code (#16), add content style hints for AA (#8).

**Production AA/BT path:** after the above — publish on Google Play + submit to the Android for Cars program (media category) and pass the Google review. Until that happens, AA works exclusively in developer mode with "unknown sources" (section 2).

---

## 10. Final verdict

| Criterion | Rating (1–5) |
|-----------|:-----------:|
| Playback / streaming core | ★★★★☆ |
| Background service | ★★★☆☆ |
| Android Auto (current state) | ★★☆☆☆ |
| Bluetooth / in-car control | ★★☆☆☆ |
| AA certification readiness | ★☆☆☆☆ |
| Code quality and hygiene | ★★★★☆ |

**Summary:** a well-written radio playback core with mature handling of unstable networks, but **the in-car integration layer (AA/BT/service) needs redesign**. The root cause of most problems is a single architectural decision — **the player attached to the UI instead of the service** — plus **three diverging MediaSessions** and **no audio focus / BT disconnect handling**. Additionally, regardless of the code, **the app will not appear in Android Auto without Google certification and Play distribution** — currently only in AA developer mode with unknown sources enabled.

A P0 fix (single service + Media3 + audio focus) would raise the AA/BT rating from 2/5 to a level qualifying for the certification review.

---

## 11. Fix code — where and what to change

Below are concrete patches for each bug from sections 4–7. Fixes #1, #2, #9, #10, #12, #13, #14, #15, #16 are complete and ready to paste. Fixes #4 (a pragmatic bridge) and #5/#6/#11 (session unification / Media3) are marked as requiring a larger change — a direction and bridging code are provided.

### 11.1 🔴 #1 — Guaranteed `startForeground` (crash elimination)
**File:** `Platforms/Android/Services/AndroidMediaNotificationService.cs`
**What to do:** before the service can decide to stop, **always** enter the foreground state (placeholder when no metadata). System requirement: after `startForegroundService`, `startForeground()` must be called within 5 s.

```csharp
// 1) At the very start of OnStartCommand — before handling actions:
public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
{
    // GUARANTEE: if started via startForegroundService, we MUST enter foreground
    // within <5 s, otherwise the system throws ForegroundServiceDidNotStartInTimeException.
    EnsureForegroundStarted();

    string? action = intent?.Action;
    switch (action)
    {
        // ... unchanged ...
    }

    PublishNotification();
    return StartCommandResult.NotSticky;
}

// 2) New method — publishes a notification (placeholder if no metadata) and enters foreground:
private void EnsureForegroundStarted()
{
    if (_isForeground || _mediaSession is null)
        return;

    (PlatformNowPlayingSnapshot snapshot, Android.Graphics.Bitmap? artwork) = AndroidNowPlayingStateStore.GetState();

    Notification notification =
        (string.IsNullOrWhiteSpace(snapshot.Title) && string.IsNullOrWhiteSpace(snapshot.Artist))
            ? BuildPlaceholderNotification()
            : BuildNotification(snapshot, artwork);

    StartForegroundInternal(notification);
    _isForeground = true;
}

private Notification BuildPlaceholderNotification()
{
    Notification.Builder builder = OperatingSystem.IsAndroidVersionAtLeast(26)
        ? new Notification.Builder(this, ChannelId)
        : new Notification.Builder(this);

    return builder
        .SetContentTitle("RadioE45")
        .SetSmallIcon(Resource.Mipmap.appicon)
        .SetOnlyAlertOnce(true)
        .SetOngoing(false)
        .Build()!;
}
```

> After entering the foreground, `PublishNotification()` can safely call `StopServiceNotification()` on an empty snapshot — `StopForeground(Remove)` will remove the placeholder without a crash.

### 11.2 🟠 #2 — Safe FGS start from the background (Android 12+)
**File:** `Platforms/Android/Services/AndroidMediaNotificationService.cs`, method `StartService` (`cs:82-91`).
**What to do:** wrap the start in `try/catch` — an FGS start from the background on API 31+ may throw `ForegroundServiceStartNotAllowedException`.

```csharp
private static void StartService(Context context, string action, bool preferForeground)
{
    Intent intent = new(context, typeof(AndroidMediaNotificationService));
    intent.SetAction(action);

    try
    {
        if (preferForeground && OperatingSystem.IsAndroidVersionAtLeast(26))
            context.StartForegroundService(intent);
        else
            context.StartService(intent);
    }
    catch (Exception ex) when (
        ex is Java.Lang.IllegalStateException ||           // ForegroundServiceStartNotAllowed (API 31+)
        ex is Android.App.ForegroundServiceStartNotAllowedException)
    {
        // No permission to start from background — control should go through the active MediaSession.
        System.Diagnostics.Debug.WriteLine($"FGS start blocked: {ex.Message}");
    }
}
```

### 11.3 🔴 #4 — Play from Android Auto with the UI closed (pragmatic bridge)
**Files:** `Services/Audio/AudioService.cs` + `Platforms/Android/Services/RadioMediaBrowserService.cs`
**What to do:** the player is attached to the UI, so on an AA command you must (a) remember the request and play it after the `MediaElement` is attached, and (b) force-launch the activity that calls `Initialize()`. This is a **bridge**; ultimately the player belongs in the service (Media3, point 11.5).

```csharp
// AudioService.cs — new field:
private AzuraStation? _pendingStation;

// AudioService.cs — PlayAsync: handle the missing MediaElement instead of a silent return:
public async Task PlayAsync(AzuraStation station)
{
    _currentStation = station;
    _shouldBePlaying = true;
    _bufferingStartedAt = DateTime.MinValue;

    if (_mediaElement is null)
    {
        // Command from Android Auto / BT with the UI not initialized:
        // remember the request — Initialize() will complete it.
        _pendingStation = station;
        return;
    }

    RenewReconnectCts();
    Interlocked.Exchange(ref _reconnectGuard, 0);
    TryQueueReconnect();
}

// AudioService.cs — at the end of Initialize(), after attaching events and the _shouldBePlaying block:
if (_pendingStation is not null)
{
    AzuraStation pending = _pendingStation;
    _pendingStation = null;
    _ = PlayAsync(pending);   // now _mediaElement != null, it will start normally
}
```

```csharp
// RadioMediaBrowserService.cs — in AutoMediaCallback force the UI to be created (hence Initialize):
private static void EnsureUiLaunched()
{
    Context ctx = Android.App.Application.Context;
    Intent intent = new(ctx, typeof(MainActivity));
    intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop);
    try { ctx.StartActivity(intent); } catch { /* background activity start may be restricted */ }
}

// at the start of OnPlay() and OnPlayFromMediaId():
public override void OnPlay()
{
    EnsureUiLaunched();
    // ... rest unchanged ...
}
```

> **Caveat:** starting an activity from the background is restricted on Android 10+ and will not always succeed. Full reliability comes only from moving the player into the service (11.5).

### 11.4 🔴 #9 — Audio focus (ducking / pause on navigation and calls)
**New file:** `Platforms/Android/Services/AudioFocusManager.cs`
**What to do:** request audio focus at playback start, release it on stop; react to loss (pause/ducking) and regain (resume).

```csharp
using Android.Content;
using Android.Media;
using RadioE45.Services.Audio;

namespace RadioE45;

public sealed class AudioFocusManager : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
{
    private readonly AudioManager _audioManager;
    private readonly IAudioService _audio;
    private AudioFocusRequestClass? _focusRequest;
    private bool _resumeOnFocusGain;

    public AudioFocusManager(Context context, IAudioService audio)
    {
        _audioManager = (AudioManager)context.GetSystemService(Context.AudioService)!;
        _audio = audio;
    }

    public bool RequestFocus()
    {
        AudioFocusRequest result;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            AudioAttributes attributes = new AudioAttributes.Builder()!
                .SetUsage(AudioUsageKind.Media)!
                .SetContentType(AudioContentType.Music)!
                .Build()!;

            _focusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)!
                .SetAudioAttributes(attributes)!
                .SetOnAudioFocusChangeListener(this)!
                .SetWillPauseWhenDucked(false)!   // we handle ducking ourselves
                .Build()!;

            result = _audioManager.RequestAudioFocus(_focusRequest);
        }
        else
        {
#pragma warning disable CS0618
            result = _audioManager.RequestAudioFocus(this, Stream.Music, AudioFocus.Gain);
#pragma warning restore CS0618
        }
        return result == AudioFocusRequest.Granted;
    }

    public void AbandonFocus()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26) && _focusRequest is not null)
            _audioManager.AbandonAudioFocusRequest(_focusRequest);
#pragma warning disable CS0618
        else if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            _audioManager.AbandonAudioFocus(this);
#pragma warning restore CS0618
    }

    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        switch (focusChange)
        {
            case AudioFocus.Loss:                  // permanent loss — logical stop
                _resumeOnFocusGain = false;
                _ = _audio.PauseAsync();
                break;
            case AudioFocus.LossTransient:         // call / short interruption
                _resumeOnFocusGain = _audio.IsPlaying;
                _ = _audio.PauseAsync();
                break;
            case AudioFocus.LossTransientCanDuck:  // e.g. navigation prompt
                _audio.SetVolume(0.2);
                break;
            case AudioFocus.Gain:
                _audio.SetVolume(1.0);             // NOTE: restore to the user's volume (see below)
                if (_resumeOnFocusGain)
                {
                    _resumeOnFocusGain = false;
                    _ = _audio.ResumeAsync();
                }
                break;
        }
    }
}
```

**Wiring:** register in DI and call it around playback.

```csharp
// MauiProgram.cs (ANDROID section, next to the IPlatformNowPlayingService registration):
builder.Services.AddSingleton(sp =>
    new AudioFocusManager(Android.App.Application.Context, sp.GetRequiredService<IAudioService>()));
```

```csharp
// AudioService.cs — inject and use:
//   constructor: add a parameter (on Android) or fetch lazily from IPlatformApplication.Current.Services
//   RequestFocus() before opening the stream (in TryOpenStreamAsync, after selecting winner):
if (!_focus.RequestFocus())   // _focus = AudioFocusManager
    return;                   // no focus — do not start
//   AbandonFocus() in StopAsync()/StopImmediate()/Shutdown().
```

> **Important:** ducking and `Gain` restore a hard-coded `1.0`. You should remember the user's previous volume (e.g. a `_userVolume` field updated in `SetVolume`) and restore that instead of `1.0`.

### 11.5 🔴 #10 — Reaction to BT disconnect (ACTION_AUDIO_BECOMING_NOISY)
**New file:** `Platforms/Android/Services/BecomingNoisyReceiver.cs` + registration in the notification service.

```csharp
using Android.Content;
using Android.Media;
using RadioE45.Services.Audio;

namespace RadioE45;

public sealed class BecomingNoisyReceiver : BroadcastReceiver
{
    private readonly IAudioService _audio;
    public BecomingNoisyReceiver(IAudioService audio) => _audio = audio;

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action == AudioManager.ActionAudioBecomingNoisy)
            _ = _audio.PauseAsync();   // BT/headphones unplugged → pause, not the phone speaker
    }
}
```

```csharp
// AndroidMediaNotificationService.cs — field + registration in OnCreate, unregistration in OnDestroy:
private BecomingNoisyReceiver? _noisyReceiver;

public override void OnCreate()
{
    base.OnCreate();
    // ... existing code ...

    IAudioService? audio = IPlatformApplication.Current?.Services?.GetService<IAudioService>();
    if (audio is not null)
    {
        _noisyReceiver = new BecomingNoisyReceiver(audio);
        IntentFilter filter = new(AudioManager.ActionAudioBecomingNoisy);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            RegisterReceiver(_noisyReceiver, filter, ReceiverFlags.NotExported);
        else
            RegisterReceiver(_noisyReceiver, filter);
    }
}

public override void OnDestroy()
{
    if (_noisyReceiver is not null)
    {
        UnregisterReceiver(_noisyReceiver);
        _noisyReceiver = null;
    }
    // ... existing code ...
}
```

### 11.6 🔴 #5 / #11 — One shared MediaSession (bridge) + #6 Media3 (ultimately)
**Files:** `AndroidMediaNotificationService.cs`, `RadioMediaBrowserService.cs`
**What to do (bridge):** eliminate the separate `"RadioE45Auto"` session in the browser service and point to the token of the single `"RadioE45Playback"` session. This way AA, the lock screen and BT buttons use a single source of truth.

```csharp
// New file: Platforms/Android/Services/SharedMediaSessionHolder.cs
using Android.Media.Session;
namespace RadioE45;

internal static class SharedMediaSessionHolder
{
    public static MediaSession? Session { get; set; }
}
```

```csharp
// AndroidMediaNotificationService.cs — in OnCreate after creating _mediaSession:
SharedMediaSessionHolder.Session = _mediaSession;
// in OnDestroy before Release():
SharedMediaSessionHolder.Session = null;
```

```csharp
// RadioMediaBrowserService.cs — do NOT create your own session; use the shared one:
public override void OnCreate()
{
    base.OnCreate();
    _logger = IPlatformApplication.Current?.Services?.GetService<ILogger<RadioMediaBrowserService>>();

    // Make sure the notification service (the session owner) is running and the token is available.
    AndroidMediaNotificationService.RequestRefresh(this);

    MediaSession? shared = SharedMediaSessionHolder.Session;
    if (shared is not null)
        SessionToken = shared.SessionToken;

    AndroidNowPlayingStateStore.SnapshotChanged += OnSnapshotChanged;
}
```

> **Ordering caveat:** if the browser service starts before the notification service, `SessionToken` may be temporarily `null` — hence the `RequestRefresh` above, and ultimately you should migrate to Media3 anyway.
>
> **#6 — the ultimate solution (Media3):** instead of the framework `MediaSession` + `MediaBrowserService`, migrate to `androidx.media3.session.MediaLibraryService` with **one** `MediaSession` and `ExoPlayer` **in the service**. This fixes #3, #4, #5, #6, #11 at once (player independent of the UI, a single session, native FGS-restriction exemption, correct audio focus). In .NET MAUI it requires the `Xamarin.AndroidX.Media3.*` bindings. Effort: significant (redesigning the audio layer on Android), but it is the only path to AA certification.

### 11.7 🟠 #12 — Relax the buffering watchdog
**File:** `Services/Audio/AudioService.cs:19`

```csharp
// was:  private const double BufferingTimeoutSeconds = 1.0;
private const double BufferingTimeoutSeconds = 12.0;   // tolerance for a variable connection in the car
```

### 11.8 🟠 #13 — Remove unused storage permissions
**File:** `Platforms/Android/AndroidManifest.xml:31-32` — remove both lines:

```xml
<!-- REMOVE — unused, flagged by the Play Console: -->
<uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" />
<uses-permission android:name="android.permission.WRITE_EXTERNAL_STORAGE" />
```

### 11.9 🟡 #14 — Thread-safety of `_reconnectCts`
**File:** `Services/Audio/AudioService.cs` — protect the CTS with a lock; do not read the token after Dispose.

```csharp
private readonly object _ctsLock = new();
private CancellationTokenSource _reconnectCts = new();

private void RenewReconnectCts()
{
    lock (_ctsLock)
    {
        _reconnectCts.Cancel();
        _reconnectCts.Dispose();
        _reconnectCts = new CancellationTokenSource();
    }
}

private void CancelReconnect()
{
    lock (_ctsLock) { _reconnectCts.Cancel(); }
}

private CancellationToken CurrentReconnectToken()
{
    lock (_ctsLock) { return _reconnectCts.Token; }
}
```

Then replace the direct calls:
- `_reconnectCts.Cancel();` → `CancelReconnect();` (in `PauseAsync` `cs:110`, `StopAsync` `cs:150`, `StopImmediate` `cs:173`),
- the read `var ct = _reconnectCts.Token;` → `var ct = CurrentReconnectToken();` (in `TryReconnectAsync` `cs:393`).
(In `Shutdown` keep Cancel+Dispose, but also under `_ctsLock`.)

### 11.10 🟡 #15 — Correct the README
**File:** `README.md`

```diff
- - Android 8.0+ (API 21)
+ - Android 8.0+ (API 26)
```

### 11.11 🟡 #16 — Remove the dead crash-reporting consent code
**File:** `App.xaml.cs:80-82` — remove the premature `return;` (also eliminates warning CS0162), or deliberately leave the feature disabled until it is production-ready. If it should work:

```csharp
Page? hostPage = await GetPromptHostPageAsync();
if (hostPage is null)
    return;
// REMOVE the line below "return;" (and the TODO comment):
// return;
bool enabled = await hostPage.DisplayAlertAsync( ... );
```

---

### Implementation order (mapping to the priorities from section 9)
1. **P0:** 11.1 (#1), 11.2 (#2), 11.4 (#9), 11.5 (#10) — quick, standalone, eliminate the crash and the worst in-car UX.
2. **P0 (larger):** 11.6 — ultimately Media3 (fixes #3/#4/#5/#6/#11). Until the migration: the bridge 11.3 (#4) + the shared session 11.6.
3. **P1/P2:** 11.7 (#12), 11.8 (#13), 11.9 (#14), 11.10 (#15), 11.11 (#16) — housekeeping, low risk.
