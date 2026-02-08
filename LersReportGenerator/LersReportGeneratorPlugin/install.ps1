# LersReportGeneratorPlugin Install Script
# Build and install plugin on target machine with LERS installed
#
# Usage:
#   .\install.ps1                     # Build + install (auto-increment version if changed)
#   .\install.ps1 -BuildOnly          # Build only, no install
#   .\install.ps1 -NoVersionIncrement # Build without version increment
#   .\install.ps1 -IncrementMinor     # Increment minor version (new features)
#   .\install.ps1 -IncrementMajor     # Increment major version (breaking changes)
#   .\install.ps1 -LersClientPath "D:\LERS\Client"  # Specify path manually
#
# Automatically:
#   - Increments patch version when code changes
#   - Installs .NET SDK 8.0 if not found (winget or dotnet-install.ps1)
#   - Finds LERS folder (registry, standard paths, search)
#   - Installs plugin to Plugins\ with backup of previous version
#
# Requirements:
#   - Installed LERS ARM operator
#   - Administrator rights (for .NET SDK installation)

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$LersClientPath,

    [switch]$BuildOnly,

    [switch]$NoVersionIncrement,

    [switch]$IncrementMinor,

    [switch]$IncrementMajor
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Write-Step {
    param([string]$Text)
    Write-Host ""
    Write-Host "[*] $Text" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Text)
    Write-Host "[+] $Text" -ForegroundColor Green
}

function Write-Err {
    param([string]$Text)
    Write-Host "[-] $Text" -ForegroundColor Red
}

function Write-Info {
    param([string]$Text)
    Write-Host "    $Text" -ForegroundColor Gray
}

# ============================================
# VERSION MANAGEMENT
# ============================================

$AssemblyInfoPath = Join-Path $ScriptDir "Properties\AssemblyInfo.cs"
$HashFilePath = Join-Path $ScriptDir ".build-hash"

function Get-SourceHash {
    $csFiles = Get-ChildItem -Path $ScriptDir -Filter "*.cs" -Recurse |
               Where-Object { $_.FullName -notmatch "\\(bin|obj)\\" } |
               Sort-Object FullName

    $allContent = ""
    foreach ($file in $csFiles) {
        $allContent += Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    }

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($allContent)
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return [System.BitConverter]::ToString($hash) -replace '-', ''
}

function Get-CurrentVersion {
    $content = Get-Content $AssemblyInfoPath -Raw
    if ($content -match 'AssemblyVersion\("(\d+)\.(\d+)\.(\d+)\.(\d+)"\)') {
        return @{
            Major = [int]$matches[1]
            Minor = [int]$matches[2]
            Patch = [int]$matches[3]
            Build = [int]$matches[4]
        }
    }
    return @{ Major = 1; Minor = 0; Patch = 0; Build = 0 }
}

function Set-Version {
    param($Major, $Minor, $Patch, $Build)

    $versionString = "$Major.$Minor.$Patch.$Build"
    $content = Get-Content $AssemblyInfoPath -Raw

    $content = $content -replace 'AssemblyVersion\("[^"]+"\)', "AssemblyVersion(`"$versionString`")"
    $content = $content -replace 'AssemblyFileVersion\("[^"]+"\)', "AssemblyFileVersion(`"$versionString`")"

    Set-Content $AssemblyInfoPath -Value $content -NoNewline
    Write-Success "Version updated: $versionString"
}

function Update-VersionIfNeeded {
    if ($NoVersionIncrement) {
        Write-Info "Version increment skipped (-NoVersionIncrement)"
        return
    }

    $currentHash = Get-SourceHash
    $savedHash = ""

    if (Test-Path $HashFilePath) {
        $savedHash = Get-Content $HashFilePath -Raw
    }

    $version = Get-CurrentVersion
    $needIncrement = $false

    if ($IncrementMajor) {
        $version.Major++
        $version.Minor = 0
        $version.Patch = 0
        $version.Build = 0
        $needIncrement = $true
        Write-Info "Major version incremented"
    }
    elseif ($IncrementMinor) {
        $version.Minor++
        $version.Patch = 0
        $version.Build = 0
        $needIncrement = $true
        Write-Info "Minor version incremented"
    }
    elseif ($currentHash -ne $savedHash) {
        $version.Patch++
        $needIncrement = $true
        Write-Info "Code changes detected, patch version incremented"
    }
    else {
        Write-Info "No code changes, version unchanged"
    }

    if ($needIncrement) {
        Set-Version -Major $version.Major -Minor $version.Minor -Patch $version.Patch -Build $version.Build
    }

    # Save hash after successful version update
    Set-Content $HashFilePath -Value $currentHash -NoNewline
}

