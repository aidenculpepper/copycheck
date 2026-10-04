# CopyCheck Update 007 — Updates

Version 1.0.7. Windows 10/11 x64. Run CopyCheckSetup.exe to install. Setup checks the latest public GitHub Release first: if a newer installer exists, it downloads, verifies, and launches it. If the check fails, setup asks explicitly before installing the bundled version.

Settings includes Check for updates, an automatic-check toggle (on by default), installed version/status, and Install update when a newer release is found. Automatic checks run at launch and every six hours; installation always requires a click. Downloads must match the GitHub release asset size and SHA-256 digest. Drafts, prereleases, downgrades, foreign installer URLs, incomplete downloads, and invalid checksums are rejected. Checks use anonymous public GitHub requests, with no embedded credentials.

Upgrades preserve the current user's startup and administrator preferences. Automatic checks and animation preferences are saved in LocalAppData/CopyCheck. Installation remains in Program Files/CopyCheck with an optional matching desktop shortcut and scheduled sign-in startup. Setup requires an administrator Windows account. The installer is unsigned.

## Publishing future versions

1. Increment matching versions in CopyCheck.cs (assembly versions and Updates.Current) and CopyCheck.iss (AppVersion and VersionInfoVersion).
2. Run Build.ps1 with the Inno Setup compiler path. It builds the installer and generates release.json from that exact binary.
3. Upload CopyCheckSetup.exe, release.json, and RELEASE_NOTES.md together to the main branch of aidenculpepper/copycheck. The included GitHub workflow creates a stable release named vX.Y.Z and attaches the installer. Never reuse a published version.
4. Wait for the Publish CopyCheck release workflow to succeed. Existing Update 007 installers and apps will discover the newest published stable release. Changing source or unrelated files alone does not create an installable update.

Update 006 cannot check for updates; install Update 007 once to enable future updating. In-app installs launch standard interactive Setup. Old installers may show their normal UAC prompt before checking for newer releases. Downloads remain in a uniquely named Windows temp folder for Windows to clean up; cancelling never closes the app.

Validation: parser/version/digest/URL/size rejection checks, settings rendering, app and Inno builds. Full elevated installation, live upgrade, UAC cancellation, sign-in startup, and uninstall still need testing on a normal Windows desktop.
