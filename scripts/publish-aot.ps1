# AOT Native Publishing Script for Opener Tool
# Publishes self-contained, AOT-compiled native binaries for multiple platforms

param(
    [string]$OutputDir = "publish-aot",
    [string]$Configuration = "Release",
    [string]$Version = "1.0.1",
    [switch]$LocalOnly = $false
)

$ErrorActionPreference = "Stop"

# Determine local RID
$localRid = if ($IsWindows -or $env:OS -eq "Windows_NT") {
    "win-x64"
} elseif ($IsLinux) {
    "linux-x64"
} elseif ($IsMacOS) {
    "osx-x64"
} else {
    "win-x64"  # default
}

# RIDs: Runtime Identifiers for cross-platform publishing
# For cross-OS compilation, this script should be run in CI/CD environments
$RIDs = if ($LocalOnly) {
    @($localRid)
} else {
    @("win-x64", "linux-x64", "osx-x64")
}

Write-Host "Starting AOT native publishing for Opener Tool..." -ForegroundColor Cyan
Write-Host "Output Directory: $OutputDir" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
if ($LocalOnly) {
    Write-Host "Mode: Local only ($localRid)" -ForegroundColor Cyan
}
Write-Host ""

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

foreach ($rid in $RIDs) {
    Write-Host "Publishing for RID: $rid" -ForegroundColor Green
    
    $outputPath = Join-Path $OutputDir $rid
    
    try {
        # Publish as self-contained, AOT-compiled
        # For multi-targeted projects, we need to specify a target framework
        dotnet publish Opener/Opener.csproj `
            -c $Configuration `
            -f net10.0 `
            -r $rid `
            --self-contained `
            -p:PublishAot=true `
            -p:Version=$Version `
            -o $outputPath
        
        Write-Host "[OK] Successfully published $rid" -ForegroundColor Green
        
        # Get the executable name
        $exeName = if ($rid -like "win-*") { "Opener.exe" } else { "Opener" }
        $exePath = Join-Path $outputPath $exeName
        
        # Check file exists and get size
        if (Test-Path $exePath) {
            $size = (Get-Item $exePath).Length / 1MB
            $sizeRounded = [Math]::Round($size, 2)
            Write-Host "  Executable: $exeName ($sizeRounded MB)" -ForegroundColor Gray
        }
        
        Write-Host ""
    }
    catch {
        Write-Host "[FAIL] Failed to publish $rid : $_" -ForegroundColor Red
        Write-Host ""
    }
}

Write-Host "AOT publishing complete!" -ForegroundColor Cyan
Write-Host "Binaries are in: $OutputDir" -ForegroundColor Gray
Write-Host ""
if ($LocalOnly) {
    Write-Host "To test your binary:" -ForegroundColor Yellow
    if ($localRid -eq "win-x64") {
        Write-Host "  .\publish-aot\win-x64\Opener.exe --help" -ForegroundColor Gray
    } elseif ($localRid -eq "linux-x64") {
        Write-Host "  ./publish-aot/linux-x64/Opener --help" -ForegroundColor Gray
    } else {
        Write-Host "  ./publish-aot/osx-x64/Opener --help" -ForegroundColor Gray
    }
} else {
    Write-Host "Note: Cross-platform AOT compilation requires native toolchains:" -ForegroundColor Yellow
    Write-Host "  - Windows:  C++ Desktop Development workload in Visual Studio" -ForegroundColor Gray
    Write-Host "  - Linux:    GCC/Clang and build-essential" -ForegroundColor Gray
    Write-Host "  - macOS:    Xcode Command Line Tools" -ForegroundColor Gray
    Write-Host "" -ForegroundColor Gray
    Write-Host "For CI/CD cross-platform builds, use GitHub Actions or Docker:" -ForegroundColor Yellow
    Write-Host "  - Run this script on Windows runner for win-x64" -ForegroundColor Gray
    Write-Host "  - Run this script on Linux runner for linux-x64" -ForegroundColor Gray
    Write-Host "  - Run this script on macOS runner for osx-x64" -ForegroundColor Gray
    Write-Host "" -ForegroundColor Gray
    Write-Host "For local testing, use: .\scripts\publish-aot.ps1 -LocalOnly" -ForegroundColor Gray
}
Write-Host ""