function Find-LersPath {
    Write-Step "Searching for LERS..."

    # 1. Check registry
    $regPaths = @(
        "HKLM:\SOFTWARE\LERS",
        "HKLM:\SOFTWARE\WOW6432Node\LERS",
        "HKCU:\SOFTWARE\LERS"
    )

    foreach ($regPath in $regPaths) {
        if (Test-Path $regPath) {
            try {
                $installPath = (Get-ItemProperty -Path $regPath -ErrorAction SilentlyContinue).InstallPath
                if ($installPath) {
                    $clientPath = Join-Path $installPath "Client"
                    if (Test-Path (Join-Path $clientPath "Lers.Core.dll")) {
                        Write-Info "Found in registry: $clientPath"
                        return $clientPath
                    }
                }
            }
            catch { }
        }
    }

    # 2. Standard paths
    $standardPaths = @(
        "C:\Program Files\LERS\Client",
        "C:\Program Files (x86)\LERS\Client",
        "D:\LERS\Client",
        "D:\Program Files\LERS\Client"
    )

    foreach ($path in $standardPaths) {
        if (Test-Path (Join-Path $path "Lers.Core.dll")) {
            Write-Info "Found at standard path: $path"
            return $path
        }
    }

    # 3. Search for Lers.Client.exe on C: drive
    Write-Info "Searching for Lers.Client.exe..."
    $found = Get-ChildItem -Path "C:\" -Filter "Lers.Client.exe" -Recurse -ErrorAction SilentlyContinue -Depth 4 | Select-Object -First 1
    if ($found) {
        $clientPath = Split-Path $found.FullName -Parent
        if (Test-Path (Join-Path $clientPath "Lers.Core.dll")) {
            Write-Info "Found: $clientPath"
            return $clientPath
        }
    }

    return $null
}

function Test-DotNetSdk {
    Write-Step "Checking .NET SDK..."

    try {
        $dotnetVersion = & dotnet --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success ".NET SDK: $dotnetVersion"
            return $true
        }
    }
    catch { }

    return $false
}

function Install-DotNetSdk {
    Write-Step "Installing .NET SDK 8.0..."

    # Method 1: winget (Windows 10/11)
    $wingetPath = Get-Command winget -ErrorAction SilentlyContinue
    if ($wingetPath) {
        Write-Info "Installing via winget..."
        try {
            & winget install Microsoft.DotNet.SDK.8 --silent --accept-package-agreements --accept-source-agreements
            if ($LASTEXITCODE -eq 0) {
                # Update PATH
                $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")
                Write-Success ".NET SDK installed via winget"
                return $true
            }
        }
        catch {
            Write-Info "winget failed, trying another method..."
        }
    }

    # Method 2: Download dotnet-install.ps1 from Microsoft
    Write-Info "Downloading dotnet-install.ps1..."
    $installScript = Join-Path $env:TEMP "dotnet-install.ps1"

    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript -UseBasicParsing

        Write-Info "Running installer..."
        & powershell -NoProfile -ExecutionPolicy Bypass -File $installScript -Channel 8.0 -InstallDir "$env:ProgramFiles\dotnet"

        if ($LASTEXITCODE -eq 0) {
            # Add to PATH for current session
            $dotnetPath = "$env:ProgramFiles\dotnet"
            if ($env:Path -notlike "*$dotnetPath*") {
                $env:Path = "$dotnetPath;$env:Path"
            }
            Write-Success ".NET SDK installed"
            return $true
        }
    }
    catch {
        Write-Info "Error: $_"
    }

    # Method 3: Direct installer download
    Write-Info "Downloading SDK installer..."
    $installerPath = Join-Path $env:TEMP "dotnet-sdk-8.0-installer.exe"

    try {
        $installerUrl = "https://download.visualstudio.microsoft.com/download/pr/6902745c-34bd-4d5e-8b8c-d7f8a6245f2e/4c2bf5f0d0f5c391fe0e8a00bd67d1ee/dotnet-sdk-8.0.404-win-x64.exe"
        Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing

        Write-Info "Running installer (admin rights required)..."
        Start-Process -FilePath $installerPath -ArgumentList "/install", "/quiet", "/norestart" -Wait -Verb RunAs

        # Update PATH
        $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")
        Write-Success ".NET SDK installed"
        return $true
    }
    catch {
        Write-Info "Installation error: $_"
    }

    Write-Err "Failed to install .NET SDK automatically"
    Write-Host ""
    Write-Host "Install manually:" -ForegroundColor Yellow
    Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor White
    return $false
}

# ============================================
# MAIN
# ============================================

Write-Header "LersReportGeneratorPlugin Builder"

# Check .NET SDK, install if needed
if (-not (Test-DotNetSdk)) {
    Write-Host ""
    Write-Host ".NET SDK not found." -ForegroundColor Yellow
    Write-Host ""
    $response = Read-Host "Install .NET SDK 8.0 automatically? (Y/n)"

    if ($response -eq "" -or $response -match "^[Yy]") {
        if (-not (Install-DotNetSdk)) {
            exit 1
        }
        # Re-check after installation
        if (-not (Test-DotNetSdk)) {
            Write-Err "SDK installed but not found in PATH"
            Write-Host "Restart terminal and try again" -ForegroundColor Yellow
            exit 1
        }
    }
    else {
        Write-Host ""
        Write-Host "Install .NET SDK manually:" -ForegroundColor Yellow
        Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor White
        exit 1
    }
}

