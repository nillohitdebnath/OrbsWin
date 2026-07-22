# =====================================================================================
# OrbsWin Build & Packaging Script
# =====================================================================================
# This PowerShell script builds a standalone, self-contained single-file Release binary
# for OrbsWin (win-x64) and packages all runtime assets into the /dist directory.
# =====================================================================================

param (
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$DistDir = "$PSScriptRoot\dist"
)

$ErrorActionPreference = "Stop"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " Building OrbsWin ($Configuration - $Runtime)" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 1. Clean previous dist output
if (Test-Path $DistDir) {
    Write-Host "Cleaning existing dist directory: $DistDir..." -ForegroundColor Yellow
    Remove-Item -Path $DistDir -Recurse -Force
}

New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

# 2. Run dotnet publish
Write-Host "Publishing OrbsWin self-contained single-file executable..." -ForegroundColor Green

dotnet publish "$PSScriptRoot\OrbsWin.csproj" `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o "$DistDir"

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 3. Ensure Assets directory exists in /dist
$distAssets = Join-Path $DistDir "Assets"
if (-not (Test-Path $distAssets)) {
    New-Item -ItemType Directory -Path $distAssets -Force | Out-Null
}

# Copy Assets if source exists
$srcAssets = Join-Path $PSScriptRoot "Assets"
if (Test-Path $srcAssets) {
    Write-Host "Copying Assets to dist directory..." -ForegroundColor Green
    Copy-Item -Path "$srcAssets\*" -Destination $distAssets -Recurse -Force
}

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host " Build & Packaging Complete!" -ForegroundColor Cyan
Write-Host " Output Directory: $DistDir" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

<#
=====================================================================================
MANUAL STEPS FOR PRODUCTION DISTRIBUTION (SIGNTOOL & INNO SETUP)
=====================================================================================

1. CODE SIGNING (Signtool):
   -----------------------
   To prevent Windows SmartScreen warnings, sign the executable using signtool.exe 
   and your Code Signing Certificate (.pfx):

   Example Command:
   signtool sign /f "C:\Path\To\Certificate.pfx" /p "YourCertPassword" /tr http://timestamp.digicert.com /td sha256 /fd sha256 "$PSScriptRoot\dist\OrbsWin.exe"

2. CREATING AN INSTALLER (Inno Setup):
   -----------------------------------
   Download & install Inno Setup (https://jrsoftware.org/isdl.php).
   Create a setup script `setup.iss` with the following configuration:

   [Setup]
   AppName=OrbsWin
   AppVersion=1.0.0
   DefaultDirName={autopf}\OrbsWin
   DefaultGroupName=OrbsWin
   OutputDir=installer_output
   OutputBaseFilename=OrbsWinSetup-1.0.0
   Compression=lzma
   SolidCompression=yes

   [Files]
   Source: "dist\OrbsWin.exe"; DestDir: "{app}"
   Source: "dist\Assets\*"; DestDir: "{app}\Assets"; Flags: recursesubdirs createallsubdirs

   [Icons]
   Name: "{autoprograms}\OrbsWin"; Filename: "{app}\OrbsWin.exe"
   Name: "{userstartup}\OrbsWin"; Filename: "{app}\OrbsWin.exe"

   [Run]
   Filename: "{app}\OrbsWin.exe"; Description: "Launch OrbsWin"; Flags: nowait postinstall skipifsilent

   To compile the installer via CLI:
   ISCC.exe setup.iss

=====================================================================================
#>
