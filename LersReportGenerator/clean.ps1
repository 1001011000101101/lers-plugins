# Clean build artifacts from all projects
# Removes bin/, obj/, and .build-hash files

param(
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"
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
    Write-Host "[*] $Text" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Text)
    Write-Host "[+] $Text" -ForegroundColor Green
}

function Get-FolderSize {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return 0 }

    $size = 0
    Get-ChildItem -Path $Path -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        $size += $_.Length
    }
    return $size
}

function Format-FileSize {
    param([long]$Bytes)

    if ($Bytes -ge 1GB) {
        return "{0:N2} GB" -f ($Bytes / 1GB)
    }
    elseif ($Bytes -ge 1MB) {
        return "{0:N2} MB" -f ($Bytes / 1MB)
    }
    elseif ($Bytes -ge 1KB) {
        return "{0:N2} KB" -f ($Bytes / 1KB)
    }
    else {
        return "$Bytes bytes"
    }
}

function Remove-BuildArtifacts {
    param([string]$ProjectPath, [string]$ProjectName)

    Write-Step "Cleaning $ProjectName..."

    $totalSize = 0
    $deletedCount = 0

    # bin/ folders
    $binFolders = Get-ChildItem -Path $ProjectPath -Filter "bin" -Recurse -Directory -ErrorAction SilentlyContinue
    foreach ($folder in $binFolders) {
        $size = Get-FolderSize -Path $folder.FullName
        $totalSize += $size

        if ($Verbose) {
            Write-Host "  Removing: $($folder.FullName) ($(Format-FileSize $size))" -ForegroundColor Gray
        }

        Remove-Item -Path $folder.FullName -Recurse -Force -ErrorAction SilentlyContinue
        $deletedCount++
    }

    # obj/ folders
    $objFolders = Get-ChildItem -Path $ProjectPath -Filter "obj" -Recurse -Directory -ErrorAction SilentlyContinue
    foreach ($folder in $objFolders) {
        $size = Get-FolderSize -Path $folder.FullName
        $totalSize += $size

        if ($Verbose) {
            Write-Host "  Removing: $($folder.FullName) ($(Format-FileSize $size))" -ForegroundColor Gray
        }

        Remove-Item -Path $folder.FullName -Recurse -Force -ErrorAction SilentlyContinue
        $deletedCount++
    }

    # .build-hash files
    $hashFiles = Get-ChildItem -Path $ProjectPath -Filter ".build-hash" -Recurse -File -ErrorAction SilentlyContinue
    foreach ($file in $hashFiles) {
        $totalSize += $file.Length

        if ($Verbose) {
            Write-Host "  Removing: $($file.FullName)" -ForegroundColor Gray
        }

        Remove-Item -Path $file.FullName -Force -ErrorAction SilentlyContinue
        $deletedCount++
    }

    if ($deletedCount -gt 0) {
        $sizeStr = Format-FileSize $totalSize
        Write-Success "${ProjectName}: removed $deletedCount items ($sizeStr)"
    }
    else {
        Write-Host "  ${ProjectName}: already clean" -ForegroundColor Gray
    }

    return $totalSize
}

Write-Header "Clean Build Artifacts"

$totalFreed = 0

# Clean LersReportCommon
$commonPath = Join-Path $ScriptDir "LersReportCommon"
if (Test-Path $commonPath) {
    $totalFreed += Remove-BuildArtifacts -ProjectPath $commonPath -ProjectName "LersReportCommon"
}

# Clean LersReportGeneratorPlugin
$pluginPath = Join-Path $ScriptDir "LersReportGeneratorPlugin"
if (Test-Path $pluginPath) {
    $totalFreed += Remove-BuildArtifacts -ProjectPath $pluginPath -ProjectName "LersReportGeneratorPlugin"
}

# Clean LersReportProxy
$proxyPath = Join-Path $ScriptDir "LersReportProxy"
if (Test-Path $proxyPath) {
    $totalFreed += Remove-BuildArtifacts -ProjectPath $proxyPath -ProjectName "LersReportProxy"
}

Write-Host ""
Write-Header "Done!"
Write-Host "Total space freed: $(Format-FileSize $totalFreed)" -ForegroundColor Green
