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

# New execution flags
# `-r/--return`: write the resolved value to stdout instead of performing the default action (copy/open)
# `-c/--copy`: force copy the resolved value to clipboard instead of performing the default action
# `-s/--search`: when used with a key argument, treat the argument as a search term (case-insensitive substring across key and description). If single match, executes; if multiple, prints matches.

# Examples:
o githubtoken -r    # print token to stdout
o githubtoken -c    # force copy token to clipboard
o token -s          # search keys containing "token"; show matches or execute if single

# List all keys
o list
o list -s github
```

### Key Types & Examples

#### 1. Web Shortcuts (`WebPath`)
Store long URLs and open them quickly. Supports both indexed placeholders (`{0}`) and named placeholders (`<env>`, `<region>`, `<user>`).

```bash
# Simple URL
o add google "https://google.com" -t WebPath
o google

# Dynamic URL with placeholder
o add jira "https://jira.company.com/browse/{0}" -t WebPath
o jira PROJ-123  # Opens https://jira.company.com/browse/PROJ-123

# Named placeholders with aliases/defaults (configured per key or globally)
# Template: https://yourapi<env>.domain.com/<region>/<user>
# env aliases: d -> -dev, u -> -uat, p -> ""
# default user: KC45454
o api d us krishna
# Resolves to: https://yourapi-dev.domain.com/us/KC45454

o api region=us u
# Resolves to: https://yourapi-uat.domain.com/us/KC45454

o api p us
# Resolves to: https://yourapi.domain.com/us/KC45454
```

Placeholder resolution rules:
- Indexed placeholders are resolved first.
- Named placeholders are resolved after indexed placeholders.
- `key=value` args take precedence over positional order.
- Remaining positional args are applied left-to-right by placeholder order in the URL.
- Alias maps are applied per placeholder (for example, `env: { d: -dev, u: -uat, p: "" }`).
- Defaults are used for missing named values.

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

## ☁️ Portable Mode & Cloud Sync

By default, Opener uses **Local Encryption** which is tied to your machine. For cross-platform sync and cloud storage, Opener supports multiple approaches:

### 1. Microsoft Graph API Integration (Recommended for OneDrive)

The `cloud` command provides proper OneDrive access via Microsoft Graph API, which works reliably across **Windows, macOS, and Linux**:

```bash
# Authenticate with your Microsoft account
o cloud auth

# Store data in OneDrive via Graph API
o cloud set-location

# Your keys are now synced to OneDrive automatically
```

This approach bypasses OneDrive's virtual filesystem layer and works on all platforms.

### 2. Local Storage with Manual Sync

For maximum compatibility, store keys locally and manually sync the encrypted file:

```bash
o config set-location "/path/to/local/folder"
o config set-encryption portable
# Then manually backup /path/to/local/folder/opener.dat to cloud
```

### Important Note on OneDrive Files On-Demand

**If you use OneDrive's built-in folder sync:**
- OneDrive **Files On-Demand** (Windows 10/11) creates placeholder files that don't work with direct file I/O
- This affects any application trying to access unmaterialized files, not just Opener
- Windows Explorer shows these files, but .NET applications cannot access them until materialized
- **Solution**: Use the `cloud` command with Graph API instead, which bypasses this limitation

This limitation does **not** apply to:
- Google Drive (no native Linux support, but works on Windows/macOS)
- Local storage
- Microsoft Graph API (our recommended cross-platform solution)

## ⚙️ Configuration

```bash
# Show current config
o config show

# Clear cached password
o config clear-password
```

- **Automation Friendly**: Supports `--password` flags for `config`, `export`, and `import` to integrate into scripts and CI/CD pipelines.

## 🛡️ Data Safety & Backup

Opener includes several safeguards to prevent accidental data loss:

### Confirmation Prompts
Destructive operations now ask for confirmation:
- `o delete <key>` — requires confirmation before deleting (use `-y/--yes` to skip)
- `o config clear-url-aliases` — requires confirmation before clearing all aliases
- `o config set-encryption <mode>` — requires confirmation before re-encrypting (auto-creates backup first)

### Automatic Backups
- **Before migration**: When you switch encryption modes, an automatic backup is created in `.backup/` folder
- **Manual backup**: Use `o backup` to create a snapshot at any time

### Recovery Options
If you lose keys (corrupted file, accidental deletion, etc.):

1. **Check `.backup` folder** (next to your data file):
   ```bash
   # On Windows
   $LOCALAPPDATA\Opener\.backup\

   # On Linux/macOS
   ~/.local/share/Opener/.backup/
   ```

2. **Restore from backup**:
   ```bash
   # If you have an exported backup
   o import backup.dat --password your-password

   # Or manually copy a `.backup` file back to your data location
   # Then restart the tool
   ```

3. **Export for safekeeping**:
   ```bash
   # Create an encrypted export
   o export ~/Documents/opener_backup.dat --password backup-password

   # Or use auto-backup (stored with timestamps)
   o backup --password optional-password
   ```

### Storage Location Safeguarding
To keep your data in a custom location:

```bash
# Set a custom storage path (must include filename)
o config set-location "C:\Users\Me\Secure\opener.dat"    # Windows
o config set-location "$HOME/secure/opener.dat"           # Linux/macOS

# Verify it took effect
o config show
```

**Avoid these locations** (known issues):
- `~/Documents` on OneDrive-synced machines (use `~/Desktop` or local paths instead)
- Network shares without proper permissions
- Read-only filesystems

The tool now validates write permissions before saving the config, so you'll see errors upfront.

- **🛠 Tech Stack**

- **.NET 10** (AOT Enabled)
- **System.CommandLine** (CLI Parsing)
- **Spectre.Console** (UI)
- **AES-256-GCM / DPAPI** (Encryption)
