<!-- markdownlint-disable MD022 MD024 -->
# Changelog

All notable changes to Radio E45 are documented in this file. Texts are in English and written for users, not for developers.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versions follow `version.json` (two digits, e.g. `0.47`); the Git tag is `v` plus the version. The section of the version being released is used as the body of the GitHub release (see `docs/RELEASE.md`). Releases before 0.46 are only on the [GitHub Releases](https://github.com/delfuria/RadioE45/releases) page.

## [Unreleased]

### New
- **Mac:** the app now checks once a day whether a newer version is available and has a manual check in Settings.

### Fixed
- **Updates (iPhone and iPad):** the update check now sees a new version as soon as it is published on the App Store, instead of missing it for up to a day.
- **Android:** when the connection drops completely (a tunnel, a coverage gap), the radio now keeps trying to reconnect and resumes by itself when the network is back, instead of staying silent until you press Play.

## [0.50] - 2026-10-24

### Fixed
- **Pause:** pausing the radio from the Mac Control Center or media keys, or from the notification, lock screen or Bluetooth controls on Android, now stops the stream, so resuming puts you back on the live broadcast instead of the delayed buffered audio.
- **Mac Control Center:** while the radio is paused, the cover and track title no longer flicker to the app icon and name.

## [0.49] - 2026-10-23

### New
- **Android TV:** the app now runs on Android TV and Google TV with a remote-friendly screen: now playing with the next track, previous/next station and favorite, song requests, podcasts, the schedule, and adding, editing and deleting stations with the on-screen keyboard.

### Fixed
- **Android:** resuming the radio after a long pause (for example after the TV or a media key paused it) now reconnects to the live stream instead of playing the old buffered audio.
- **Schedule:** with a larger text size the schedule panel on iPhone and Android now uses the full screen width instead of a narrow window, and the "On Air" badge keeps its normal height.

## [0.48] - 2026-10-22

### New
- **Apple CarPlay:** choose a station from the list on the car screen and see the track, artist and cover in Now Playing; the car's buttons pause, resume and change station.
- **iPhone and iPad:** the next and previous buttons on the lock screen, in Control Center, in CarPlay and on headphones switch to the next or previous station.

### Fixed
- **CarPlay:** stations that stream in MP3 now start in about a second instead of after about 20 seconds, also after pause and when changing station.
- **iPhone and iPad:** when you unplug headphones or disconnect from CarPlay, the radio stays paused instead of starting again on the phone speaker a few seconds later.
- **On Air:** the HLS badge has the same height as the station name badge.

## [0.47] - 2026-10-21

### New
- **Update check:** the app tells you when a newer version is available (App Store, Microsoft Store, GitHub releases for Linux and the Windows installer, and the web app), with a manual check in Settings.

### Improved
- **On Air:** the previous/next station buttons are hidden when there is a single station.
- **On Air:** the buffering spinner is replaced by the VU meter.
- **Requests:** the list of songs you can request is shuffled.

## [0.46.6] - 2026-10-16

### New
- **Web app on GitHub Pages:** Radio E45 now runs in the browser at https://delfuria.github.io/RadioE45/ and can be installed as an app from the browser menu.

### Improved
- **Requests:** songs titled "jingle" are no longer listed among the songs you can request.
- **Podcasts:** the episode count now uses the right singular and plural ("1 episode", "2 episodes"), including the Polish forms.
- **Add station:** a station you already have is now recognised even if you type the server address with `https://` or a trailing slash.
- **Settings:** the Theme setting now always shows the theme that is actually applied.
- **Web:** songs without artwork now show the Radio E45 generic cover instead of an empty placeholder.

### Fixed
- **Linux (arm64):** the app failed to start on a clean system because of missing system libraries.
- **Light theme:** secondary text in the schedule and in the podcast pages was hard to read.
- **Loading:** "No schedule", "No songs", "No podcasts" and "No episodes" are no longer shown while the list is still loading.
- **Requests:** after a loading error the empty list now shows its message.

## [0.46] - 2026-10-16

### New
- **Linux desktop app**, with `.deb` packages for x64 and arm64.
- **Web app** that runs in the browser and can be installed as a PWA, with an offline shell.
- Both new apps include On Air, Channels, Add/Edit station, Podcasts, Settings, Terms of use and the side menu.
- System media controls: MPRIS on Linux and Media Session in the browser.
- **Clearer schedule:** streamers always take precedence, and playlists are clipped or split around their time slots.
- A streamer shows **LIVE** only when connected, otherwise **On schedule**. Playlists show **On Air** only when no live is connected.
- Schedule popup: microphone icon for streamers, music note for playlists.
- Terms and Conditions page.
- Privacy info for macOS.

### Improved
- Podcast episode filters are always visible, with the status filter first.
- Station search retries on local-network hosts while the Local Network alert is shown, and skips private hosts for public stations.
- Web: the radio restarts after a podcast, and starts at the first tap when autoplay is blocked.
- Web: streams the browser fetch refuses can still play.
- Web: content is centered on wide screens.

### Fixed
- Podcast resume position.
- Podcast tab switching.
- Web: playback no longer fails because streams were intercepted by the service worker.
- Neutral-language strings.
