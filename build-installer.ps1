# Builds the SafeExamBrowser application and the installers (Release).
#
# Produces:
#   Setup\bin\x64\Release\Setup.msi
#   Setup\bin\x86\Release\Setup.msi
#   SetupBundle\bin\x86\Release\SetupBundle.exe   (wraps both MSIs + .NET 4.8 + VC++ runtime)
#
# Prerequisites:
#   - Visual Studio 2022 (or Build Tools) with .NET Framework 4.8 targeting pack.
#   - WiX Toolset v3.14 binaries. Point $env:WIX at the install root (must contain bin\candle.exe).
#     Portable setup: download wix314-binaries.zip, extract into <root>\bin, then set WIX=<root>\ .
#   - nuget.exe on PATH (or next to this script).
#   - Internet access (the bundle's PreBuildEvent downloads the .NET 4.8 and WebView2 installers).
#
# Usage:  powershell -ExecutionPolicy Bypass -File build-installer.ps1
#         powershell -ExecutionPolicy Bypass -File build-installer.ps1 -NoBundle   # MSIs only

param([switch]$NoBundle)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
Set-Location $root

# --- Locate tools -----------------------------------------------------------
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -products * -property installationPath
$msbuild = Join-Path $vs 'MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path $msbuild)) { throw "MSBuild not found at $msbuild" }

if (-not $env:WIX) { throw "Set the WIX environment variable to your WiX v3.14 install root (containing bin\candle.exe)." }
$wixBin = Join-Path $env:WIX 'bin'
if (-not (Test-Path (Join-Path $wixBin 'candle.exe'))) { throw "candle.exe not found under $wixBin" }

$nuget = (Get-Command nuget.exe -ErrorAction SilentlyContinue).Source
if (-not $nuget) { $nuget = Join-Path $root 'nuget.exe' }
if (-not (Test-Path $nuget)) { throw "nuget.exe not found (put it on PATH or next to this script)." }

$sln = Join-Path $root 'SafeExamBrowser.sln'
$wixProps = @(
    "-p:WixTargetsPath=$wixBin\wix.targets",
    "-p:WixTasksPath=$wixBin\WixTasks.dll",
    "-p:WixToolPath=$wixBin\",
    "-p:WixExtDir=$wixBin\",
    "-p:SolutionDir=$root\"
)

function Build-Platform([string]$plat) {
    Write-Host "`n=== Restoring + building application ($plat) ===" -ForegroundColor Cyan
    & $nuget restore $sln
    # PackageReference projects (non-SDK csproj) need a RID-aware restore:
    foreach ($p in 'SafeExamBrowser.SystemComponents', 'SafeExamBrowser.Integrity') {
        & $msbuild "$p\$p.csproj" -t:Restore -p:Configuration=Release -p:Platform=$plat -p:RuntimeIdentifier=win-$plat -v:q -nologo
    }
    & $msbuild $sln -t:Build -p:Configuration=Release -p:Platform=$plat -m -nologo

    Write-Host "=== Building Setup.msi ($plat) ===" -ForegroundColor Cyan
    & $msbuild 'Setup\Setup.wixproj' -t:Build -p:Configuration=Release -p:Platform=$plat @wixProps -nologo
}

Build-Platform 'x64'

if (-not $NoBundle) {
    Build-Platform 'x86'   # the bundle embeds both the x64 and x86 MSIs

    Write-Host "`n=== Building SetupBundle.exe ===" -ForegroundColor Cyan
    & $msbuild 'SetupBundle\SetupBundle.wixproj' -t:Build -p:Configuration=Release -p:Platform=x86 -p:SignOutput=false @wixProps -nologo
}

Write-Host "`n=== Done ===" -ForegroundColor Green
foreach ($a in 'Setup\bin\x64\Release\Setup.msi', 'Setup\bin\x86\Release\Setup.msi', 'SetupBundle\bin\x86\Release\SetupBundle.exe') {
    $p = Join-Path $root $a
    if (Test-Path $p) { Write-Host ("{0,-55} {1:N0} bytes" -f $a, (Get-Item $p).Length) }
}
