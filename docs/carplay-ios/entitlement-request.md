# CarPlay Audio entitlement — request (Phase 0)

Submit at <https://developer.apple.com/contact/carplay/> signed in as the Account Holder.

The form has no free-text description. It asks only for:

1. **Developer information** (from the Apple Developer account).
2. **App type:** choose the **Audio** category.
3. **Terms and conditions** (CarPlay Entitlement Addendum): accept.

Submitted on: 2026-10-21
Approved on: 2026-10-21 (confirmation email: "The entitlement for CarPlay Audio App (CarPlay framework) has been assigned to your account"). Organization Admins can also use the capability.

## After submitting

- Apple reviews the request with non-public criteria. Time is not predictable.
- Since the form carries no description, the app itself is what gets judged. The points below
  matter when Apple (or App Review later) looks at it:
  - Radio E45 is an internet radio app, built primarily for audio playback.
  - In CarPlay it uses only system templates: `CPListTemplate` (stations) and
    `CPNowPlayingTemplate` (current track, play/pause; seeking disabled because the stream is live).
  - Bundle ID: `com.radioe45.app`. Background audio is enabled (`UIBackgroundModes: audio`).
- Keep these points for the App Review notes and for a possible reply if Apple asks questions.

## When approved

1. Enable the **CarPlay Audio** capability on the App ID.
2. Regenerate both provisioning profiles (Development and App Store).
3. Add `com.apple.developer.carplay-audio` to `Platforms/iOS/Entitlements.plist` (Phase F).
