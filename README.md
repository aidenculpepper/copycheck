# CopyCheck

A small Windows tray app that displays a checkmark after a successful Ctrl+C copy, beside the final selected character when the application exposes its text position.

## Download and install

Download [CopyCheckSetup.exe](CopyCheckSetup.exe), exit any running CopyCheck version, and run the installer. Approve the Windows permission prompt during setup. Select **Create a desktop shortcut** if wanted.

Requires Windows 10/11 x64 with .NET Framework 4.x. Install while signed into an administrator Windows account.

## Features

- Small animated copy confirmation.
- Checkmark tray icon and dark settings.
- Optional desktop shortcut with the matching icon.
- Administrator mode enabled by setup.
- Scheduled startup at sign-in without repeated administrator prompts.

Left-click the tray icon for settings. Right-click for Settings or Exit. The startup and administrator settings can be changed in the app.

The installer uses Windows Task Scheduler for elevated startup and leaves Windows UAC policy unchanged. It stores no passwords or clipboard history. Some applications do not expose usable selection coordinates and are skipped.

## Uninstall

Exit CopyCheck from its tray menu, then uninstall it through **Windows Settings → Apps → Installed apps → CopyCheck**. Uninstall removes the app, installed shortcuts, and its scheduled tasks. User preferences remain in LocalAppData\CopyCheck.

## Version and verification

Version 1.0.6. The installer is not code-signed.

App and installer builds, task XML validation, preference checks, and icon checks passed. Full elevated installation, uninstall, and startup after sign-in still require testing.
