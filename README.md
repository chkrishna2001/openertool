# Opener Tool (`o`)

[![Documentation](https://img.shields.io/badge/docs-chkrishna2001.github.io%2Fopenertool-blue)](https://chkrishna2001.github.io/openertool/)

**Opener** is a secure, developer-friendly, **cross-platform** CLI tool for managing and quickly accessing your frequently used sensitive data, links, scripts, and API calls. It stores data in an **encrypted** local file and integrates seamlessly into your terminal workflow.

Think of it as a secure key-value store for your CLI that can "act" on the keys: opening browsers, running scripts, copying secrets to clipboard, or making API calls.

## 🌍 Cross-Platform Support

Opener is designed to work seamlessly across **Windows**, **Linux**, and **macOS**.

- **Windows**: Uses native **DPAPI** for zero-config local encryption and **Windows Credential Manager** for secure password caching.
- **Linux & macOS**: Uses a generated **Machine Key** for local encryption and automatically uses platform-specific commands like `xdg-open` or `open` to launch web paths.

## 🚀 Installation

Opener can be installed using automated scripts, as a global .NET tool, or by downloading precompiled binaries directly.

### 1. Automated Installers (Recommended)

Choose the command for your operating system to download and install the latest native AOT binary automatically:

**Windows (PowerShell):**
```powershell
powershell -c "irm https://chkrishna2001.github.io/openertool/install.ps1 | iex"
```

**macOS & Linux (Bash):**
```bash
curl -fsSL https://chkrishna2001.github.io/openertool/install.sh | sh
```

### 2. Global .NET Tool (NuGet)

If you have the .NET SDK installed, you can install Opener globally via NuGet:

```bash
dotnet tool install --global com.chirravuris.opener
```

### 3. Manual Download

You can download the precompiled native standalone binaries directly from the latest [GitHub Releases](https://github.com/chkrishna2001/openertool/releases).

Once installed, use the shorthand command `o` to access the tool.

## ✨ Features

- **Secure Storage**: All data is encrypted at rest.
  - **Local Mode (Default)**: Zero-config. Uses DPAPI on Windows or a local machine-key on Linux/macOS.
  - **Portable Mode**: Uses AES-256-GCM with a password. Ideal for syncing via OneDrive/Dropbox or moving between machines.
- **Rich TUI**: Interactive tables and colored output using [Spectre.Console](https://spectreconsole.net/).
- **Interactive Picker**: Run `o` with no arguments to open a searchable picker over all your keys — handy from a terminal, and works as a quick launcher from Windows Run (Win+R).
- **Git-Based Vault Sync**: Push/pull your encrypted vault through a git remote instead of relying on a cloud-storage client.
- **Actionable Keys**: Not just storage — Opener acts on your data:
  - `WebPath`: Opens URLs in your default browser (supports dynamic placeholders).
  - `LocalPath`: Executes files, scripts, or opens folders.
  - `Data`: Securely copies secrets/text to your clipboard.
  - `JsonData`: Pretty-prints JSON and copies it to your clipboard.
  - `Rest`: Executes defined REST API calls directly from the terminal, including multi-step chains that pass a token from one call into the next.
  - `EmailTemplate`: Sends templated emails via your system mail client, SMTP, or Microsoft Graph.
  - `CalendarEvent`: Creates calendar invites, including relative start times like "tomorrow 10am".
  - `Totp`: Generates live 6-digit 2FA codes, compatible with Google Authenticator/Authy.

## 📖 Usage

### Basics

```bash
# Add a key (Auto-initializes on first use)
o add <key> <value> -t <type>

# Execute/Use a key
o <key> [args]

# Run with no key to open an interactive, searchable picker over all your keys -
# selecting one runs it exactly like typing its name would. Falls back to a plain
# list if the console isn't interactive (e.g. piped output), so it never hangs.
# Handy launched from Windows Run (Win+R): type "o" and hit Enter.
o

# New execution flags
# `-r/--return`: write the resolved value to stdout instead of performing the default action (copy/open)
# `-c/--copy`: force copy the resolved value to clipboard instead of performing the default action
# `-s/--search`: treat the key as a search term and look up by substring (case-insensitive)
# `-v/--view`: view the raw details and stored value of the key instead of executing it
# `-e/--elevated`: run a LocalPath key elevated (admin/sudo) even if it wasn't stored that way

# Examples:
o githubtoken -r    # print token to stdout
o githubtoken -c    # force copy token to clipboard
o token -s          # search keys containing "token"; show matches or execute if single
o myemail -v        # view raw email template JSON details

# List all keys
o list
o list -s github

# View raw stored details of a key
o view mykey

# Open interactive HTML documentation and schema builder
o docs
o docs -o docs.html    # write it to a file instead of opening a browser
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

#### 3. Structured Secrets (`JsonData`)
Like `Data`, but for JSON — pretty-prints it in the terminal in addition to copying it to your clipboard.

```bash
o add api-response "{\"id\":1,\"name\":\"test\"}" -t JsonData
o api-response
# Pretty-prints the JSON and copies the raw string to your clipboard.
```

#### 4. Local Scripts & Folders (`LocalPath`)
Launch local tools or scripts. Supports absolute paths.

#### 5. REST API Client (`Rest`)
Store and execute API requests. Value must be a JSON object.

```bash
o add get-user "{ \"url\": \"https://api.github.com/users/{0}\", \"method\": \"GET\", \"headers\": { \"User-Agent\": \"Opener-CLI\" } }" -t Rest
o get-user chkri
```

**Referencing another stored key:** `{{key:name}}` in a `Rest` key's `url`, `headers`, or
`body` resolves to the raw stored value of another key in your vault — handy for keeping an
API token in its own `Data` key (so it's listed, editable, and reusable on its own) instead of
inlining it into every `Rest` key that needs it. Lookup is by key name, case-insensitive.

```bash
o add githubtoken "ghp_xxxxxxxxxxxx" -t Data
o add get-user '{ "url": "https://api.github.com/users/{0}", "method": "GET", "headers": { "Authorization": "Bearer {{key:githubtoken}}" } }' -t Rest
o get-user chkri
```

**Chaining requests (login, then call):** instead of a single request, the value can be
`{ "steps": [...] }` — each step can `extract` values from its JSON response (dot-separated
path, e.g. `"data.token"` or `"items[0].id"`), and later steps reference them as `{{varName}}`
in their `url`, `headers`, or `body`. If a non-final step fails, the chain aborts instead of
continuing with bad data.

```bash
o add authed-api '{
  "steps": [
    {
      "url": "https://api.example.com/login",
      "method": "POST",
      "body": "{\"user\":\"me\",\"pass\":\"secret\"}",
      "extract": { "token": "access_token" }
    },
    {
      "url": "https://api.example.com/data/{0}",
      "method": "GET",
      "headers": { "Authorization": "Bearer {{token}}" }
    }
  ]
}' -t Rest
o authed-api 123
```

#### 6. Email Templates (`EmailTemplate`)
Send templated emails from the terminal via your system mail client, SMTP, or Microsoft Graph. Value must be a valid JSON object with **camelCase** field names (`to`, `cc`, `bcc`, `subject`, `body`, `attachmentPath`, `provider`).

```bash
o add alert '{"to":"oncall@company.com","subject":"Alert: <level>","body":"Details: <details>","provider":"system"}' -t EmailTemplate
o alert level=P1 details="disk full"
```

Providers:
- `system` (default): opens your default mail client via the `mailto:` protocol.
- `smtp`: sends via a custom SMTP server — set up with `o config set-provider smtp --server <host> --port <port> --username <user> --password <pass> [--ssl]`.
- `graph`: sends directly via Microsoft Graph API — requires `o config auth-graph` (device-code login) or `o config set-provider graph --tenant-id <id> --client-id <id> --client-secret <secret>` first.

#### 7. Calendar Events (`CalendarEvent`)
Create calendar invites or schedule meetings. Value must be a valid JSON object with **camelCase** field names (`subject`, `body`, `invitees`, `durationMinutes`, `availability`, `provider`, `startTime`).

```bash
o add sync-meeting '{"subject":"Sync Meeting","body":"Roadmap discussion","invitees":"a@company.com,b@company.com","durationMinutes":30,"provider":"system","startTime":"tomorrow 10:00"}' -t CalendarEvent
o sync-meeting
```

`startTime` accepts relative inputs like `"tomorrow 10am"`, `"today 3pm"`, `"next monday 10:30"`, resolved at execution time. The `system` provider generates a local `.ics` file and opens it in your default calendar app; `graph` creates the event directly in Microsoft 365/Exchange (same `auth-graph`/`set-provider graph` setup as `EmailTemplate` above).

#### 8. Two-Factor Codes (`Totp`)
Store a 2FA seed and generate the live 6-digit code, using the same algorithm (RFC 6238)
as Google Authenticator/Authy — no separate app needed. Accepts either the raw base32
secret or a full `otpauth://` URI (the secret is extracted automatically).

```bash
o add github JBSWY3DPEHPK3PXP -t Totp
o github            # copies the current code to your clipboard
o github -r         # prints the current code instead
```

Because a compromised vault would then expose both a password *and* its 2FA code together,
this is best suited to lower-stakes accounts (internal tools, staging) rather than the
account tied to the vault's own recovery (e.g. your primary email or GitHub).

## 🔄 Git-Based Vault Sync

Push/pull your already-encrypted vault file through a git remote, instead of relying on a
cloud-storage client's local sync agent (see the OneDrive note below for why that can be
unreliable). Git only ever sees ciphertext.

```bash
# One-time setup
o config set-sync-remote git@github.com:me/opener-vault.git   # SSH: uses your existing SSH keys
o config set-sync-remote https://github.com/me/opener-vault.git  # HTTPS: needs a token, see below

# Manual sync
o sync push
o sync pull      # backs up your current vault to .backup/ first
o sync status
```

**SSH vs. HTTPS:** an SSH remote needs no extra setup — it uses your existing SSH agent/keys,
and Opener never stores anything for it. For an HTTPS remote, store a personal access token
(kept in your OS keychain, in a slot separate from your vault's own unlock password):

```bash
o config set-sync-token ghp_xxxxxxxxxxxx
```

**Auto-sync (opt-in):** once a remote is set, you can have every `add`/`update`/`delete`/
`import` push automatically in the background:

```bash
o config enable-auto-sync
o config disable-auto-sync
```

Auto-sync never fails the command that triggered it — a push failure just prints a warning.
A pull conflict (two machines changed the vault before syncing) isn't auto-merged, since
merging an opaque encrypted blob is meaningless — it aborts cleanly and tells you to resolve
it manually (your data is safe either way, since a pull always backs up first).

## ☁️ Portable Mode & Cross-Machine Sync

By default, Opener uses **Local Encryption** which is tied to your machine and doesn't sync anywhere. To use your vault across multiple machines, you have two options:

### 1. Git-Based Vault Sync (Recommended)

See the **Git-Based Vault Sync** section above — push/pull your encrypted vault through a git remote (GitHub, GitLab, self-hosted, whatever you already use). Works identically on **Windows, macOS, and Linux**, and sidesteps the OneDrive Files-On-Demand problem below entirely, since it never depends on a cloud-storage client's local sync agent.

### 2. Local Storage with Manual Sync

For maximum compatibility, switch to portable (password-based) encryption and manually sync the resulting file yourself, with whatever tool you already use:

```bash
o config set-location "/path/to/local/folder/opener.dat"
o config set-encryption portable --password your-secret
# Then sync /path/to/local/folder/opener.dat however you like (cloud-storage app, git, USB drive, ...)
```

### Important Note on OneDrive Files On-Demand

**If you use OneDrive's built-in folder sync:**
- OneDrive **Files On-Demand** (Windows 10/11) creates placeholder files that don't work with direct file I/O
- This affects any application trying to access unmaterialized files, not just Opener
- Windows Explorer shows these files, but .NET applications cannot access them until materialized
- **Solution**: don't point Opener's storage location at a OneDrive-synced folder — use a local folder (`o config set-location`) plus **Git-Based Vault Sync** instead

This limitation does **not** apply to local storage or to git-based sync.

> **Note:** `o config auth-graph` / `o config set-provider graph` authenticate against Microsoft Graph for **sending email and creating calendar events** (`EmailTemplate`/`CalendarEvent` keys) — they don't store or sync your vault. There is currently no OneDrive/SharePoint storage backend for the vault itself.

## ⚙️ Configuration

```bash
# Show current config
o config show

# Clear cached password
o config clear-password
```

### Global URL Aliases & Default Parameters

These apply to every key's named placeholders (`<env>`, `<region>`, ...), so you don't have to set `--url-aliases`/`--default-params` on each key individually.

```bash
# Set the alias map for one placeholder - this REPLACES that placeholder's whole map,
# it doesn't merge into it. Resupply every pair you want to keep.
o config set-url-aliases env d=-dev u=-uat p=

# Set one default value - this only touches that one placeholder, others are untouched
o config set-default-params user kchirravuri

# Remove one placeholder's aliases / one default value
o config clear-url-alias env
o config clear-default-param user

# Remove everything (asks for confirmation)
o config clear-url-aliases
o config clear-default-params

# Bulk edit: `config show` prints these as JSON - copy it out, edit it, and load it back.
# This replaces the whole map in one shot, avoiding the per-placeholder replace behavior above.
o config set-url-aliases --file aliases.json
o config set-default-params --file defaults.json
```

- **Automation Friendly**: Supports `--password` flags for `config`, `export`, and `import`, and `-y/--yes` to skip confirmation prompts (e.g. `o config set-encryption portable --password ... -y`), to integrate into scripts and CI/CD pipelines.
- `o config auth-graph [--client-id <id>]` — override the default Azure AD app registration used for the Microsoft Graph device-code login, if you're using your own.

## 🛡️ Data Safety & Backup

Opener includes several safeguards to prevent accidental data loss:

### Confirmation Prompts
Destructive operations now ask for confirmation:
- `o delete <key>` — requires confirmation before deleting (use `-y/--yes` to skip)
- `o config clear-url-aliases` — requires confirmation before clearing all aliases
- `o config set-encryption <mode>` — requires confirmation before re-encrypting (auto-creates backup first, use `-y/--yes` to skip)

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
