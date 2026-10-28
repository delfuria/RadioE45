# Issue #7 — Android / Android Auto action plan

Issue [#7](https://github.com/delfuria/RadioE45/issues/7) is a map of hypotheses, not a defect list. Every point below was compared with the current source. Nothing is scheduled that is already implemented or deliberately chosen.

Ownership split:

- **Maintainer (us):** everything that does not need Android Auto. Phone and Android TV emulators, the Sony TV, Mac builds, release artifacts, Play Console.
- **Tomek (`toevi`):** everything that needs Android Auto, on the Desktop Head Unit (DHU) with a physical phone, or on an Android emulator driving the DHU.

The Android implementation has one `MediaLibraryService`, one Media3 session, one player. Phone, notification, Bluetooth, Android Auto and Android TV share it. Do not add a second player/session and do not attach the legacy `AudioFocusManager` to Media3.

## Execution status

| Step | Status | Notes |
|---|---|---|
| M1 — fonts (A) | **Done, not committed** | The v0.50 GitHub APK has no font (0/4); v0.49 APK and the v0.50 AAB have all four, so Play is not affected. Reproduced in a throwaway copy: with `mauifont.stamp` surviving and the rest of `obj/Release/net10.0-android` gone, the build exits 0 and the APK has no font. `dotnet clean -f net10.0-android -c Release` alone does not reproduce it (it removes the stamp too), so the exact trigger of the v0.50 APK is unproven. Fix: `DropStaleMauiFontStamp` and `VerifyMauiFontsPresent` in `RadioE45.csproj` (Android only; verified 4/4 fonts on the stale state), plus a font check next to the ABI check in `build-android.sh` and `build-store-android.sh` (outside the repo). Open: rebuild and re-upload the v0.50 APK, or wait for the next release. |
| M2 — B.1 retry | In progress | |
| M3–M7 | Not started | |

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
| T7 | **C.1.** Confirm Android Auto still shows both prev and next with 2 stations, and nothing changed. | Screenshot |

Rules for his PRs:
- One point per PR, with the reproduced scenario, device/API/Android Auto version and result.
- Android-only code under `RadioE45/Platforms/Android/`; no change to `OnTaskRemoved`, buffers or `RepeatMode` without a reproduced case.
- Follow `CLAUDE.md` (CHANGELOG line if user-visible, parity if UI, no commits on `main`). Do not post device identifiers, credentials or private stream URLs.
- Shared-service changes go through our M6 regression before merge.

## Closure
1. Every code change has a reproduced problem and cites its #7 point.
2. Android code builds with `dotnet build RadioE45 -f net10.0-android`; shared UI/Core passes ParityCheck.
3. Outcomes are posted on #7; product-policy items (B.3, B.4, E.2) become small follow-up issues.
