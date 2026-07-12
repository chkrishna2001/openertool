# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-07-12

### Added
- Interactive picker (`o` with no arguments) — a searchable selection prompt over all stored keys, falling back to a static list on non-interactive consoles. Also usable as a quick launcher from Windows Run (Win+R).
- Two-factor code generation (`Totp` key type) — stores a base32 seed or `otpauth://` URI and computes the live RFC 6238 code, compatible with Google Authenticator/Authy.
- Git-based encrypted vault sync: `o sync push/pull/status`, plus `o config set-sync-remote`, `set-sync-token`, `enable-auto-sync`, and `disable-auto-sync`. Pushes/pulls the already-encrypted vault through a git remote instead of a cloud-storage client. SSH remotes use the existing SSH agent; HTTPS remotes use a token stored in a separate OS-keychain slot from the vault's own unlock password. Auto-sync (opt-in) fires a background push after `add`/`update`/`delete`/`import` without failing the triggering command.
- REST request chaining — a `Rest` key's value can be `{ "steps": [...] }`, where each step can `extract` values from its JSON response (dot-path, e.g. `"data.token"` or `"items[0].id"`) for later steps to reference as `{{varName}}` in their `url`, `headers`, or `body`. Enables login-then-call flows (authenticate, capture a token, use it in the next request). A non-final step failing aborts the chain.
- `Headers` support for `Rest` keys (previously documented but never actually implemented).
- `-y`/`--yes` flag on `o config set-encryption` to skip the confirmation prompt, for scripting.
- MIT `LICENSE`.

### Changed
- Portable-mode credential storage on Linux/macOS now uses the real OS keychain (`secret-tool`/libsecret on Linux, Keychain via `security` on macOS) when available, falling back to an encrypted file (previously plaintext) otherwise.
- `RestData`, `EmailTemplateData`, and `CalendarEventData` JSON parsing is now case-insensitive and camelCase, matching the documented examples for all three (see Fixed).
- README and the generated `o docs` page substantially expanded: interactive picker, TOTP, git sync, and REST chaining sections added; `EmailTemplate`/`CalendarEvent` (previously undocumented despite being fully implemented) and the `config set-url-aliases`/`set-default-params`/`clear-*` editing commands are now documented; the Commands Reference table now lists all real commands instead of a small subset.
- Removed documentation and runtime-message references to a `cloud` command that never existed in the code; replaced with accurate guidance pointing at git-based sync and local storage.

### Fixed
- `EmailTemplate`/`CalendarEvent` keys added using the documented JSON schema silently failed to bind any fields (e.g. `"Provider":"smtp"` was ignored, routing to the wrong provider with no error) due to a case-sensitivity mismatch between the documented PascalCase examples and the deserializer's camelCase-only matching.
- `Rest` keys had the same case-sensitivity bug — the documented lowercase JSON shape (`"url"`, `"method"`) never actually parsed.
- `tests/integration_test.sh` was broken by the credential-storage change above (it seeded a now-encrypted credential file with plaintext); both integration test scripts now drive setup through the actual CLI and cover today's new features.
- `SystemProcessRunner.CommandExists` used `which`, which isn't available on plain Windows; it now uses `where` there.
- External process calls (`secret-tool`, `security`, `git`) had no timeout, so a keychain/credential-store CLI blocking on an interactive authorization prompt in a headless environment (e.g. a CI runner) could hang the whole process forever — this is what caused the `integration_test.ps1` CI job to appear stuck on `config set-encryption portable` on macOS. `IProcessRunner.Run` now bounds every call (20s default) and kills the process on timeout instead of blocking indefinitely; every existing call site already had fallback/error handling in place to absorb the resulting failure cleanly. Also fixed a related sequential-stream-read deadlock risk by reading stdout/stderr concurrently instead of one after the other.

### Security
- Removed a committed encrypted backup file (`backup.dat`) from the repository and added `*.dat` to `.gitignore`.
- Portable-mode credential fallback file is now encrypted (AES-256-GCM, machine-derived key) instead of plaintext.

## [1.0.10] - 2026-07-11

### Added
- Standalone native AOT compilation for multiple platform/architecture combinations: Windows (`win-x64`, `win-arm64`), Linux (`linux-x64`, `linux-arm64`), and macOS (`osx-x64`, `osx-arm64`), published as GitHub Release assets.
- Automated installation scripts hosted on GitHub Pages: `install.ps1` for Windows, and `install.sh` for macOS/Linux.
- Updated documentation pages and README with guidance for script-based and manual release installations.

