# Opener Tool (`o`)

**Opener** is a secure, developer-friendly CLI tool for managing and quickly accessing your frequently used sensitive data, links, scripts, and API calls. It stores data in an **encrypted** local file (using DPAPI or AES-256) and integrates seamlessly into your terminal workflow.

Think of it as a secure key-value store for your CLI that can "act" on the keys: opening browsers, running scripts, copying secrets to clipboard, or making API calls.

## 🚀 Installation

Install globally as a .NET tool:

```bash
dotnet tool install --global Opener.Tool
```

Once installed, use the shorthand command `o`.

## ✨ Features

- **Secure Storage**: All data is encrypted at rest.
  - **Local Mode (Default)**: Uses Windows DPAPI. No password needed, works seamlessly for the current user.
  - **Portable Mode**: Uses AES-256 with a password (cached securely in Windows Credential Manager). Ideal for syncing via OneDrive/Dropbox or moving between machines.
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
# Initialize storage (first run)
o init

# Add a key
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
# Output: Data copied to clipboard! (Ready to paste)
```

#### 3. JSON Snippets (`JsonData`)
Store complex JSON payloads. Copies minified JSON to clipboard but displays pretty-printed version.

```bash
o add config "{ \"env\": \"prod\", \"retries\": 3 }" -t JsonData
o config
# Copies to clipboard and prints formatted JSON to terminal
```

#### 4. Local Scripts & Folders (`LocalPath`)
Launch local tools or scripts.

```bash
# Open a folder
o add logs "C:\AppData\Logs" -t LocalPath
o logs

# Run a script
o add deploy "C:\scripts\deploy.bat" -t LocalPath
o deploy staging
```

#### 5. REST API Client (`Rest`)
Store and execute API requests. Value must be a JSON object with `url`, `method`, and optional `body`.

```bash
# Add a REST endpoint
o add get-user "{ \"url\": \"https://api.github.com/users/{0}\", \"method\": \"GET\" }" -t Rest

# Execute it
o get-user chkri
# Output: Status: OK, { ...json response... }
```

## ☁️ Portable Mode & OneDrive Sync

By default, Opener uses **Local Encryption** (DPAPI) which is machine-specific. If you want to sync your keys via OneDrive or move them between computers, switch to **Portable Mode**.

### Enable Portable Mode
1. Set your storage location to a synced folder:
   ```bash
   o config set-location "C:\Users\You\OneDrive\Opener\keys.dat"
   ```
2. Enable portable encryption (asks for a password):
   ```bash
   o config set-encryption portable
   ```
   *Your password is securely cached in Windows Credential Manager, so you don't need to type it every time.*

### Migration (Export/Import)
You can also manually move keys between machines:

```bash
# Machine A: Export to encrypted file
o export backup.dat

# Machine B: Import
o import backup.dat
```

## ⚙️ Configuration

```bash
# Show current config
o config show

# Clear cached password (locks the tool on next use until re-entered)
o config clear-password
```

## 🛠 Tech Stack

- **.NET 10** (AOT Enabled for speed)
- **System.CommandLine** (CLI Parsing)
- **Spectre.Console** (UI)
- **Windows DPAPI / AES-256** (Encryption)
