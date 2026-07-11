$ErrorActionPreference = 'Stop'

# Repository configuration
$repo = "chkrishna2001/openertool"
$binaryName = "o.exe"

# Detect Architecture
$arch = $env:PROCESSOR_ARCHITECTURE
if ($arch -eq 'AMD64') {
    $archName = 'x64'
} elseif ($arch -eq 'ARM64') {
    $archName = 'arm64'
} else {
    Write-Error "Unsupported architecture: $arch"
    exit 1
}

$assetName = "opener-windows-$archName.zip"

Write-Host "Checking latest release version from GitHub..."
$latestReleaseUrl = "https://api.github.com/repos/$repo/releases/latest"
try {
    $response = Invoke-RestMethod -Uri $latestReleaseUrl -UseBasicParsing
    $versionTag = $response.tag_name
}
catch {
    Write-Error "Failed to check the latest version from GitHub: $_"
    exit 1
}

if (-not $versionTag) {
    Write-Error "Could not retrieve latest version tag."
    exit 1
}

Write-Host "Latest version is $versionTag"
$downloadUrl = "https://github.com/$repo/releases/download/$versionTag/$assetName"

# Set up installation directory in user profile
$installDir = Join-Path $env:USERPROFILE ".opener"
if (!(Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir | Out-Null
}

$tempZip = Join-Path $env:TEMP "$assetName"

Write-Host "Downloading $downloadUrl..."
try {
    Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing
}
catch {
    Write-Error "Failed to download release asset: $_"
    exit 1
}

Write-Host "Extracting to $installDir..."
try {
    # Extract the zip file contents
    Expand-Archive -Path $tempZip -DestinationPath $installDir -Force
}
catch {
    Write-Error "Failed to extract zip archive: $_"
    exit 1
}
finally {
    if (Test-Path $tempZip) {
        Remove-Item $tempZip -Force
    }
}

# Verify binary exists after extraction
$binaryPath = Join-Path $installDir $binaryName
if (!(Test-Path $binaryPath)) {
    Write-Error "Installation failed: $binaryName not found in extracted folder."
    exit 1
}

# Add to User PATH if not already present
$userPath = [Environment]::GetEnvironmentVariable("Path", [EnvironmentVariableTarget]::User)
$pathDirs = $userPath -split ';'
if ($pathDirs -notcontains $installDir) {
    Write-Host "Adding $installDir to User PATH..."
    $newUserPath = $userPath + ";" + $installDir
    [Environment]::SetEnvironmentVariable("Path", $newUserPath, [EnvironmentVariableTarget]::User)
    # Refresh current shell path
    $env:Path = $env:Path + ";" + $installDir
}

Write-Host "Opener has been successfully installed!"
Write-Host "You can now run: o --help"
Write-Host "Note: If 'o' is not recognized, please restart your terminal window."
