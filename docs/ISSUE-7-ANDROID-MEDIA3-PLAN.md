# Issue #7 — Android / Android Auto action plan

Issue [#7](https://github.com/delfuria/RadioE45/issues/7) is a map of hypotheses, not a defect list. Every point below was compared with the current source. Nothing is scheduled that is already implemented or deliberately chosen.

Ownership split:

- **Maintainer (us):** everything that does not need Android Auto. Phone and Android TV emulators, the Sony TV, Mac builds, release artifacts, Play Console.
- **Tomek (`toevi`):** everything that needs Android Auto, on the Desktop Head Unit (DHU) with a physical phone, or on an Android emulator driving the DHU.

The Android implementation has one `MediaLibraryService`, one Media3 session, one player. Phone, notification, Bluetooth, Android Auto and Android TV share it. Do not add a second player/session and do not attach the legacy `AudioFocusManager` to Media3.

## Execution status

| Step | Status | Notes |
|---|---|---|
| M1 — fonts (A) | **Done** (committed `9f9fbe4`) | The v0.50 GitHub APK has no font (0/4); v0.49 APK and the v0.50 AAB have all four, so Play is not affected. Reproduced in a throwaway copy: with `mauifont.stamp` surviving and the rest of `obj/Release/net10.0-android` gone, the build exits 0 and the APK has no font. `dotnet clean -f net10.0-android -c Release` alone does not reproduce it (it removes the stamp too), so the exact trigger of the v0.50 APK is unproven. Fix: `DropStaleMauiFontStamp` and `VerifyMauiFontsPresent` in `RadioE45.csproj` (Android only; verified 4/4 fonts on the stale state), plus a font check next to the ABI check in `build-android.sh` and `build-store-android.sh` (outside the repo). Open: rebuild and re-upload the v0.50 APK, or wait for the next release. |
| M2 — B.1 retry | **Done** (committed `c46a170`; phone emulator and Sony TV verified, see "TV verification" below) | Reproduced on `Pixel_6_Pro_API_34` (airplane mode + wifi/data off for 45 s while playing): the session stayed in `ERROR (Source error)` 40 s after the network returned. Fix in `RadioPlaybackService`: after the last candidate URL fails, `ScheduleRecovery` re-prepares the station with a bounded backoff (3, 5, 10, 20 s, then 30 s, 20 attempts max) from the new `StreamRecoveryBackoff` (Core, pure policy, 5 unit tests). A generation token cancels it on `LivePlayer.Pause/Stop/Play`, `playWhenReady=false`, a different media item, task removal, service destroy and `STATE_READY`. Verified on the emulator: recovers on its own after the network returns; a media-key pause during the gap cancels the pending retry and nothing restarts; skipping to the next station during the gap does not revive the previous station. Core tests 293/293. Sony TV verified by the maintainer (see "TV verification"). Not run: the `Television_1080p` emulator regression and a real cellular gap (optional). Pending for Tomek: DHU confirmation (T1). Known: the controller still publishes the error immediately, so the app UI may show a red error while a retry is pending (see C.4 in M3). |
| M3 — measurements (B.2, B.3, B.5, B.6, C.4) | **Done** (B.6 fix committed `f92ff5b`; measured on the phone emulator, B.6 also re-checked on the Sony TV, see below) | **B.2** not reproduced: the session stayed `PLAYING` through a real track change (1 s sampling, 16:43:42), no `BUFFERING`. No code change. **B.3** media-key Play after a swipe-away does nothing (no process, no session): expected with the deliberate task-removal policy; the system-kill/process-recreation case cannot be simulated on the Play Store image (no root), so it stays a product decision. **B.5** not reproduced on the direct MP3 stream: the session advertises no `SEEK_TO`, `REWIND` or `FAST_FORWARD`; HLS (opt-in) not measured, it may expose seeking. **B.6** reproduced: a 35 s simulated call puts the session in `PAUSED` and playback resumes with no live-edge reconnect (the transient focus loss sets a suppression reason, not `playWhenReady`). Fix: `PlayerListener.OnPlaybackSuppressionReasonChanged` reconnects to the live edge after more than 5 s of suppression, like the long explicit pause. Verified: a 25 s call logs the reconnect, a 1 s call does not. **C.4** not visible: at 22 s offline the app showed no red error. Assistant ducking and a real device call are left to Tomek (T5). |
| M4 — emoji to Font Awesome (C.2) | **Done, not committed** (phone emulator and Sony TV checked) | MAUI: `FaSolid` glyphs replace ⏮ ⏭ ⏸ ▶ ◀ 🔊 🔇 👥 📻 ☰ ⟳ in `OnAirPage`, `RadioListPage`, `PodcastEpisodesPage`, `SongRequestPage` and the `PlayButton` style; TV: the listener icon in `TvNowPlayingView`. Uno already used the same glyphs, so no port was needed. `parity/features.yaml` has the new `shell.control-icons` feature (all platforms `done`); `ParityCheck` exit 0; Android build 0 errors; On Air and Channels checked visually on the emulator. Left as they were: the ▲ ▼ history chevron on MAUI (not in the issue, Uno already uses Font Awesome). Icons on the other platforms were checked visually by the maintainer; the TV D-pad focus map (`tools/tv-focus-probe.sh`) was not run. |
| TV verification (M2, M3/B.6, M4) | **Done on the Sony, reported OK** | Debug build for `armeabi-v7a` (`-p:RuntimeIdentifier=android-arm`, 4 fonts and the 32-bit libraries present in the APK) installed on the Sony BRAVIA 4K VH21 (Android 12, 32-bit) with `adb install -r -t` on 2026-10-28, 1.0 GB free afterwards. The maintainer ran the checks by hand with the real remote and reported **everything OK**. The checklist given was: the listener icon on the TV On Air view; standby for more than 5 s then Play resumes on the live broadcast (B.6 path); remote media keys (play, pause, previous/next station); and a network gap of about a minute, after which the radio must restart on its own (B.1 path). The outcome of each check was not itemized. The maintainer also checked the new icons visually on the other platforms and reported them fine (platforms not itemized; the MAUI XAML and font are the same as Android, which was also seen on the emulator). **Not done:** `Television_1080p` emulator regression and the `tools/tv-focus-probe.sh` D-pad focus map. Note: `adb connect` to the Sony failed with "No route to host" until the adb server was restarted from the terminal (macOS "Local Network" permission). |
| M5 — release artifacts and R8 spike (D.1, D.3) | **Measured; obfuscation handed to Tomek (T8)** | **AAB v0.50** (`store-packages/RadioE45.aab`): DEX 24,439,864 bytes in 3 files (matches the issue's ~24.4 MB, above the 10 MB threshold); `arm64-v8a`, `armeabi-v7a`, `x86_64` with 151 libraries each; 0 of 151 `arm64-v8a` and `x86_64` libraries below 16 KB alignment; 4 fonts; manifest (from a Release build of the same sources): `targetSdk 36`, `minSdk 26`, `RadioPlaybackService` with `foregroundServiceType=mediaPlayback` and the `MediaLibraryService` intent filter, `allowBackup="true"`. **Play rule** (Android vitals documentation, checked 2026-10-28): from February 2027 an app with more than 10 MB of DEX code needs at least 25% on each of optimization, shrinking and obfuscation. **R8 spike** (throwaway copy outside the repo, `-p:AndroidLinkTool=r8`): DEX falls to 13,441,960 bytes in 2 files (-45%), still above 10 MB, and `mapping.txt` shows only 30 of 13,791 classes renamed (0.2% obfuscation), because the SDK's generated `proguard_xamarin.cfg` always contains `-dontobfuscate`. A second build that replaced the rule list through `ProguardConfigFiles` without that line produced a DEX of 904 bytes (everything shrunk away): that attempt is invalid and was discarded. Consequence: shrink-only R8 meets shrinking and optimization but not obfuscation; turning obfuscation on needs a proper rule set (keep rules for the JNI-bound classes, plus `-keepclassmembers class androidx.media3.common.MediaItem { androidx.media3.common.MediaItem$RequestMetadata requestMetadata; }` for the voice-search reflection at `RadioPlaybackService.cs:792`). Voice search after R8 can only be verified on the DHU (T6). Not changed in the repo. The Play Console score, Data Safety and foreground-service warnings still have to be read in the console (not accessible from here). |
| M7 — SongRequestService tests (C.3, optional) | **Done, not committed** | New `RadioE45.Core.Tests/Services/Radio/SongRequestServiceTests.cs`, 15 cases through the real Refit client with a stub HTTP handler: the six known AzuraCast reasons map to their localized texts, the cooldown keeps the minutes from the server (`2.5`), an unknown message, an empty or non-JSON body, a null/blank `message` and a body without it all give the generic text, a successful call posts to `/api/station/{id}/request/{requestId}`, and a network failure is not mapped to a server message. Expected texts come from `LocalizationResourceManager` itself, so the tests do not depend on the UI language or change the shared culture. No production code changed; C.3 stays "no implementation needed". |
| M6 — Television_1080p regression and `tools/tv-focus-probe.sh` | **Done, 2026-10-28** | Debug build of the current sources on the `Television_1080p` emulator (`sdk_google_atv64_arm64`, API arm64). **Look:** the On Air view draws correctly with the Font Awesome listener icon. **Focus map** (`tools/tv-focus-probe.sh tour --expect "On Air,Request,Channels,Schedule"`): exit 0, all four labels reachable, plus play/pause, next station and the favorite heart. **Media keys:** `MEDIA_PLAY`, `MEDIA_PAUSE` and `MEDIA_NEXT` behave as expected. **B.1 on TV:** with wifi and data off for 50 s while playing the session goes `BUFFERING` then `ERROR`, the log shows `recovery #1…#3` (3, 5, 10 s), and playback returns by itself after the network is back. **Pause during the gap:** a `MEDIA_PAUSE` cancels the pending retry (no `recovery attempt` after the pause, the session stays stopped for 60 s) and `MEDIA_PLAY` then resumes. **`PLAY_PAUSE` finding:** on the emulator `KEYCODE_MEDIA_PLAY_PAUSE` pauses but a second press does not resume; an A/B build with the pre-session `RadioPlaybackService` shows the same, so it is not caused by this work. The maintainer confirmed that play/pause works normally on the Sony. B.6 (transient focus) cannot be exercised on this emulator (no telephony). **Test notes:** a clean reinstall wipes the app data, so the Terms dialog and the "add sample stations?" prompt appear before anything plays; `adb shell cmd connectivity airplane-mode` does not exist on the TV image, use `svc wifi`/`svc data`; toggling wifi drops the adb link for a few seconds; `-t:Install` can skip the install when the installed package is a different build (uninstall first). |

## Already handled / no change

| Point | Why |
|---|---|
| B.4 | `OnTaskRemoved` stops and kills playback on purpose (`RadioPlaybackService.cs:151`). A different policy with an Android Auto controller connected is a product decision, see Tomek's part. |
| B.8 | The 3–8 s buffer is set in `DefaultLoadControl` (`:90`). No change; measure real behavior while testing B.1. |
| C.1 | Android Auto, notification and steering wheel already have both prev/next (`RepeatModeAll`, `:126`). The in-app ⏮ visibility on other platforms stays as is. **No code change.** Only Tomek checks on the DHU that Android Auto keeps both buttons. |
| C.3 | `SongRequestService` already maps AzuraCast reasons to localized strings; the generic text is the fallback for unknown failures. Optional Core tests only. |
| E.1 | `AudioFocusManager` is unused on purpose (Media3 owns focus). |
| E.2 | `allowBackup="true"` is explicit: privacy decision, not a defect. |
| E.3 | The drawer header already binds `StationLogoUrl`. Investigate only if a device shows the fallback by mistake. |

## Part 1 — Maintainer plan (no Android Auto needed)

### M1. A — fonts missing from the v0.50 package
1. Inspect the published v0.50 APK (`unzip -l`) for `FaSolid`, `FaBrands`, `OpenSans`. Check the Play AAB too if reachable.
2. Build a clean signed Release APK/AAB by the documented route and inspect it.
3. Reproduce the stale `mauifont.stamp` case in a disposable output folder.
4. If it reproduces: add Android-only MSBuild targets (delete a stale stamp; fail the build if a font is missing). Tomek offered his.
5. Add the font check next to the existing ABI check in `docs/RELEASE.md`.

Guards: Android packaging only, never break iOS/Mac/Windows/Uno; no broad `obj` deletion; no alias or icon change to mask the defect.

### M2. B.1 — reconnect after an exhausted stream failure
Reproduce first on the phone emulator (airplane mode via `adb`), then on `Television_1080p`. If it reproduces, extend `OnStreamError` (`:382-415`): after the last candidate fails, bounded, cancellable backoff retry tied to the station/playback generation. Cancel on pause, stop, station change, task removal, service teardown. Unit-test the generation/backoff logic in isolation.

Guards: no infinite retry; live pause stays unbuffered; TV standby pause, remote media keys and long-pause live-edge reconnect keep working.

### M3. Measure on phone before touching code (B.2, B.3, B.5, B.6, C.4)
- **B.2** log `UpdateMetadata`, selected URI, `Prepare`, state on song change. Change `ReplaceMediaItem` only if a re-prepare or gap is proven.
- **B.3** kill the process, then `adb shell input keyevent MEDIA_PLAY`. Decide the product policy before implementing resumption.
- **B.5** look at the real notification and lock screen on the emulator. Decide: remove seek commands in `OnConnect`, or show AzuraCast track time.
- **B.6** simulated call (`adb emu gsm call`) and another app stealing focus. Add handling only for a callback seen on the pinned Media3.
- **C.4** capture ordering of controller error and service fallback. Retract the UI error only if a stale red error is visible.

### M4. C.2 — emoji to Font Awesome
Only after M1. Replace ⏮ ⏭ ⏸ 🔇 🔊 👥 📻 ☰ ⟳ in `OnAirPage`, `RadioListPage`, `PodcastEpisodesPage`, `TvNowPlayingView` using the existing icon font. Keep accessibility names, D-pad focus, light/dark.
Then: `parity/features.yaml`, Uno port, `tv` status, `CHANGELOG.md` (TV lines in `docs/ANDROID-TV-CHANGELOG-PENDING.md`), `ParityCheck`, `--strict-tv`, `tools/tv-focus-probe.sh`.

### M5. D — release and policy
- Inspect the Release AAB: DEX size, page size, ABIs, fonts, Media3 declarations.
- R8 experiment in an isolated branch only. Narrowest possible keep rule for the voice-search reflection (`:689`); no broad `-keep class androidx.** { *; }`. Voice search is verified by Tomek (T6).
- Play Console (vitals, Data Safety), developer verification and signing: tracked separately, linked to #6.

### M6. Our own regression testing
Any shared Media3 change (M2, M3, M5) is also tested on `Television_1080p` and the Sony TV before release.

### M7. Optional
Core tests for `SongRequestService` error parsing.

## Part 2 — Tomek's plan (Android Auto, DHU with physical phone or emulator)

Test only with a real projection session (DHU). An Android Automotive AVD is not an Android Auto test. Setup: `docs/android-auto-media3/DEV-SETUP_EN.md`.

| # | Task | Output |
|---|---|---|
| T1 | **B.1 on Android Auto.** Reproduce the coverage gap with DHU connected; verify our fix (M2) or send the RadioAndroid recovery logic as a PR. | Result + logcat |
| T2 | **B.3.** Play from steering wheel/Bluetooth/DHU after the app was closed and after process recreation. | Result; opinion on resumption |
| T3 | **B.4.** Does swiping the app away end the radio in Android Auto? Propose a policy (e.g. skip the kill only while an Android Auto controller is connected) and test it. | Result + proposal |
| T4 | **B.5.** Which controls the DHU draws for a live stream (seek bar, prev/next). | Screenshots |
| T5 | **B.6.** Assistant and navigation ducking inside Android Auto: focus loss, return, reconnection after long suppression. | Result + logcat |
| T6 | **B.7 and voice search.** Browse-list logos on the DHU (remote `https://` artwork). Voice search after the R8 build from M5. | Result |
| T8 | **R8 / obfuscation (D.1).** Handed over by the maintainer on 2026-10-28; see "R8 hand-over to Tomek" below. | A PR (opt-in first), evidence and a short note in `docs/RELEASE.md` |
| T7 | **C.1.** Confirm Android Auto still shows both prev and next with 2 stations, and nothing changed. | Screenshot |

### R8 hand-over to Tomek (T8)

Context and what was already measured are in the M5 row above: default R8 (`-p:AndroidLinkTool=r8`) cuts the DEX to 13.4 MB (still above 10 MB) but obfuscates 0.2% of the classes, because the SDK-generated `proguard_xamarin.cfg` always has `-dontobfuscate`. Play needs at least 25% on optimization, shrinking and obfuscation from February 2027 (apps above 10 MB of DEX). There is time: this does not block #7 and no release depends on it.

Constraints, because **the maintainer builds on their own Mac with local scripts** (`build-android.sh`, `build-store-android.sh`, keystore on that machine), not in CI:

1. **Everything lives in the repo.** The change must work with a plain `dotnet publish -f net10.0-android -c Release` on macOS, with no extra tool, no file outside the repo and no edit of the maintainer's scripts. If a script change is unavoidable, deliver it as a diff in the PR description.
2. **Opt-in first.** Gate it with an MSBuild property (for example `RadioE45EnableR8`, off by default) so normal Release builds stay as today. It becomes the default only after the checks below pass on the maintainer's machine; switching back must be one property.
3. **Release and Android only.** Debug, iOS, Mac Catalyst, Windows and Uno builds must be unaffected.
4. **Rules in a repo file** added through the `@(ProguardConfiguration)` item, not by replacing `ProguardConfigFiles`: replacing the list shrank the whole DEX to 904 bytes in the maintainer's spike.
5. **Narrow keep rules only.** No `-keep class androidx.** { *; }`. The voice-search reflection (`RadioPlaybackService.cs:792`) needs a keep on `MediaItem.requestMetadata`.
6. **Clean builds.** A change of the rules alone can be skipped by the incremental build (the R8 step was served from cache in the spike), so every measurement must start from a clean `obj`. The release scripts already start clean.
7. **Crash reports.** The app sends crashes to Sentry and Play. Obfuscated Java stack traces need the R8 `mapping.txt`: say where the build leaves it, how to upload it to Play Console and whether Sentry needs it too. The release flow must keep it next to the AAB (the maintainer's `store-packages/` folder) for each shipped version.

Acceptance, posted on #7:
- obfuscation percentage from `mapping.txt` (renamed classes over all classes) at or above 25%, and DEX size before and after;
- clean build time before and after on a Mac;
- the AAB still has the three ABIs, the four fonts and the `RadioPlaybackService` declaration;
- on the DHU: browse, playback, next/previous, voice search; on a phone: playback, notification, Bluetooth. The maintainer repeats the Sony TV check before the option becomes the default;
- a short section in `docs/RELEASE.md` with the build property, where `mapping.txt` ends up and how to upload it.

Rules for his PRs:
- One point per PR, with the reproduced scenario, device/API/Android Auto version and result.
- Android-only code under `RadioE45/Platforms/Android/`; no change to `OnTaskRemoved`, buffers or `RepeatMode` without a reproduced case.
- Follow `CLAUDE.md` (CHANGELOG line if user-visible, parity if UI, no commits on `main`). Do not post device identifiers, credentials or private stream URLs.
- Shared-service changes go through our M6 regression before merge.

## Closure
1. Every code change has a reproduced problem and cites its #7 point.
2. Android code builds with `dotnet build RadioE45 -f net10.0-android`; shared UI/Core passes ParityCheck.
3. Outcomes are posted on #7; product-policy items (B.3, B.4, E.2) become small follow-up issues.
