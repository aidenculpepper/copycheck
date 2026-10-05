Fix setup failing with 0x80070002 when no CopyCheck startup task exists. Handle the .NET FileNotFoundException emitted by Task Scheduler COM interop. Preserve upgrade preferences and report the actual setup stage on other errors.

Includes optional timestamped code-signing support in Build.ps1. This release is unsigned; Windows SmartScreen may still show "Windows protected your PC". Trusted signing requires the publisher's certificate or signing-service account; signing alone does not guarantee immediate SmartScreen reputation.
