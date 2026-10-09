# App Store review — Guideline 5.2.3 (third-party content)

Apple rejected version 0.43.1 under 5.2.3 and asked for documentary evidence of rights over
third-party streaming content. The app has no third-party catalog, so the answer is to remove
everything that could look like one and to show terms that put the responsibility for added
streams on the user and give rights holders a contact.

## What the app does now

| Item | Where |
|------|-------|
| Preloaded stations: only **Radio E45** (operated by the developer) and the **official AzuraCast demo**. The third-party "Muse" station is gone. | `RadioE45.Core/Services/Data/DatabaseService.cs` |
| **Terms of Use** dialog shown once, at first launch, with a single "I have read this" button. It never shows again. | `RadioE45.Core/Services/Legal/TermsService.cs` |
| Dialog called at startup, before the "add sample stations?" prompt | `RadioE45/AppShell.xaml.cs` (`OnNavigated`) |
| **Settings → Info → Terms of Use** shows the same text again (button "OK", records nothing). The first-launch text says so in its last line. | `RadioE45/Views/SettingsPage.xaml`, `SettingsViewModel.ShowTermsCommand` |
| Texts in Italian, English, Polish (`Terms_Title`, `Terms_Body`, `Terms_Accept`, `Settings_Terms`) | `RadioE45.Core/Resources/Strings/AppResources*.resx` |
| Tests: shown once, not shown again, recorded only after dismissal | `RadioE45.Core.Tests/Services/Legal/TermsServiceTests.cs` |

The acknowledgement is stored as `terms_accepted_version` in the key/value settings
(`ISettingsStore`). If the text changes materially, raise `TermsService.CurrentTermsVersion`
and the dialog appears once more. It survives "Reset database" because it is not in SQLite.

For the Uno head: call `ITermsService.EnsureAcceptedAsync()` once the first page is on screen,
before any other startup prompt. Nothing else is needed, the service is registered by
`AddRadioE45Core()`.

## Terms text shown to the user (English)

> Radio E45 is a player for internet radio streams. It does not host, store or rebroadcast any audio.
>
> Stations are added by you via public URL. You confirm you are allowed to listen to them and will
> respect each operator's terms. Rights to each stream belong to its operator or licensors.
>
> The app includes only the Radio E45 station (operated by the developer) and the official
> AzuraCast demo station. You can remove them at any time.
>
> Rights holders: to report unauthorized access or request removal of a station, write to
> support@radioe45.it with the station name, stream URL and a short description of your rights.
> Valid requests are handled within 5 business days.
>
> The app is provided "as is". We are not responsible for third-party streams.
>
> You can read these terms again at any time in Settings → Info.

## Before resubmitting

1. Make sure `support@radioe45.it` is read and can answer within 5 business days.
2. Submit a **new build** (Apple said the issue cannot be fixed in a later submission).
3. Test on a **clean install**: first the Terms dialog, then the "add sample stations?" prompt,
   and the list must contain only the two stations above. Then relaunch: no dialog.
4. Existing installs keep the stations already in their database. Removing old third-party stations
   from them would need a `CurrentDbVersion` bump, which resets the seed tables, so it was not done.
   Apple reviews a clean install.
5. Paste the notes below in App Store Connect → App Review Information → Notes.

## Text for App Review Information → Notes

```
The app contains no third-party content or catalog. It ships only with the Radio E45
station (operated by the developer) and the official AzuraCast demo station. Any other
station is added by the user via public URL, like adding a podcast feed by URL in a
podcast player; the app does not host, store or rebroadcast audio.

On first launch the app shows Terms of Use (also available in Settings > Info) that make the user responsible for the streams
added and give rights holders a contact for reports and removal requests:
support@radioe45.it
```

## Reply for the Resolution Center

```
Hello,

Thank you for the feedback on Radio E45. We have updated the app so that it no longer
includes anything that could be mistaken for a catalog of third-party streams:

- The only stations preloaded on first launch are the Radio E45 station, which we operate,
  and the official AzuraCast demo station. All other stations are added by the user via
  public URL. The app does not host, store or rebroadcast any audio.
- On first launch the app now shows Terms of Use (also available in Settings > Info) that make users responsible for the
  streams they add and give rights holders a contact for reports and removal requests,
  with a 5-business-day response commitment: support@radioe45.it

Because the app now contains no third-party content, there are no third-party rights to
document. If you still need documentation, please tell us exactly which stream or feature
you consider to require it and we will provide it or remove it right away.

Best regards,
Radio E45
```

## If Apple still insists

- Ask for a call with App Review, or file an appeal with the App Review Board.
- Optionally collect written consent (a plain email is enough) from AzuraCast for the demo
  station and attach it under App Review Information.
