param(
    [string]$InnoCompiler,
    [string]$SignTool,
    [string]$CertificateThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
$releaseDirectory = $PSScriptRoot
$frameworkDirectory = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if ($CertificateThumbprint) {
    if ($CertificateThumbprint -notmatch '^[0-9a-fA-F]{40}$') { throw 'Supply a valid certificate thumbprint.' }
    if (-not $SignTool) {
        $signCommand = Get-Command signtool.exe -ErrorAction SilentlyContinue
        if ($signCommand) { $SignTool = $signCommand.Source }
    }
    if (-not $SignTool) { throw 'Pass the Windows SDK signtool.exe path using -SignTool.' }
}
function Sign-ReleaseFile([string]$FilePath) {
    if (-not $CertificateThumbprint) { return }
    & $SignTool sign /sha1 $CertificateThumbprint /s My /fd SHA256 /tr $TimestampUrl /td SHA256 $FilePath
    if ($LASTEXITCODE -ne 0) { throw "Signing failed: $FilePath" }
    if ((Get-AuthenticodeSignature -LiteralPath $FilePath).Status -ne 'Valid') { throw "Signature is not trusted: $FilePath" }
}
& (Join-Path $frameworkDirectory 'csc.exe') /nologo /target:winexe "/out:$releaseDirectory\CopyCheck.exe" "/win32icon:$releaseDirectory\CopyCheck.ico" /reference:Microsoft.CSharp.dll /reference:System.Web.Extensions.dll "/reference:$frameworkDirectory\WPF\UIAutomationClient.dll" "/reference:$frameworkDirectory\WPF\UIAutomationTypes.dll" "/reference:$frameworkDirectory\WPF\WindowsBase.dll" "$releaseDirectory\CopyCheck.cs"
if ($LASTEXITCODE -ne 0) { throw 'CopyCheck build failed.' }
Sign-ReleaseFile (Join-Path $releaseDirectory 'CopyCheck.exe')
if (-not $InnoCompiler) {
    $compilerCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($compilerCommand) { $InnoCompiler = $compilerCommand.Source }
    else {
        $candidate = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
        if (Test-Path -LiteralPath $candidate) { $InnoCompiler = $candidate }
    }
}
if (-not $InnoCompiler) { throw 'Install Inno Setup 6.7 or later, then pass its ISCC.exe path using -InnoCompiler.' }
& $InnoCompiler /Q "$releaseDirectory\CopyCheck.iss"
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
Sign-ReleaseFile (Join-Path $releaseDirectory 'CopyCheckSetup.exe')
$installerHash = (Get-FileHash -LiteralPath (Join-Path $releaseDirectory "CopyCheckSetup.exe") -Algorithm SHA256).Hash.ToLowerInvariant()
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $releaseDirectory "CopyCheck.exe")).FileVersion
$releaseVersion = ([Version]$version).ToString(3)
@{ version = $releaseVersion; sha256 = $installerHash } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseDirectory "release.json") -Encoding UTF8
Write-Output "Built $releaseDirectory\CopyCheckSetup.exe"
