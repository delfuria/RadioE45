# App Store review — Guideline 5.1.1(ii) (Local Network purpose string)

Apple rejected build 0.44.10 under 5.1.1(ii) — Data Collection and Storage: the app accesses the
local network but the Info.plist has no clear `NSLocalNetworkUsageDescription`.

## Cause

The AzuraCast server behind Radio E45 has its **Base URL** (System Settings → Base URL) set to a LAN
address. The station API therefore returns `listen_url` (and `hls_url`, track `art`) with
`http://192.168.1.100/...`. The app probed every stream candidate in order (`StreamUrlProber`), so
pressing play on Radio E45 tried a private address and iOS showed the Local Network prompt, with
no purpose string.

## Fix

| Item | Where |
|------|-------|
| `NSLocalNetworkUsageDescription` added (development language: English). `CFBundleDevelopmentRegion` = `en`, `CFBundleLocalizations` = `en`, `it`, `pl` added. No `NSBonjourServices` (the app does not use Bonjour). | `RadioE45/Platforms/iOS/Info.plist`, `RadioE45/Platforms/MacCatalyst/Info.plist` |
| Localized purpose string. MAUI bundles `Platforms/<platform>/Resources/**` automatically (`IPhoneResourcePrefix`), so the csproj needs no change. | `RadioE45/Platforms/{iOS,MacCatalyst}/Resources/{en,it,pl}.lproj/InfoPlist.strings` |
| `PrivateHostDetector`: purely syntactic check (no DNS) for private hosts: 10/8, 172.16/12, 192.168/16, 169.254/16, 127/8, `::1`, `fe80::/10`, `fc00::/7`, dotless names, `localhost`, `.local`, `.lan`, `.home.arpa`, `.internal`. | `RadioE45.Core/Services/Audio/PrivateHostDetector.cs` |
| Stream candidates: private URLs are dropped for **public** stations, order and `PreferHlsStream` unchanged. A station is "local" when its `UrlBase` is private (fallback: `StreamUrlFallback`); local stations keep every candidate. If nothing public is left, the original list is used. | `LiveStreamAudioService.GetCandidateUrls` (iOS, Mac Catalyst, Windows, Uno), `RadioPlaybackService.GetStreamCandidates` (Android) |
| Track artwork from the now-playing API: same rule, a private `art` URL becomes `null` for public stations. | `RadioE45.Core/Services/Radio/NowPlayingService.cs` |
| Add station on a LAN host: while the Local Network alert is on screen the system can fail the first request (TN3179), which showed "server unreachable". On Apple platforms only (`IAppEnvironment.HasLocalNetworkPrivacy`) and only for private addresses, the search is retried every 2 s, at most 10 attempts / 30 s, so results appear once the user answers. | `RadioE45.Core/ViewModels/AddStationViewModel.cs` |
| Tests | `RadioE45.Core.Tests/Services/Audio/PrivateHostDetectorTests.cs`, `LiveStreamAudioServiceTests.cs`, `RadioE45.Core.Tests/ViewModels/AddStationViewModelTests.cs` |

### Still reachable on a misconfigured public server

These come straight from the AzuraCast API and have no public alternative in the app, so they are
not filtered. They only matter while the server Base URL is wrong, and only when the user opens
those screens:

- Podcast episode media (`links.download`) and podcast/episode artwork (`PodcastService`).
- Song request list artwork (`AzuraCastRequestItem.ArtworkUrl`).

Fixing the Base URL on the server removes all of them.

## Purpose string

| Language | Text |
|----------|------|
| en | Radio E45 connects to radio stations on your local network, for example an AzuraCast server at home or in your studio, so you can listen to them in the app. |
| it | Radio E45 si collega alle stazioni radio della tua rete locale, ad esempio un server AzuraCast di casa o del tuo studio, per permetterti di ascoltarle nell'app. |
| pl | Radio E45 łączy się ze stacjami radiowymi w Twojej sieci lokalnej, na przykład z serwerem AzuraCast w domu lub w studiu, aby można było ich słuchać w aplikacji. |

## Server side (do this too)

In AzuraCast, **System Settings → Base URL**, set the public address `https://radioe45.ddns.net`.
The API then stops returning LAN addresses in `listen_url`, `hls_url` and `art`.

## Device check before resubmitting

1. Uninstall the app, install the new build, launch it.
2. With the default stations (Radio E45, AzuraCast demo), play each one and open the On Air screen:
   the Local Network prompt must **not** appear.
3. Add a station whose URL is on the LAN (e.g. `192.168.1.x` or `myserver.local`): the prompt must
   appear with the new text in the device language (try it, en, pl); after allowing it the station
   list appears without pressing "Search" again, and playback works.

Conditions for the prompt to appear (TN3179): a **real device** (the Simulator has no local network
privacy); the app **deleted first** (the choice is remembered); the server on the **same Wi-Fi/Ethernet
subnet** (routed, VPN, cellular and loopback addresses don't count). On macOS, open the app from
Finder, not from Terminal/IDE (processes launched from Terminal are allowed automatically); the
choice can be reset in System Settings → Privacy & Security → Local Network (macOS 27.2+).
4. Bump the version (`version.json`, see [`VERSION-BUMP.md`](VERSION-BUMP.md)), build, upload.

## Reply for the Resolution Center

> Thank you for the review. We have added the NSLocalNetworkUsageDescription purpose string, now localized in English, Italian and Polish: 'Radio E45 connects to radio stations on your local network, for example an AzuraCast server at home or in your studio, so you can listen to them in the app.' The app only accesses the local network when the user adds a station that is hosted on a private address (for example a personal AzuraCast server). We also changed the app so that it no longer attempts connections to private addresses for public stations such as Radio E45 and the demo stations, so the local network permission prompt does not appear in normal use. The new build is attached.
