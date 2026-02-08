<#
.SYNOPSIS
    Build and install LERS Report Proxy service

.DESCRIPTION
    Builds the project (if needed) and installs Windows Service.
    Configures port, firewall rule and URL ACL.

.PARAMETER Port
    HTTP server port (default 5377)

.PARAMETER LersServerUrl
    Local LERS server URL (default localhost)

.PARAMETER LersClientPath
    Path to LERS Client folder with libraries (default "C:\Program Files\LERS\Client")

.PARAMETER Uninstall
    Uninstall the service

.PARAMETER Rebuild
    Force rebuild before installing

.EXAMPLE
    .\install.ps1                    # Build (if needed) + install
    .\install.ps1 -Rebuild           # Force rebuild + install
    .\install.ps1 -SkipBuild         # Install from existing build (offline mode)
    .\install.ps1 -Port 8080         # Custom port
    .\install.ps1 -Uninstall         # Remove service
#>

param(
    [int]$Port = 5377,
    [string]$LersServerUrl = "localhost",
    [string]$LersClientPath = "C:\Program Files\LERS\Client",
    [switch]$Uninstall,
    [switch]$Rebuild,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$ServiceName = "LersReportProxy"
$ServiceDisplayName = "LERS Report Proxy"
$ServiceDescription = "Proxy service for LERS report generation"
$InstallPath = "C:\Program Files\LersReportProxy"
$ExePath = "$InstallPath\LersReportProxy.exe"

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host "=== $Text ===" -ForegroundColor Cyan
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

function Test-DotNetSdk {
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

    # Method 1: winget
    $wingetPath = Get-Command winget -ErrorAction SilentlyContinue
    if ($wingetPath) {
        Write-Info "Installing via winget..."
        try {
            & winget install Microsoft.DotNet.SDK.8 --silent --accept-package-agreements --accept-source-agreements
            if ($LASTEXITCODE -eq 0) {
                $env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")
                Write-Success ".NET SDK installed via winget"
                return $true
            }
        }
        catch {
            Write-Info "winget failed, trying another method..."
        }
    }

    # Method 2: dotnet-install.ps1
    Write-Info "Downloading dotnet-install.ps1..."
    $installScript = Join-Path $env:TEMP "dotnet-install.ps1"

    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript -UseBasicParsing

        Write-Info "Running installer..."
        & powershell -NoProfile -ExecutionPolicy Bypass -File $installScript -Channel 8.0 -InstallDir "$env:ProgramFiles\dotnet"

        if ($LASTEXITCODE -eq 0) {
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

    Write-Err "Failed to install .NET SDK automatically"
    Write-Host ""
    Write-Host "Install manually:" -ForegroundColor Yellow
    Write-Host "  https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor White
    return $false
}

function Find-LersPath {
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
            Write-Info "Found at: $path"
            return $path
        }
    }

    # 3. Search
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

function Test-Admin {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Check admin rights
if (-not (Test-Admin)) {
    Write-Host "Error: Administrator rights required!" -ForegroundColor Red
    Write-Host "Run PowerShell as Administrator." -ForegroundColor Yellow
    exit 1
}

# Uninstall
if ($Uninstall) {
    Write-Header "Uninstalling $ServiceName"

    # Stop service
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($service) {
        if ($service.Status -eq "Running") {
            Write-Host "Stopping service..."
            Stop-Service -Name $ServiceName -Force
        }
        Write-Host "Removing service..."
        sc.exe delete $ServiceName | Out-Null
    }

    # Remove firewall rule
    $firewallRule = Get-NetFirewallRule -DisplayName $ServiceDisplayName -ErrorAction SilentlyContinue
    if ($firewallRule) {
        Write-Host "Removing firewall rule..."
        Remove-NetFirewallRule -DisplayName $ServiceDisplayName
    }

    # Remove URL ACL
    Write-Host "Removing URL ACL..."
    $null = netsh http delete urlacl url=http://+:$Port/ 2>&1

    Write-Host "Service removed." -ForegroundColor Green
    exit 0
}

# Install
Write-Header "Installing $ServiceName"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SourceExe = "$ScriptDir\bin\Release\net48\LersReportProxy.exe"

# Check .NET SDK
Write-Step "Checking .NET SDK..."
if (-not (Test-DotNetSdk)) {
    Write-Host ""
    Write-Host ".NET SDK not found." -ForegroundColor Yellow
    Write-Host ""
    $response = Read-Host "Install .NET SDK 8.0 automatically? (Y/n)"

    if ($response -eq "" -or $response -match "^[Yy]") {
        if (-not (Install-DotNetSdk)) {
            exit 1
        }
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
Write-Step "Searching for LERS..."
if ($LersClientPath -eq "C:\Program Files\LERS\Client" -and -not (Test-Path "$LersClientPath\Lers.Core.dll")) {
    $detectedPath = Find-LersPath
    if ($detectedPath) {
        $LersClientPath = $detectedPath
    }
}

if (-not (Test-Path "$LersClientPath\Lers.Core.dll")) {
    Write-Err "Lers.Core.dll not found in $LersClientPath"
    Write-Host "Specify correct path: .\install.ps1 -LersClientPath 'D:\LERS\Client'" -ForegroundColor Yellow
    exit 1
}
Write-Success "LERS libraries: $LersClientPath"

# Fix corrupted NuGet.Config (common after fresh SDK install)
$nugetConfigPath = Join-Path $env:APPDATA "NuGet\NuGet.Config"
if (Test-Path $nugetConfigPath) {
    $content = Get-Content $nugetConfigPath -Raw -ErrorAction SilentlyContinue
    if (-not $content -or $content.Trim().Length -eq 0 -or $content -notmatch '<configuration') {
        Write-Info "Fixing corrupted NuGet.Config..."
        $defaultConfig = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
'@
        Set-Content $nugetConfigPath -Value $defaultConfig -Encoding UTF8
        Write-Success "NuGet.Config restored"
    }
}

# Check NuGet access
function Test-NuGetAccess {
    Write-Step "Checking NuGet access..."

    # Test 1: Web access (browser)
    try {
        $response = Invoke-WebRequest -Uri "https://api.nuget.org/v3/index.json" -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Success "Web access to api.nuget.org: OK"
        }
    }
    catch {
        Write-Err "Web access to api.nuget.org: FAILED"
        Write-Info $_.Exception.Message
        return $false
    }

    # Test 2: NuGet CLI access
    Write-Info "Testing NuGet CLI access..."
    $testProject = "$env:TEMP\nuget-test-$(Get-Random)"
    try {
        New-Item -ItemType Directory -Path $testProject -Force | Out-Null
        Set-Content "$testProject\test.csproj" -Value @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
'@
        $restoreOutput = & dotnet restore "$testProject\test.csproj" --no-cache 2>&1
        $restoreSuccess = $LASTEXITCODE -eq 0

        Remove-Item $testProject -Recurse -Force -ErrorAction SilentlyContinue

        if ($restoreSuccess) {
            Write-Success "NuGet CLI access: OK"
            return $true
        }
        else {
            Write-Err "NuGet CLI access: FAILED"
            Write-Info "Restore output:"
            $restoreOutput | ForEach-Object { Write-Info $_ }
            return $false
        }
    }
    catch {
        Write-Err "NuGet test failed: $($_.Exception.Message)"
        Remove-Item $testProject -Recurse -Force -ErrorAction SilentlyContinue
        return $false
    }
}

# Fix NuGet issues
function Fix-NuGetIssues {
    Write-Step "Attempting to fix NuGet issues..."

    # Fix 1: Clear NuGet cache
    Write-Info "Clearing NuGet cache..."
    & dotnet nuget locals all --clear 2>&1 | Out-Null

    # Fix 2: Enable TLS 1.2
    Write-Info "Enabling TLS 1.2..."
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    # Fix 3: Reset NuGet config
    $nugetConfigPath = "$env:APPDATA\NuGet\NuGet.Config"
    if (Test-Path $nugetConfigPath) {
        Write-Info "Resetting NuGet.Config..."
        Remove-Item $nugetConfigPath -Force
    }

    Write-Success "NuGet fixes applied"
}

# Build
function Build-Project {
    Write-Step "Building..."
    & dotnet build "$ScriptDir" -c Release -p:LersDllPath="$LersClientPath"
    if ($LASTEXITCODE -ne 0) {
        Write-Err "Build failed"
        exit 1
    }
    Write-Success "Build completed"
}

# Check if build is needed
$SourceDir = "$ScriptDir\bin\Release\net48"
$SourceExe = "$SourceDir\LersReportProxy.exe"
$needsBuild = $true

if ($SkipBuild) {
    Write-Info "Build skipped (-SkipBuild flag)"
    $needsBuild = $false
}
elseif (Test-Path $SourceExe) {
    Write-Info "Existing build found: $SourceExe"
    if (-not $Rebuild) {
        Write-Info "Using existing build (use -Rebuild to force rebuild)"
        $needsBuild = $false
    }
}

# Build if needed
if ($needsBuild) {
    # Check NuGet access before building
    $hasNuGetAccess = Test-NuGetAccess

    if (-not $hasNuGetAccess) {
        Write-Host ""
        Write-Host "Attempting to fix NuGet issues..." -ForegroundColor Yellow
        Fix-NuGetIssues

        Write-Host ""
        Write-Host "Retrying NuGet access..." -ForegroundColor Yellow
        $hasNuGetAccess = Test-NuGetAccess

        if (-not $hasNuGetAccess) {
            Write-Err "Cannot build: NuGet still not working after fixes"
            Write-Host ""
            Write-Host "SOLUTIONS:" -ForegroundColor Yellow
            Write-Host "  1. Build on local machine and copy files" -ForegroundColor Gray
            Write-Host "  2. Configure NuGet proxy: nuget config -set http_proxy=..." -ForegroundColor Gray
            Write-Host "  3. Check antivirus/firewall blocking dotnet.exe" -ForegroundColor Gray
            Write-Host "  4. Update .NET SDK to latest version" -ForegroundColor Gray
            Write-Host ""
            Write-Host "If you already have built files, use: .\install.ps1 -SkipBuild" -ForegroundColor Cyan
            exit 1
        }
    }

    Build-Project
}

if (-not (Test-Path $SourceExe)) {
    Write-Err "$SourceExe not found"
    Write-Info "Please build the project first or copy pre-built files"
    exit 1
}

# Stop existing service if any
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Existing service found, stopping..."
    if ($service.Status -eq "Running") {
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }
    Write-Host "Removing old service..."
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}

# Create install folder
Write-Host "Creating folder $InstallPath..."
if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

# Copy files
Write-Host "Copying files..."
$SourceDir = "$ScriptDir\bin\Release\net48"
Copy-Item "$SourceDir\*" $InstallPath -Recurse -Force

# Copy ALL LERS dependencies (required for .NET Framework)
Write-Host "Copying LERS dependencies..."

# Copy all System.*.dll and Microsoft.*.dll from LERS folder
$patterns = @("System.*.dll", "Microsoft.*.dll")
$copiedCount = 0

foreach ($pattern in $patterns) {
    $files = Get-ChildItem -Path $LersClientPath -Filter $pattern -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        $destPath = Join-Path $InstallPath $file.Name
        # Don't overwrite if already exists with same or newer version
        if (-not (Test-Path $destPath)) {
            Copy-Item $file.FullName $InstallPath -Force
            $copiedCount++
        }
    }
}
Write-Host "  Copied $copiedCount dependency files from LERS" -ForegroundColor Gray

# Create config
Write-Host "Creating configuration..."
$config = @{
    Port = $Port
    LersServerUrl = $LersServerUrl
    LersLibraryPath = $LersClientPath
}
$config | ConvertTo-Json | Set-Content "$InstallPath\config.json"

# Add URL ACL
Write-Host "Configuring URL ACL..."
$null = netsh http add urlacl url=http://+:$Port/ user=Everyone 2>&1

# Add firewall rule
Write-Host "Adding firewall rule..."
$firewallRule = Get-NetFirewallRule -DisplayName $ServiceDisplayName -ErrorAction SilentlyContinue
if ($firewallRule) {
    Remove-NetFirewallRule -DisplayName $ServiceDisplayName
}
New-NetFirewallRule -DisplayName $ServiceDisplayName `
    -Direction Inbound `
    -Protocol TCP `
    -LocalPort $Port `
    -Action Allow `
    -Description "Allow incoming connections to LERS proxy service" | Out-Null

# Create service
Write-Host "Registering Windows Service..."
sc.exe create $ServiceName binPath= "$ExePath" start= auto DisplayName= "$ServiceDisplayName" | Out-Null
sc.exe description $ServiceName "$ServiceDescription" | Out-Null

# Start service
Write-Host "Starting service..."
Start-Service -Name $ServiceName

# Check status
Start-Sleep -Seconds 2
$service = Get-Service -Name $ServiceName
if ($service.Status -eq "Running") {
    Write-Host ""
    Write-Host "Installation completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Service running on port $Port" -ForegroundColor Cyan
    Write-Host "URL: http://localhost:$Port/lersproxy/" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Health check:" -ForegroundColor Yellow
    Write-Host "  curl http://localhost:$Port/lersproxy/health"
    Write-Host "  curl http://localhost:$Port/lersproxy/version"
} else {
    Write-Host "Warning: Service did not start. Check logs." -ForegroundColor Yellow
    Write-Host "Logs: $InstallPath\Logs\" -ForegroundColor Yellow
}
