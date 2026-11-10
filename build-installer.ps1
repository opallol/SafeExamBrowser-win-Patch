# Builds the SafeExamBrowser application and the Setup.msi installer (Release | x64).
#
# Prerequisites:
#   - Visual Studio 2022 (or Build Tools) with .NET Framework 4.8 targeting pack.
#   - WiX Toolset v3.14 binaries. Point $env:WIX at the install root (must contain bin\candle.exe).
#     Portable setup: download wix314-binaries.zip, extract into <root>\bin, then set WIX=<root>\ .
#   - nuget.exe on PATH (or next to this script).
#
# Usage:  powershell -ExecutionPolicy Bypass -File build-installer.ps1

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

# --- Restore ----------------------------------------------------------------
# packages.config projects:
& $nuget restore $sln
# PackageReference projects need a RID-aware restore for the x64 build
# (SystemComponents and Integrity are non-SDK csproj + PackageReference):
foreach ($p in 'SafeExamBrowser.SystemComponents', 'SafeExamBrowser.Integrity') {
    & $msbuild "$p\$p.csproj" -t:Restore -p:Configuration=Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -v:q -nologo
}

# --- Build application ------------------------------------------------------
& $msbuild $sln -t:Build -p:Configuration=Release -p:Platform=x64 -m -nologo

# --- Build Setup.msi --------------------------------------------------------
& $msbuild 'Setup\Setup.wixproj' -t:Build -p:Configuration=Release -p:Platform=x64 `
    -p:SolutionDir="$root\" `
    -p:WixTargetsPath="$wixBin\wix.targets" `
    -p:WixTasksPath="$wixBin\WixTasks.dll" `
    -p:WixToolPath="$wixBin\" `
    -p:WixExtDir="$wixBin\" -nologo

$msi = Join-Path $root 'Setup\bin\x64\Release\Setup.msi'
Write-Host "`nInstaller built: $msi"
Write-Host ("Size: {0:N0} bytes" -f (Get-Item $msi).Length)
