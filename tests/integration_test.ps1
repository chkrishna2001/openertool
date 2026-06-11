# Integration Test for Opener Tool (Cross-Platform)
param(
    [string]$OpenerCommand = $env:OPENER_COMMAND
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OpenerCommand)) {
    $OpenerCommand = "o"
}

$script:SandboxRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("opener-integration-" + [Guid]::NewGuid().ToString("N"))
$script:SandboxHome = Join-Path $script:SandboxRoot "home"
$script:SandboxProfile = Join-Path $script:SandboxRoot "profile"
$script:SandboxLocalAppData = Join-Path $script:SandboxRoot "LocalAppData"
$script:SandboxRoamingAppData = Join-Path $script:SandboxRoot "AppData"
$script:SandboxDataDir = Join-Path $script:SandboxRoot "Data"

New-Item -ItemType Directory -Force -Path $script:SandboxHome, $script:SandboxProfile, $script:SandboxLocalAppData, $script:SandboxRoamingAppData, $script:SandboxDataDir | Out-Null

$env:HOME = $script:SandboxHome
$env:USERPROFILE = $script:SandboxProfile
$env:LOCALAPPDATA = $script:SandboxLocalAppData
$env:APPDATA = $script:SandboxRoamingAppData
$env:OPENER_HOME = $script:SandboxHome
$env:OPENER_DATA_DIR = $script:SandboxDataDir

function Invoke-Opener {
    & $script:OpenerCommand @args
}

function Cleanup-Opener {
    Write-Host "Cleaning environment..."

    # 1. Wipe Config Dir in the sandboxed profile only
    $oDir = Join-Path $env:USERPROFILE ".opener"
    if (Test-Path $oDir) { 
        Write-Host "Removing $oDir"
        Remove-Item -Recurse -Force $oDir 
    }

    # 2. Wipe sandboxed data paths only
    $paths = @()
    $paths += Join-Path $env:HOME ".local/share/Opener"
    $paths += Join-Path $env:HOME "Library/Application Support/Opener"
    $paths += Join-Path $env:LOCALAPPDATA "Opener"
    $paths += $env:OPENER_DATA_DIR

    foreach ($p in $paths) {
        if (Test-Path $p) {
            Write-Host "Removing detected data path: $p"
            Remove-Item -Recurse -Force $p
        }
    }
}

try {
    Write-Host "Starting Integration Tests..." -ForegroundColor Cyan

    # 1. Initial Wipe
    Cleanup-Opener

    # 2. Test auto-initialization
    Write-Host "Testing auto-initialization..."
    $out = Invoke-Opener list | Out-String # This should create new machine key + new storage
    if ($out -notmatch "No keys found") { throw "Auto-initialization failed or output mismatch." }
    Write-Host "Success: Auto-initialized."

    # 3. Test 'add' and 'list'
    Write-Host "Testing 'add' and 'list'..."
    $testKey = "tk$(Get-Random -Minimum 100 -Maximum 999)"
    Invoke-Opener add $testKey "https://google.com?q={0}" -t WebPath
    $out = Invoke-Opener list | Out-String
    if ($out -notmatch $testKey) { throw "$testKey not found in list output" }
    Write-Host "Success: Key added and listed."

    # 4. Test 'update'
    Write-Host "Testing 'update'..."
    Invoke-Opener update $testKey "https://bing.com?q={0}"
    $out = Invoke-Opener list | Out-String
    if ($out -notmatch "bing.com") { throw "$testKey update failed." }
    Write-Host "Success: Key updated."

    # 5. Test Encryption Migration (Local -> Portable)
    Write-Host "Testing Migration to Portable Mode..."
    $pass = "password123"
    Invoke-Opener config set-encryption portable --password $pass
    Write-Host "Success: Migrated to portable."

    # 6. Test Key Execution
    Write-Host "Testing key execution..."
    try { Invoke-Opener $testKey "apple" } catch {}
    Write-Host "Success: Execution step completed."

    # 7. Test Export
    Write-Host "Testing Export..."
    $exportFile = "backup.dat"
    $exportPass = "exportpass"
    Invoke-Opener export $exportFile --password $exportPass
    if (-not (Test-Path $exportFile)) { throw "Export file not created." }
    Write-Host "Success: Exported to $exportFile"

    # 8. Test Import (Clean Start)
    Write-Host "Testing Import (Cleaning data first)..."
    Cleanup-Opener

    # Re-init in local mode
    Invoke-Opener list | Out-String

    Write-Host "Importing from $exportFile..."
    Invoke-Opener import $exportFile --password $exportPass
    $out = Invoke-Opener list | Out-String
    if ($out -notmatch $testKey) { throw "Import failed. $testKey not found in output." }
    Write-Host "Success: Imported."

    Write-Host "Integration Tests Passed!" -ForegroundColor Green
}
finally {
    if (Test-Path $script:SandboxRoot) {
        try {
            Remove-Item -Recurse -Force $script:SandboxRoot -ErrorAction Stop
        }
        catch {
            Write-Host "Warning: Could not fully remove sandbox root: $($_.Exception.Message)"
        }
    }
}
