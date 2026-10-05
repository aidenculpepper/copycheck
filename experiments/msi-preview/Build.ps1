param([string]$WixDirectory = (Join-Path $PSScriptRoot '..\..\work\installer-tools\wix3'))
$ErrorActionPreference = 'Stop'
$f = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
& "$f\csc.exe" /nologo /target:winexe "/out:$PSScriptRoot\CopyCheck.exe" "/win32icon:$PSScriptRoot\CopyCheck.ico" /reference:Microsoft.CSharp.dll /reference:System.Web.Extensions.dll "/reference:$f\WPF\UIAutomationClient.dll" "/reference:$f\WPF\UIAutomationTypes.dll" "/reference:$f\WPF\WindowsBase.dll" "$PSScriptRoot\CopyCheck.cs"
if ($LASTEXITCODE -ne 0) { throw 'App build failed.' }
$WixDirectory = (Resolve-Path -LiteralPath $WixDirectory).Path
Push-Location $PSScriptRoot
try {
 & "$WixDirectory\candle.exe" -nologo -arch x64 CopyCheck.wxs -out CopyCheck.wixobj
 if ($LASTEXITCODE -ne 0) { throw 'MSI compile failed.' }
 & "$WixDirectory\light.exe" -nologo -ext WixUIExtension -ext WixUtilExtension CopyCheck.wixobj -out CopyCheckSetup.msi
 if ($LASTEXITCODE -ne 0) { throw 'MSI link/validation failed.' }
 Get-FileHash -LiteralPath CopyCheckSetup.msi -Algorithm SHA256
} finally { Pop-Location }
