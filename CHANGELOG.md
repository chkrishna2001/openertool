# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- **Important Finding**: Direct file I/O to OneDrive Files On-Demand fails across all .NET applications due to Windows virtual filesystem layer. This is not a bug in Opener but a platform limitation.
- Removed ineffective retry logic and fallback mechanisms that attempted to work around OneDrive access issues.
- Simplified error messages with guidance to use Microsoft Graph API for reliable OneDrive access.

### To Be Added
- Microsoft Graph API integration for reliable cross-platform OneDrive support.
- Google Drive support via Google Drive API.
- Cloud sync configuration management.

## [1.0.2] - 2026-05-08

### Changed
- Added resilient storage handling for company OneDrive and other inaccessible cloud-synced paths.
- Switched NuGet publishing to a tag-driven release flow with changelog-based release notes.
- Added GitHub Release generation from the matching changelog section.

## [1.0.1] - 2026-02-03

### Added
- **Initial Release** of Opener Tool (`o`).
- Secure CLI-based key-value store.
- **Auto-Initialization**: Storage is now created automatically on first use.
- **Docker Integration Tests**: Added pipeline validation using Docker.
- **Cross-Platform Support**: Partial support for Linux via `portable` mode.
- **Key Types Supported**:
    - `WebPath`: Open URLs with placeholder support.
    - `LocalPath`: Run files and scripts.
    - `Data`: Copy secrets to clipboard.
    - `JsonData`: Store and format JSON.
    - `Rest`: Execute simple REST API calls.
- **TUI**: Rich interactive list using Spectre.Console.
- **Encrypted Storage**:
    - **Local Mode**: Zero-config Windows DPAPI encryption.
    - **Portable Mode**: AES-256 password-based encryption for OneDrive/Sync support.
- **Commands**: `add`, `update`, `delete`, `list`, `config`, `export`, `import`.
- **Global Tool**: Installable via `dotnet tool install -g`.

### Fixed
- Fixed `NullReferenceException` in command binding.
- Hardened null-safety in many core services.
- Added `--password` flags to `config set-encryption`, `export`, and `import` for automation.
