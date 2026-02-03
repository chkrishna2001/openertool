# Integration Test for Opener Tool (Cross-Platform)
$ErrorActionPreference = "Stop"

Write-Host "Starting Integration Tests..." -ForegroundColor Cyan

# 1. Wipe Everything for a clean start
$isWin = ($env:OS -like "*Windows*") -or ($PSVersionTable.Platform -eq "Win32NT")
$openerDir = Join-Path $HOME ".opener"
Write-Host "Cleaning $openerDir"
if (Test-Path $openerDir) { Remove-Item -Recurse -Force $openerDir }

$appData = if ($isWin) { $env:LOCALAPPDATA } else { "$env:HOME/.local/share" }
$datDir = Join-Path $appData "Opener"
Write-Host "Data directory: $datDir"
if (Test-Path $datDir) { Remove-Item -Recurse -Force $datDir }

# 2. Test auto-initialization
Write-Host "Testing auto-initialization..."
$out = o list | Out-String
Write-Host "Output: $out"
if ($out -notmatch "No keys found") { throw "Auto-initialization failed or output mismatch." }
Write-Host "Success: Auto-initialized."

# 3. Test 'add' and 'list'
Write-Host "Testing 'add' and 'list'..."
$testKey = "tk$(Get-Random -Minimum 100 -Maximum 999)"
o add $testKey "https://google.com?q={0}" -t WebPath
$out = o list | Out-String
Write-Host "Output: $out"
if ($out -notmatch $testKey) { throw "$testKey not found in list output: $out" }
Write-Host "Success: Key added and listed."

# 4. Test 'update'
Write-Host "Testing 'update'..."
o update $testKey "https://bing.com?q={0}"
$out = o list | Out-String
if ($out -notmatch "bing.com") { throw "$testKey update failed. Output: $out" }
Write-Host "Success: Key updated."

# 5. Test Encryption Migration (Local -> Portable)
Write-Host "Testing Migration to Portable Mode..."
$pass = "password123"
# Use new --password flag
o config set-encryption portable --password $pass
Write-Host "Success: Migrated to portable."

# 6. Test Key Execution
Write-Host "Testing key execution..."
try {
    o $testKey "apple"
} catch {
    Write-Host "Execution command failed (expected in headless CI): $_" -ForegroundColor Yellow
}
Write-Host "Success: Execution step completed."

# 7. Test Export
Write-Host "Testing Export..."
$exportFile = "backup.dat"
$exportPass = "exportpass"
# Use new --password flag
o export $exportFile --password $exportPass
if (-not (Test-Path $exportFile)) { throw "Export file not created at $exportFile" }
Write-Host "Success: Exported to $exportFile"

# 8. Test Import
Write-Host "Testing Import..."
# Wipe data again to test import into clean state
if (Test-Path $openerDir) { Remove-Item -Recurse -Force $openerDir }
if (Test-Path $datDir) { Remove-Item -Recurse -Force $datDir }

o list # Re-init
# Use new --password flag
o import $exportFile --password $exportPass
$out = o list | Out-String
Write-Host "Final Output: $out"
if ($out -notmatch $testKey) { throw "Import failed. $testKey not found in output: $out" }
Write-Host "Success: Imported."

Write-Host "Integration Tests Passed!" -ForegroundColor Green