# Find LERS
if (-not $LersClientPath) {
    $LersClientPath = Find-LersPath
}

if (-not $LersClientPath -or -not (Test-Path $LersClientPath)) {
    Write-Err "LERS not found!"
    Write-Host ""
    Write-Host "Specify path manually:" -ForegroundColor Yellow
    Write-Host "  .\install.ps1 -LersClientPath 'C:\Program Files\LERS\Client'" -ForegroundColor White
    exit 1
}

# Check Lers.Core.dll
$LersCoreDll = Join-Path $LersClientPath "Lers.Core.dll"
if (-not (Test-Path $LersCoreDll)) {
    Write-Err "Lers.Core.dll not found in $LersClientPath"
    exit 1
}

# LERS info
$LersVersion = (Get-Item $LersCoreDll).VersionInfo.FileVersion
Write-Success "LERS version: $LersVersion"
Write-Info "Path: $LersClientPath"

# Update version if needed
Write-Step "Checking version..."
Update-VersionIfNeeded
$version = Get-CurrentVersion
Write-Info "Current version: $($version.Major).$($version.Minor).$($version.Patch).$($version.Build)"

# Detect DevExpress version
Write-Step "Detecting DevExpress version..."
$devExpressDll = Get-ChildItem -Path $LersClientPath -Filter "DevExpress.Data.v*.dll" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $devExpressDll) {
    Write-Err "DevExpress DLL not found in $LersClientPath"
    exit 1
}

# Extract version from filename: DevExpress.Data.v24.1.dll -> v24.1
$DevExpressVersion = $devExpressDll.Name -replace "DevExpress\.Data\.(v[\d\.]+)\.dll", '$1'
Write-Success "DevExpress version: $DevExpressVersion"

# Build
Write-Step "Building plugin ($Configuration)..."

$buildArgs = @(
    "build",
    $ScriptDir,
    "-c", $Configuration,
    "/p:LersClientPath=`"$LersClientPath`"",
    "/p:DevExpressVersion=$DevExpressVersion"
)

$buildResult = & dotnet @buildArgs 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Err "Build failed!"
    Write-Host $buildResult
    exit 1
}

Write-Success "Build successful!"

$OutputDll = Join-Path $ScriptDir "bin\$Configuration\net48\LersReportGeneratorPlugin.dll"
if (Test-Path $OutputDll) {
    $dllInfo = Get-Item $OutputDll
    Write-Info "DLL: $OutputDll"
    Write-Info "Size: $([math]::Round($dllInfo.Length / 1KB, 1)) KB"
}

# Install plugin
if (-not $BuildOnly) {
    Write-Step "Installing plugin..."

    $PluginsDir = Join-Path $LersClientPath "Plugins"
    $PluginDir = Join-Path $PluginsDir "LersReportGeneratorPlugin"

    # Create Plugins folder if not exists
    if (-not (Test-Path $PluginsDir)) {
        New-Item -ItemType Directory -Path $PluginsDir -Force | Out-Null
        Write-Info "Created folder: $PluginsDir"
    }

    # Create plugin folder
    if (-not (Test-Path $PluginDir)) {
        New-Item -ItemType Directory -Path $PluginDir -Force | Out-Null
        Write-Info "Created folder: $PluginDir"
    }

    # Backup existing DLL
    $TargetDll = Join-Path $PluginDir "LersReportGeneratorPlugin.dll"
    if (Test-Path $TargetDll) {
        $backupName = "LersReportGeneratorPlugin.dll.bak_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
        $backupPath = Join-Path $PluginDir $backupName
        Copy-Item $TargetDll $backupPath -Force
        Write-Info "Backup: $backupName"
    }

    # Copy plugin DLL (single file, no external dependencies)
    try {
        Copy-Item $OutputDll $PluginDir -Force
        Write-Success "Installed: $TargetDll"
    }
    catch {
        Write-Err "Failed to copy DLL!"
        Write-Host ""
        Write-Host "LERS may be running. Close ARM operator and try again." -ForegroundColor Yellow
        exit 1
    }

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Installation completed!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "1. Open LERS ARM operator" -ForegroundColor Yellow
    Write-Host "2. Administration -> External modules -> Add from file" -ForegroundColor Yellow
    Write-Host "3. Select file:" -ForegroundColor Yellow
    Write-Host "   $TargetDll" -ForegroundColor White
    Write-Host ""
    Write-Host "After installation, plugin will appear in menu:" -ForegroundColor Cyan
    Write-Host "Service -> Report Generator" -ForegroundColor Cyan
}

Write-Header "Done!"
