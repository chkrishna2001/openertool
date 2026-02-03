# Opener Tool (`o`)

**Opener** is a secure, developer-friendly, **cross-platform** CLI tool for managing and quickly accessing your frequently used sensitive data, links, scripts, and API calls. It stores data in an **encrypted** local file and integrates seamlessly into your terminal workflow.

Think of it as a secure key-value store for your CLI that can "act" on the keys: opening browsers, running scripts, copying secrets to clipboard, or making API calls.

## 🌍 Cross-Platform Support

Opener is designed to work seamlessly across **Windows**, **Linux**, and **macOS**.

- **Windows**: Uses native **DPAPI** for zero-config local encryption and **Windows Credential Manager** for secure password caching.
- **Linux & macOS**: Uses a generated **Machine Key** for local encryption and automatically uses platform-specific commands like `xdg-open` or `open` to launch web paths.

## 🚀 Installation

Install globally as a .NET tool:

```bash
dotnet tool install --global com.chirravuris.opener
```

Once installed, use the shorthand command `o`.

## ✨ Features

- **Secure Storage**: All data is encrypted at rest.
  - **Local Mode (Default)**: Zero-config. Uses DPAPI on Windows or a local machine-key on Linux/macOS.
  - **Portable Mode**: Uses AES-256-GCM with a password. Ideal for syncing via OneDrive/Dropbox or moving between machines.
- **Rich TUI**: Interactive tables and colored output using [Spectre.Console](https://spectreconsole.net/).
- **Actionable Keys**: Not just storage — Opener acts on your data:
  - `WebPath`: Opens URLs in your default browser (supports dynamic placeholders).
  - `LocalPath`: Executes files, scripts, or opens folders.
  - `Data`: Securely copies secrets/text to your clipboard.
  - `JsonData`: Pretty-prints JSON and copies it to your clipboard.
  - `Rest`: Executes defined REST API calls directly from the terminal.

## 📖 Usage

### Basics

```bash
# Add a key (Auto-initializes on first use)
o add <key> <value> -t <type>

# Execute/Use a key
o <key> [args]

# List all keys
o list
```

### Key Types & Examples

#### 1. Web Shortcuts (`WebPath`)
Store long URLs and open them quickly. Supports `{0}` placeholders for dynamic arguments.

```bash
# Simple URL
o add google "https://google.com" -t WebPath
o google

# Dynamic URL with placeholder
o add jira "https://jira.company.com/browse/{0}" -t WebPath
o jira PROJ-123  # Opens https://jira.company.com/browse/PROJ-123
```

#### 2. Secrets & Clipboard (`Data`)
Store API keys, connection strings, or passwords.

```bash
o add db-prod "Server=prod;Database=mydb;User Id=admin;Password=secret;" -t Data
o db-prod
# Output: Data copied to clipboard!
```

#### 4. Local Scripts & Folders (`LocalPath`)
Launch local tools or scripts. Supports absolute paths.

#### 5. REST API Client (`Rest`)
Store and execute API requests. Value must be a JSON object.

```bash
o add get-user "{ \"url\": \"https://api.github.com/users/{0}\", \"method\": \"GET\" }" -t Rest
o get-user chkri
```

## ☁️ Portable Mode & OneDrive Sync

By default, Opener uses **Local Encryption** which is tied to your machine. To sync your keys via OneDrive or move them between computers, switch to **Portable Mode**.

### Enable Portable Mode
1. Set your storage location to a synced folder:
   ```bash
   o config set-location "/path/to/onedrive/opener.dat"
   ```
2. Enable portable encryption:
   ```bash
   o config set-encryption portable
   ```

## ⚙️ Configuration

```bash
# Show current config
o config show

# Clear cached password
o config clear-password
```

- **Automation Friendly**: Supports `--password` flags for `config`, `export`, and `import` to integrate into scripts and CI/CD pipelines.
- **🛠 Tech Stack**

- **.NET 10** (AOT Enabled)
- **System.CommandLine** (CLI Parsing)
- **Spectre.Console** (UI)
- **AES-256-GCM / DPAPI** (Encryption)
