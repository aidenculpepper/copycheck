# Rebuilds assets/og-image.png (1200x630) and assets/social-preview.png (1280x640)
# from the real app: compiles CopyCheck.cs with SocialRender.cs, draws the settings
# window and icon into temp/social, then screenshots banner.html in headless Chrome.
#   powershell -ExecutionPolicy Bypass -File scripts\social\Build-SocialImages.ps1
# -Source renders a different copy of CopyCheck.cs, for example one from an unmerged branch.
param([string]$Chrome, [string]$Source)
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$work = Join-Path $repo 'temp\social'
$assets = Join-Path $repo 'assets'
if (-not $Source) { $Source = Join-Path $repo 'CopyCheck.cs' }
New-Item -ItemType Directory -Force $work, $assets | Out-Null

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$renderer = Join-Path $work 'SocialRender.exe'
& (Join-Path $framework 'csc.exe') /nologo /target:winexe "/out:$renderer" /main:CopyCheck.SocialRender /reference:Microsoft.CSharp.dll /reference:System.Web.Extensions.dll "/reference:$framework\WPF\UIAutomationClient.dll" "/reference:$framework\WPF\UIAutomationTypes.dll" "/reference:$framework\WPF\WindowsBase.dll" $Source (Join-Path $PSScriptRoot 'SocialRender.cs')
if ($LASTEXITCODE -ne 0) { throw 'Could not compile the renderer.' }
$render = Start-Process -FilePath $renderer -ArgumentList "`"$work`"" -Wait -PassThru
if ($render.ExitCode -ne 0) { throw 'The renderer failed.' }

if (-not $Chrome) {
    foreach ($candidate in @("$env:ProgramFiles\Google\Chrome\Application\chrome.exe", "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe")) {
        if (Test-Path -LiteralPath $candidate) { $Chrome = $candidate; break }
    }
}
if (-not $Chrome) { throw 'Pass the path to chrome.exe or msedge.exe using -Chrome.' }
$page = ([Uri](Join-Path $PSScriptRoot 'banner.html')).AbsoluteUri
foreach ($image in @(@{ Name = 'og-image.png'; W = 1200; H = 630 }, @{ Name = 'social-preview.png'; W = 1280; H = 640 })) {
    $target = Join-Path $assets $image.Name
    $arguments = @('--headless=new', '--disable-gpu', '--hide-scrollbars', '--force-device-scale-factor=1',
        "--user-data-dir=$work\chrome-profile", "--window-size=$($image.W),$($image.H)",
        "--screenshot=$target", "$page`?w=$($image.W)&h=$($image.H)")
    Start-Process -FilePath $Chrome -ArgumentList $arguments -Wait -WindowStyle Hidden
    if (-not (Test-Path -LiteralPath $target)) { throw "Chrome did not write $target" }
    Write-Output "Wrote $target"
}
