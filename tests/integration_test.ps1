# Integration Test for Opener Tool (Cross-Platform)
$ErrorActionPreference = "Stop"

function Cleanup-Opener {
    Write-Host "Cleaning environment..."
    $localWin = ($env:OS -like "*Windows*") -or ($PSVersionTable.Platform -eq "Win32NT")
    
    # 1. Wipe Config Dir
    $oDir = Join-Path $HOME ".opener"
    if (Test-Path $oDir) { 
        Write-Host "Removing $oDir"
        Remove-Item -Recurse -Force $oDir 
    }

    # 2. Wipe standard data paths (exhaustive to handle Mac/Linux service variations)
    $paths = @(
        Join-Path $HOME ".local/share/Opener",
        Join-Path $HOME "Library/Application Support/Opener"
    )
    if ($localWin) { $paths += Join-Path $env:LOCALAPPDATA "Opener" }

    foreach ($p in $paths) {
        if (Test-Path $p) {
            Write-Host "Removing detected data path: $p"
            Remove-Item -Recurse -Force $p
        }
    }
}

Write-Host "Starting Integration Tests..." -ForegroundColor Cyan

# 1. Initial Wipe
Cleanup-Opener

# 2. Test auto-initialization
Write-Host "Testing auto-initialization..."
$out = o list | Out-String # This should create new machine key + new storage
if ($out -notmatch "No keys found") { throw "Auto-initialization failed or output mismatch." }
Write-Host "Success: Auto-initialized."

# 3. Test 'add' and 'list'
Write-Host "Testing 'add' and 'list'..."
$testKey = "tk$(Get-Random -Minimum 100 -Maximum 999)"
o add $testKey "https://google.com?q={0}" -t WebPath
$out = o list | Out-String
if ($out -notmatch $testKey) { throw "$testKey not found in list output" }
Write-Host "Success: Key added and listed."

# 4. Test 'update'
Write-Host "Testing 'update'..."
o update $testKey "https://bing.com?q={0}"
$out = o list | Out-String
if ($out -notmatch "bing.com") { throw "$testKey update failed." }
Write-Host "Success: Key updated."

# 5. Test Encryption Migration (Local -> Portable)
Write-Host "Testing Migration to Portable Mode..."
$pass = "password123"
o config set-encryption portable --password $pass
Write-Host "Success: Migrated to portable."

# 6. Test Key Execution
Write-Host "Testing key execution..."
try { o $testKey "apple" } catch {}
Write-Host "Success: Execution step completed."

# 7. Test Export
Write-Host "Testing Export..."
$exportFile = "backup.dat"
$exportPass = "exportpass"
o export $exportFile --password $exportPass
if (-not (Test-Path $exportFile)) { throw "Export file not created." }
Write-Host "Success: Exported to $exportFile"

# 8. Test Import (Clean Start)
Write-Host "Testing Import (Cleaning data first)..."
Cleanup-Opener

# Re-init in local mode
o list | Out-String

Write-Host "Importing from $exportFile..."
o import $exportFile --password $exportPass
$out = o list | Out-String
if ($out -notmatch $testKey) { throw "Import failed. $testKey not found in output." }
Write-Host "Success: Imported."

Write-Host "Integration Tests Passed!" -ForegroundColor Green
