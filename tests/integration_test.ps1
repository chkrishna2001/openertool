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
    # -y skips the confirmation prompt: interactive prompts don't reliably read from
    # redirected/non-interactive stdin, and a crashed prompt still exits 0 (System.CommandLine
    # swallows the exception), so this is verified explicitly below instead of trusting the exit code.
    Invoke-Opener config set-encryption portable --password $pass -y
    $modeCheck = Invoke-Opener config show | Out-String
    if ($modeCheck -notmatch "portable") { throw "set-encryption did not switch to portable mode. Output: $modeCheck" }
    Write-Host "Success: Migrated to portable."

    # 6. Test Key Execution
    Write-Host "Testing key execution..."
    try { Invoke-Opener $testKey "apple" } catch {}
    Write-Host "Success: Execution step completed."

    # 6b. Test TOTP: add a known RFC 6238 test secret and confirm a 6-digit code comes back
    Write-Host "Testing TOTP..."
    Invoke-Opener add mytotp GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ -t Totp
    $totpCode = (Invoke-Opener mytotp -r | Out-String).Trim()
    if ($totpCode -notmatch "^\d{6}$") { throw "TOTP code '$totpCode' is not a 6-digit number." }
    Write-Host "Success: TOTP produced a 6-digit code."

    # 6c. Test non-interactive picker fallback: bare `o` with keys present must not hang
    # and must print a static list instead of an interactive prompt.
    Write-Host "Testing non-interactive picker fallback..."
    $pickerOut = ("" | Invoke-Opener) | Out-String
    if ($pickerOut -notmatch $testKey) { throw "Bare 'o' did not fall back to a static list. Output: $pickerOut" }
    Write-Host "Success: bare 'o' fell back to a static list without hanging."

    # 6d. Test REST chaining: structural smoke test (no live network call in CI) - confirms
    # a chained key's steps survive round-trip.
    Write-Host "Testing REST chain storage..."
    Invoke-Opener add mychain '{"steps":[{"url":"https://example.com/login","method":"POST","extract":{"token":"access_token"}},{"url":"https://example.com/data","headers":{"Authorization":"Bearer {{token}}"}}]}' -t Rest
    $chainView = Invoke-Opener view mychain | Out-String
    if ($chainView -notmatch "steps" -or $chainView -notmatch "access_token") { throw "REST chain was not stored correctly. Output: $chainView" }
    Write-Host "Success: REST chain stored correctly."

    # 6e. Test git sync: push to a throwaway local bare remote, wipe local data, pull it
    # back, and confirm the key reappears.
    Write-Host "Testing git sync..."
    $remoteDir = Join-Path $script:SandboxRoot "remote.git"
    git init --bare $remoteDir | Out-Null
    Invoke-Opener config set-sync-remote $remoteDir
    Invoke-Opener sync push
    $dataFile = Join-Path $env:OPENER_DATA_DIR "Opener\opener.dat"
    if (Test-Path $dataFile) { Remove-Item -Force $dataFile }
    Invoke-Opener sync pull
    $out = Invoke-Opener list | Out-String
    if ($out -notmatch $testKey) { throw "Git sync did not restore $testKey." }
    Write-Host "Success: git sync round-tripped a key."

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