## [1.0.9] - 2026-07-06

### Added
- View raw details command (`o view <key>`) and global option (`-v` / `--view`) to print the key's metadata (type, description, aliases, default params, and raw/JSON value) directly to the console instead of executing it.
- Built-in dynamic HTML documentation app (`o docs` command) which compiles a modern, responsive, offline-first dashboard in the user's browser, including an interactive **JSON Key Builder** to easily generate `EmailTemplate`, `Rest`, and `CalendarEvent` configurations.
- Documentation output option (`-o` / `--output` in `o docs`) to export the compiled HTML documentation to a custom path (e.g. `index.html`).
- Automation workflow (`.github/workflows/deploy-docs.yml`) to automatically compile and deploy the HTML documentation to GitHub Pages on pushes to `main` or release tags (`v*`).

### Changed
- Updated Microsoft Graph device code flow and token refresh scopes to request explicit delegated permissions (`offline_access`, `Mail.Send`, `Calendars.ReadWrite`, `User.Read`) dynamically, fixing standard `Forbidden` errors when sending mail or scheduling events with public client IDs.

## [1.0.8] - 2026-06-28

### Added
- Email templating (`EmailTemplate` key type) to compose and send emails via the system client, SMTP, or Microsoft Graph API.
- Dynamic attachment support for email templates, allowing attachment file paths to be formatted using CLI placeholders (e.g. `reports/{0}_report.pdf`).
- Calendar event templating (`CalendarEvent` key type) to create calendar invites with subject, body, duration, and relative time expressions.
- File-based and stdin-based value loading for JSON-based keys (`JsonData`, `Rest`, `EmailTemplate`, `CalendarEvent`), enabling adding/updating via `.json` files or pipes (`stdin`) without shell quote escaping.
- Elevated execution flag for local paths and scripts (`LocalPath` key type) to automatically prompt for admin or run via `sudo`.

### Changed
- Improved JSON deserialization robustness with a new JSON normalization state machine that automatically translates single-quoted JSON input to standard double-quoted JSON, preventing PowerShell quoting issues.
- Optimized and simplified `Program.cs` application bootstrap logic.

## [1.0.7] - 2026-06-11

### Changed
- Bumped the package version in preparation for the next NuGet release.

## [1.0.6] - 2026-05-10

### Changed
- Simplified global URL alias configuration to support direct pair syntax, such as `o config set-url-aliases env d=-dev u=-uat p=`.
- Simplified global default parameter configuration to support direct syntax, such as `o config set-default-params user kchirravuri`.
- Added `--file` support to bulk-load global aliases and defaults without shell JSON quoting.
- Updated command help to document the simplified alias/default configuration flow.

### Fixed
- Avoided PowerShell JSON quoting issues for common alias/default configuration workflows.

## [1.0.5] - 2026-05-10

### Added
- Added global URL alias and default parameter configuration commands.
- Expanded command help across the CLI with practical examples and JSON format guidance.

### Fixed
- Made integration tests invoke the locally installed tool by explicit path instead of relying on PATH updates.
- Fixed NuGet publish to resolve package files before pushing on Windows runners.

## [1.0.4] - 2026-05-10

### Fixed
- Relaxed framework runtime requirements so .NET 10 packages run on installed patch versions such as 10.0.6 instead of requiring the build machine patch version.
- Scoped runtime framework versions per target framework so .NET 8 packages request .NET 8 and .NET 10 packages request .NET 10.
- Ensured tag-based publish builds apply the tag version before packing, so the installed tool version matches the NuGet package version.

### Changed
- Simplified release packaging so NuGet packages are created only during tag-based publish runs.

## [1.0.3] - 2026-05-10

### Added
- Named placeholder URL templates for `WebPath` and `Rest` keys, including alias and default parameter support.
- Multi-target support for .NET 8.0 and .NET 10.0.
- AOT-safe JSON parsing for CLI configuration options.

### Changed
- **Important Finding**: Direct file I/O to OneDrive Files On-Demand fails across all .NET applications due to Windows virtual filesystem layer. This is not a bug in Opener but a platform limitation.
- Removed ineffective retry logic and fallback mechanisms that attempted to work around OneDrive access issues.
- Simplified error messages with guidance to use Microsoft Graph API for reliable OneDrive access.
- Release packaging now extracts changelog content and embeds it into NuGet package metadata as release notes.
- CI/CD now validates both supported .NET target frameworks and includes AOT publishing checks.

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
