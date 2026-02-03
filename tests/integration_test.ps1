# Integration Test for Opener Tool
$ErrorActionPreference = "Stop"

Write-Host "Starting Integration Tests..." -ForegroundColor Cyan

# 1. Verify fresh start (Auto-initialization test)
$appData = if ($IsWindows) { $env:LOCALAPPDATA } else { "$env:HOME/.local/share" }
$datFile = Join-Path $appData "Opener/opener.dat"
if (Test-Path $datFile) { Remove-Item $datFile }

Write-Host "Testing 'list' on fresh install..."
o list
Write-Host "Success: Auto-initialized."

# 2. Test 'add' and 'list'
Write-Host "Testing 'add' and 'list'..."
o add testkey "https://google.com?q={0}" -t WebPath
$list = o list
if ($list -notmatch "testkey") { throw "testkey not found in list" }
Write-Host "Success: Key added and listed."

# 3. Test 'update'
Write-Host "Testing 'update'..."
o update testkey "https://bing.com?q={0}"
$list = o list
if ($list -notmatch "bing.com") { throw "testkey update failed" }
Write-Host "Success: Key updated."

# 4. Test Encryption Migration (Local -> Portable)
Write-Host "Testing Migration to Portable Mode..."
$pass = "password123"
# Pipe password twice (input + confirmation) with newlines
"$pass`n$pass" | o config set-encryption portable
Write-Host "Success: Migrated to portable."

# 5. Test Portable execution
Write-Host "Testing key execution in portable mode..."
o testkey "apple"
Write-Host "Success: Key executed."

# 6. Test Export
Write-Host "Testing Export..."
$exportFile = "backup.dat"
$exportPass = "exportpass"
"$exportPass`n$exportPass" | o export $exportFile
if (-not (Test-Path $exportFile)) { throw "Export file not created" }
Write-Host "Success: Exported."

# 7. Test Import
Write-Host "Testing Import..."
if (Test-Path $datFile) { Remove-Item $datFile } # Force clean current data
o list # Should show no keys
"$exportPass" | o import $exportFile
$list = o list
if ($list -notmatch "testkey") { throw "Import failed" }
Write-Host "Success: Imported."

Write-Host "Integration Tests Passed!" -ForegroundColor Green
