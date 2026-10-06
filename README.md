# CopyCheck Update 020 — Mouse checkmark

Version 1.0.20. Windows 10/11 x64. Run CopyCheckSetup.exe to install. Fresh installs enable startup and administrator mode. Upgrades preserve existing preferences.

## Update settings

- Check automatically: on by default; checks public GitHub Releases on launch and every six hours.
- Install automatically: OFF by default; enabled only while automatic checks are on. When opted in, newer stable releases are downloaded and verified, then installed with silent Setup and CopyCheck relaunched after success.
- Check for updates and Install update: manual checking remains available; Install update uses silent Setup and relaunches CopyCheck after success, even when Install automatically is off.
- Turning off automatic installation during download prevents Setup from launching.
- Check/download errors and cancelled UAC prompts leave the app running. A setup failure after launch may require opening CopyCheck again.

Windows may still request administrator approval when the app is running without elevation. Silent update mode suppresses normal wizard screens and does not reboot Windows. Full elevated silent install/relaunch is not tested in this restricted session.

## Installation and removal

Installs into Program Files/CopyCheck, with optional matching desktop shortcut. Uses a visible HKCU Run entry for Task Manager Startup apps and an on-demand highest-privilege per-user task for administrator mode. The startup toggle and Task Manager disabled state are preserved on upgrade. Uninstall CopyCheck opens the normal uninstaller; it is available only for the installed app.

The installer checks GitHub for a newer installer before installing. If that check fails, interactive Setup explicitly asks before installing its bundled version. Downloaded installers must match the release asset's size and SHA-256 digest and exact repository release URL. No embedded GitHub credentials. Draft/prerelease versions and downgrades are rejected.

## Publishing

Increment matching assembly versions, Updates.Current, and the Inno versions. Run Build.ps1, then commit CopyCheckSetup.exe, generated release.json, and RELEASE_NOTES.md together to main in aidenculpepper/copycheck. The existing publish-release.yml workflow creates a new stable GitHub release. Never reuse a published version. The website's latest-release download link follows new releases automatically.

Update 006 needs a one-time newer installation to gain updating. Versions 007–009 can install this version using their manual update button. Signed releases require the publisher's certificate/account; this build is unsigned and SmartScreen warnings remain possible. See SIGNING.md for optional certificate-store signing.

Validation: 20 startup and automatic-install decision/argument checks, settings rendering, C# build, and Inno build passed. Full live sign-in, silent installation/relaunch, and uninstall require normal Windows desktop testing.

First-copy fix: confirmed copies retry unavailable text bounds for up to two seconds. Clipboard readiness is retried every 100 ms within the copy window. Changed clipboard sequence, source ownership, and populated clipboard are still required. Leaving the source window cancels feedback. A reboot reproduction remains untested.
Validation: 28 checks passed, including cold selection bounds, late clipboard rejection, duplicate events, bounded retries, cancellation/recovery, and existing startup/update regressions. App and Inno builds passed. Live reboot behavior remains untested.

Settings layout: status sits directly below Check for updates; successful up-to-date check uses green text with a small vector checkmark. Available updates use muted red. Uninstall has a subtle red tint and the installed version is at the bottom.
Validation: settings rendering reviewed; 28 inherited regression checks and app/Inno builds passed. Text-position issue will be addressed in the following update.

Position fix: find nonempty selected text matching the successful clipboard copy in the source window, including document providers used by browser/Electron/WebView apps. Empty textbox carets are rejected. Accessibility queries run in the background; stale results from older copies, changed clipboard contents, or a different source window are discarded. Apps that do not expose selection bounds may still have no animation; never guess a caret location. Live ChatGPT selection behavior still needs desktop confirmation.
Validation: 39 regression checks passed, including collapsed carets, mismatched selected text, document whitespace, stale asynchronous results, first-copy retry, and startup/update behavior. App/Inno builds passed. Live ChatGPT behavior has not been verified in this session.

Preference saves now atomically replace a temporary file, retrying transient sharing/lock conflicts for up to three seconds. Readers allow replacement without locking out saves. Persistent locks and permission errors still surface.
Validation: 46 checks passed, including saves with compatible readers, released external locks, persistent locks preserving previous data, and temporary-file cleanup. App/Inno builds passed. Full elevated installation remains untested.

CopyCheck is now the main heading above a smaller Settings label.

Updates now matches the smaller muted Settings header.

Checkmark next to mouse: OFF by default. Confirmed Ctrl+C text copies show beside the current pointer instead of selected text. Saved per user and applied immediately; copy-success checks and master animation switch remain required.
Validation: 49 checks and app/Inno builds passed; settings rendering reviewed. Full live mouse-mode copy behavior remains untested.
