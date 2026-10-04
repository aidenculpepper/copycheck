param([string]$InnoCompiler)
$ErrorActionPreference = 'Stop'
$releaseDirectory = $PSScriptRoot
$frameworkDirectory = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
& (Join-Path $frameworkDirectory 'csc.exe') /nologo /target:winexe "/out:$releaseDirectory\CopyCheck.exe" "/win32icon:$releaseDirectory\CopyCheck.ico" /reference:Microsoft.CSharp.dll /reference:System.Web.Extensions.dll "/reference:$frameworkDirectory\WPF\UIAutomationClient.dll" "/reference:$frameworkDirectory\WPF\UIAutomationTypes.dll" "/reference:$frameworkDirectory\WPF\WindowsBase.dll" "$releaseDirectory\CopyCheck.cs"
if ($LASTEXITCODE -ne 0) { throw 'CopyCheck build failed.' }
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
$installerHash = (Get-FileHash -LiteralPath (Join-Path $releaseDirectory "CopyCheckSetup.exe") -Algorithm SHA256).Hash.ToLowerInvariant()
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $releaseDirectory "CopyCheck.exe")).FileVersion
$releaseVersion = ([Version]$version).ToString(3)
@{ version = $releaseVersion; sha256 = $installerHash } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseDirectory "release.json") -Encoding UTF8
Write-Output "Built $releaseDirectory\CopyCheckSetup.exe"
