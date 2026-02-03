# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
