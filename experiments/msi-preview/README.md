# Update 013 — MSI experiment

Run CopyCheckSetup.msi. This is a native Windows Installer package built with WiX 3.14.1, not an EXE renamed or wrapped in MSI.

Installs for the current user into %LOCALAPPDATA%/Programs/CopyCheck MSI Preview. Standard installation does not require machine-wide administrator access. It has separate preferences, mutex, shortcuts, and uninstall registration. Close the stable CopyCheck from its tray while testing to avoid two checkmark animations.

Startup is enabled by default through a per-user Startup folder shortcut. The installer feature screen lets you select a matching desktop shortcut. Administrator mode is OFF by default in this experiment; enabling it can request UAC, including at later sign-ins. The preview does not use the production elevated scheduled task.

The preview has automatic and manual updates disabled. The public release/update channel stays on v1.0.12 and its EXE installer. This package does not fetch the latest release during setup; full production MSI migration is intentionally deferred until this format is tested.

Revert: exit the preview and uninstall CopyCheck MSI Preview in Windows Settings > Apps, or click its Uninstall CopyCheck button. Reopen the stable CopyCheck. If necessary, the EXE fallback - Update 012 folder contains the exact previously published installer and matching files; it was not rebuilt.

Silent MSI install: msiexec /i CopyCheckSetup.msi /qn /norestart
Optional desktop shortcut in silent mode: add ADDLOCAL=Core,Startup,Desktop
Uninstall: msiexec /x {53DA3BA1-32F5-4B64-8BFD-3DF0744A1B19}

MSI and its executable remain unsigned. SmartScreen, Smart App Control, browser warnings, or UAC may still appear. A local build test cannot establish how Windows treats a fresh Internet download. No Windows protections are disabled.

Build: run Build.ps1 with -WixDirectory pointing to the official WiX 3.14.1 binaries. WiX source is CopyCheck.wxs. Full install/launch/uninstall and first-download security prompts must be tested on a normal desktop.

Validation: native MSI compile/link and ICE validation passed with expected ICE91 per-user notices. Administrative extraction verified the embedded executable hash. 20 inherited app regression checks passed. The Update 012 fallback installer hash is unchanged. Live installation and Internet-download warning behavior remain untested.
