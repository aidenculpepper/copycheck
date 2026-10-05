# Windows SmartScreen and signing

This release is unsigned. The installer bug fix does not remove SmartScreen's "Windows protected your PC" reputation warning.

To sign a future release, obtain a trusted code-signing certificate with its private key accessible in your current user's Windows Personal certificate store, and install the official Windows SDK SignTool. Then pass -SignTool and -CertificateThumbprint to Build.ps1 along with -InnoCompiler. The build signs and timestamps the app before packaging, signs the completed installer, requires trusted signatures, and generates release.json from the final installer. Do not upload certificates or private keys to the public repository. Hardware-backed certificates may require your provider's signing software.

Microsoft Artifact Signing needs its own account and integration and is not configured by this certificate-store option. Self-signed certificates do not establish SmartScreen trust. Even trusted signatures can initially receive warnings until reputation builds. Microsoft Store distribution is another route, requiring separate packaging and submission.

See https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation
