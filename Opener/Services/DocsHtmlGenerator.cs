using System;

namespace Opener.Services;

public static class DocsHtmlGenerator
{
    public static string GetHtml(string version)
    {
        return rawHtml.Replace("{{VERSION}}", version);
    }

    private const string rawHtml = """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Opener CLI Documentation</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;500;600;700&family=Plus+Jakarta+Sans:wght@300;400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet">
    <style>
        :root {
            --bg-page: #080C14;
            --bg-sidebar: #0C1220;
            --bg-card: #121A2E;
            --bg-code: #070B14;
            --border-color: #1E294B;
            --text-main: #E2E8F0;
            --text-muted: #94A3B8;
            --text-heading: #F8FAFC;
            --accent-primary: #8B5CF6;
            --accent-secondary: #06B6D4;
            --accent-gradient: linear-gradient(135deg, #8B5CF6 0%, #3B82F6 100%);
            --glow-color: rgba(139, 92, 246, 0.15);
            --font-sans: 'Outfit', 'Plus Jakarta Sans', system-ui, -apple-system, sans-serif;
            --font-mono: 'JetBrains Mono', monospace;
        }

        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }

        html {
            scroll-behavior: smooth;
        }

        body {
            background-color: var(--bg-page);
            color: var(--text-main);
            font-family: var(--font-sans);
            line-height: 1.6;
            display: flex;
            min-height: 100vh;
        }

        /* Scrollbar */
        ::-webkit-scrollbar {
            width: 8px;
            height: 8px;
        }
        ::-webkit-scrollbar-track {
            background: var(--bg-page);
        }
        ::-webkit-scrollbar-thumb {
            background: var(--border-color);
            border-radius: 4px;
        }
        ::-webkit-scrollbar-thumb:hover {
            background: var(--accent-primary);
        }

        /* Sidebar styling */
        aside {
            width: 280px;
            background-color: var(--bg-sidebar);
            border-right: 1px solid var(--border-color);
            position: fixed;
            top: 0;
            bottom: 0;
            left: 0;
            display: flex;
            flex-direction: column;
            padding: 24px;
            z-index: 100;
            overflow-y: auto;
        }

        .logo-area {
            display: flex;
            align-items: center;
            gap: 12px;
            margin-bottom: 32px;
        }

        .logo-icon {
            width: 40px;
            height: 40px;
            background: var(--accent-gradient);
            border-radius: 10px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: 700;
            color: white;
            font-size: 20px;
            box-shadow: 0 4px 14px rgba(59, 130, 246, 0.4);
        }

        .logo-text {
            font-size: 22px;
            font-weight: 700;
            letter-spacing: 0.5px;
            background: linear-gradient(to right, #FFF 30%, var(--text-muted));
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
        }

        .search-container {
            position: relative;
            margin-bottom: 24px;
        }

        .search-input {
            width: 100%;
            background-color: var(--bg-code);
            border: 1px solid var(--border-color);
            border-radius: 8px;
            padding: 10px 12px 10px 36px;
            color: var(--text-heading);
            font-family: var(--font-sans);
            font-size: 14px;
            transition: all 0.3s ease;
        }

        .search-input:focus {
            outline: none;
            border-color: var(--accent-primary);
            box-shadow: 0 0 0 3px var(--glow-color);
        }

        .search-icon {
            position: absolute;
            left: 12px;
            top: 50%;
            transform: translateY(-50%);
            color: var(--text-muted);
            pointer-events: none;
            width: 16px;
            height: 16px;
        }

        .nav-section-title {
            font-size: 11px;
            text-transform: uppercase;
            letter-spacing: 1.5px;
            color: var(--text-muted);
            margin: 16px 0 8px 4px;
            font-weight: 600;
        }

        .nav-links {
            list-style: none;
            display: flex;
            flex-direction: column;
            gap: 4px;
        }

        .nav-link {
            display: flex;
            align-items: center;
            padding: 8px 12px;
            color: var(--text-muted);
            text-decoration: none;
            border-radius: 6px;
            font-size: 14px;
            font-weight: 500;
            transition: all 0.2s ease;
            cursor: pointer;
        }

        .nav-link:hover, .nav-link.active {
            color: var(--text-heading);
            background-color: rgba(255, 255, 255, 0.03);
        }

        .nav-link.active {
            border-left: 3px solid var(--accent-primary);
            border-top-left-radius: 0;
            border-bottom-left-radius: 0;
            background-color: rgba(139, 92, 246, 0.08);
        }

        /* Main content layout */
        main {
            margin-left: 280px;
            flex: 1;
            padding: 48px 64px;
            max-width: 1000px;
        }

        header {
            margin-bottom: 48px;
            border-bottom: 1px solid var(--border-color);
            padding-bottom: 32px;
        }

        .badge {
            display: inline-flex;
            align-items: center;
            background: rgba(139, 92, 246, 0.15);
            border: 1px solid rgba(139, 92, 246, 0.3);
            color: #C084FC;
            padding: 4px 10px;
            border-radius: 20px;
            font-size: 12px;
            font-weight: 600;
            margin-bottom: 16px;
        }

        h1 {
            font-size: 42px;
            font-weight: 700;
            color: var(--text-heading);
            margin-bottom: 16px;
            letter-spacing: -0.5px;
        }

        h2 {
            font-size: 26px;
            font-weight: 600;
            color: var(--text-heading);
            margin-top: 40px;
            margin-bottom: 20px;
            border-bottom: 1px solid rgba(30, 41, 75, 0.5);
            padding-bottom: 8px;
        }

        h3 {
            font-size: 20px;
            font-weight: 600;
            color: var(--text-heading);
            margin-top: 24px;
            margin-bottom: 12px;
        }

        p {
            margin-bottom: 16px;
            color: var(--text-main);
        }

        /* Code Block Styling */
        pre {
            background-color: var(--bg-code);
            border: 1px solid var(--border-color);
            border-radius: 8px;
            padding: 16px;
            overflow-x: auto;
            position: relative;
            margin: 16px 0;
            font-family: var(--font-mono);
            font-size: 14px;
        }

        code {
            font-family: var(--font-mono);
            font-size: 14px;
            background-color: rgba(30, 41, 75, 0.3);
            padding: 2px 6px;
            border-radius: 4px;
            color: #38BDF8;
        }

        pre code {
            background-color: transparent;
            padding: 0;
            border-radius: 0;
            color: inherit;
        }

        .copy-btn {
            position: absolute;
            right: 8px;
            top: 8px;
            background: rgba(255, 255, 255, 0.05);
            border: 1px solid var(--border-color);
            color: var(--text-muted);
            border-radius: 4px;
            padding: 4px 8px;
            font-size: 12px;
            cursor: pointer;
            transition: all 0.2s ease;
        }

        .copy-btn:hover {
            background: rgba(255, 255, 255, 0.1);
            color: var(--text-heading);
        }

        /* Alert / Callout Boxes */
        .callout {
            background-color: rgba(139, 92, 246, 0.05);
            border-left: 4px solid var(--accent-primary);
            border-radius: 0 8px 8px 0;
            padding: 16px 20px;
            margin: 24px 0;
        }

        .callout-title {
            font-weight: 600;
            color: var(--text-heading);
            margin-bottom: 6px;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .callout.warning {
            background-color: rgba(234, 179, 8, 0.03);
            border-left-color: #EAB308;
        }

        .callout.warning .callout-title {
            color: #FACC15;
        }

        /* Grid layouts */
        .grid-2 {
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 24px;
            margin: 24px 0;
        }

        .card {
            background-color: var(--bg-card);
            border: 1px solid var(--border-color);
            border-radius: 12px;
            padding: 24px;
            transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
            position: relative;
            overflow: hidden;
        }

        .card:hover {
            transform: translateY(-2px);
            border-color: rgba(139, 92, 246, 0.4);
            box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.3), 0 0 20px 0 var(--glow-color);
        }

        .card h3 {
            margin-top: 0;
            margin-bottom: 8px;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        /* Form Controls for Interactive Builder */
        .form-group {
            margin-bottom: 16px;
        }

        label {
            display: block;
            font-size: 13px;
            font-weight: 600;
            color: var(--text-muted);
            margin-bottom: 6px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        input[type="text"], select, textarea {
            width: 100%;
            background-color: var(--bg-code);
            border: 1px solid var(--border-color);
            border-radius: 8px;
            padding: 12px;
            color: var(--text-heading);
            font-family: var(--font-sans);
            font-size: 14px;
            transition: all 0.3s ease;
        }

        input[type="text"]:focus, select:focus, textarea:focus {
            outline: none;
            border-color: var(--accent-secondary);
            box-shadow: 0 0 0 3px rgba(6, 182, 212, 0.15);
        }

        .btn-primary {
            background: var(--accent-gradient);
            color: white;
            border: none;
            padding: 12px 24px;
            border-radius: 8px;
            font-family: var(--font-sans);
            font-size: 15px;
            font-weight: 600;
            cursor: pointer;
            box-shadow: 0 4px 12px rgba(139, 92, 246, 0.3);
            transition: all 0.2s ease;
        }

        .btn-primary:hover {
            transform: translateY(-1px);
            box-shadow: 0 6px 16px rgba(139, 92, 246, 0.4);
        }

        /* Table styling */
        table {
            width: 100%;
            border-collapse: collapse;
            margin: 24px 0;
        }

        th, td {
            padding: 12px 16px;
            text-align: left;
            border-bottom: 1px solid var(--border-color);
        }

        th {
            background-color: rgba(255, 255, 255, 0.02);
            color: var(--text-heading);
            font-weight: 600;
        }

        tr:hover td {
            background-color: rgba(255, 255, 255, 0.01);
        }

        /* Header builder row (Rest headers) */
        .header-row {
            display: grid;
            grid-template-columns: 1fr 1fr auto;
            gap: 12px;
            margin-bottom: 8px;
            align-items: center;
        }

        .btn-icon {
            background: rgba(239, 68, 68, 0.1);
            color: #EF4444;
            border: 1px solid rgba(239, 68, 68, 0.2);
            width: 38px;
            height: 38px;
            border-radius: 8px;
            display: flex;
            align-items: center;
            justify-content: center;
            cursor: pointer;
            font-size: 16px;
            transition: all 0.2s;
        }

        .btn-icon:hover {
            background: #EF4444;
            color: white;
        }

        .btn-secondary {
            background: rgba(255, 255, 255, 0.03);
            border: 1px solid var(--border-color);
            color: var(--text-main);
            padding: 8px 16px;
            border-radius: 8px;
            font-size: 13px;
            font-weight: 500;
            cursor: pointer;
            transition: all 0.2s;
            display: inline-flex;
            align-items: center;
            gap: 6px;
            margin-top: 8px;
        }

        .btn-secondary:hover {
            background: rgba(255, 255, 255, 0.08);
            border-color: var(--text-muted);
        }

        /* Mobile Responsive */
        @media (max-width: 900px) {
            aside {
                display: none; /* Can build a toggle menu if needed, keeping simple */
            }
            main {
                margin-left: 0;
                padding: 24px;
            }
            .grid-2 {
                grid-template-columns: 1fr;
            }
        }
    </style>
</head>
<body>

    <!-- SIDEBAR -->
    <aside>
        <div class="logo-area">
            <div class="logo-icon">o</div>
            <div>
                <div class="logo-text">Opener Docs</div>
                <div style="font-size: 11px; color: var(--text-muted); margin-top: 2px;">Version {{VERSION}}</div>
            </div>
        </div>

        <div class="search-container">
            <svg class="search-icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
            <input type="text" id="sidebarSearch" class="search-input" placeholder="Search documentation...">
        </div>

        <nav>
            <div class="nav-section-title">Getting Started</div>
            <ul class="nav-links">
                <li><a href="#welcome" class="nav-link active">Welcome</a></li>
                <li><a href="#commands" class="nav-link">Commands Reference</a></li>
            </ul>

            <div class="nav-section-title">Key Types Guide</div>
            <ul class="nav-links">
                <li><a href="#key-types" class="nav-link">Overview & Simple Types</a></li>
                <li><a href="#webpath" class="nav-link">Web Shortcuts (WebPath)</a></li>
                <li><a href="#localpath" class="nav-link">Local Scripts (LocalPath)</a></li>
                <li><a href="#rest" class="nav-link">REST API (Rest)</a></li>
                <li><a href="#email" class="nav-link">Email Templates</a></li>
                <li><a href="#calendar" class="nav-link">Calendar Events</a></li>
                <li><a href="#totp" class="nav-link">Two-Factor Codes (Totp)</a></li>
            </ul>

            <div class="nav-section-title">Productivity</div>
            <ul class="nav-links">
                <li><a href="#picker" class="nav-link">Interactive Picker</a></li>
                <li><a href="#sync" class="nav-link">Git-Based Vault Sync</a></li>
            </ul>

            <div class="nav-section-title">Interactive Tool</div>
            <ul class="nav-links">
                <li><a href="#builder" class="nav-link">JSON Key Builder</a></li>
            </ul>
        </nav>
    </aside>

    <!-- MAIN CONTENT -->
    <main>
        <section id="welcome">
            <header>
                <div class="badge">Opener CLI Tool</div>
                <h1>Welcome to Opener</h1>
                <p class="text-muted">A secure, developer-friendly CLI manager to organize, encrypt, and quickly run your links, scripts, credentials, emails, and API templates.</p>
            </header>

            <h2>Why Opener?</h2>
            <p>As developers, our daily workflows are filled with repetitive operations and scattered data. We constantly juggle:</p>
            <ul style="margin: 0 0 20px 24px; color: var(--text-main); list-style-type: square;">
                <li style="margin-bottom: 8px;"><strong>Messy Bookmarks & Long URLs</strong>: Constantly searching browser history or bookmarks for Jira issues, cloud consoles, or internal wiki pages.</li>
                <li style="margin-bottom: 8px;"><strong>Plain-Text Secrets</strong>: Storing database connection strings, API tokens, and passwords in temporary plain-text scratchpads, which is a major security risk.</li>
                <li style="margin-bottom: 8px;"><strong>Repetitive CLI Typing</strong>: Typing long scripts, path commands, or REST endpoint URLs repeatedly throughout the day.</li>
                <li style="margin-bottom: 8px;"><strong>Context Switching</strong>: Opening separate mail clients or calendars to coordinate quick team syncs or send standard email alerts.</li>
            </ul>

            <p><strong>Opener solves all of this</strong> by providing a single, unified, secure CLI vault. You register your shortcuts, scripts, and endpoints once under simple, custom keys, and Opener executes them instantly with one shorthand command: <code>o &lt;key&gt;</code>.</p>

            <h2>Installation</h2>
            <p>Opener can be installed using automated scripts, as a global .NET tool, or by downloading precompiled binaries directly.</p>
            
            <h3 style="margin-top: 20px; font-size: 1.1rem; color: var(--text-heading);">1. Automated Installers (Recommended)</h3>
            <p>Choose the command for your operating system to download and install the native AOT binary automatically:</p>
            
            <div style="margin-top: 10px; margin-bottom: 15px;">
                <strong>Windows (PowerShell):</strong>
                <pre style="margin-top: 5px;"><code>powershell -c "irm https://chkrishna2001.github.io/openertool/install.ps1 | iex"</code></pre>
            </div>
            
            <div style="margin-bottom: 20px;">
                <strong>macOS & Linux (Bash):</strong>
                <pre style="margin-top: 5px;"><code>curl -fsSL https://chkrishna2001.github.io/openertool/install.sh | sh</code></pre>
            </div>
            
            <h3 style="margin-top: 20px; font-size: 1.1rem; color: var(--text-heading);">2. Global .NET Tool (NuGet)</h3>
            <p>If you have the .NET SDK installed, you can install Opener globally via NuGet:</p>
            <pre style="margin-top: 5px;"><code>dotnet tool install --global com.chirravuris.opener</code></pre>
            
            <h3 style="margin-top: 20px; font-size: 1.1rem; color: var(--text-heading);">3. Manual Download</h3>
            <p>You can download the precompiled native standalone binaries directly from the latest <a href="https://github.com/chkrishna2001/openertool/releases" target="_blank" style="color: var(--accent-primary); text-decoration: underline;">GitHub Releases</a>.</p>
            
            <p style="margin-top: 15px;">Once installed, you can use the shorthand command <code>o</code> to access the tool from your terminal.</p>

            <h2>Core Capabilities</h2>
            <div class="grid-2">
                <div class="card">
                    <h3>
                        <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"/></svg>
                        Secure Storage
                    </h3>
                    <p>Uses Windows DPAPI or AES-256 machine keys to securely encrypt credentials. Portable (password-based) mode lets you sync the vault yourself across machines - via git (see Git-Based Vault Sync) or any file-sync tool you already use.</p>
                </div>
                <div class="card">
                    <h3>
                        <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
                        Actionable Keys
                    </h3>
                    <p>Not just raw storage. Opener opens browsers, runs programs, copies to clipboard, invokes HTTP requests, or constructs templates dynamically.</p>
                </div>
                <div class="card">
                    <h3>
                        <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/></svg>
                        Email & Calendaring
                    </h3>
                    <p>Send standard emails or book calendar slots right from your CLI via SMTP or Microsoft Graph API without opening heavy graphic clients.</p>
                </div>
                <div class="card">
                    <h3>
                        <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/></svg>
                        Dynamic Placeholders
                    </h3>
                    <p>Supports dynamic indexed or named template arguments that resolve at runtime, applying default values and alias maps automatically.</p>
                </div>
            </div>

            <div class="callout">
                <div class="callout-title">
                    <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>
                    Shorthand Command
                </div>
                <p>All examples use the shorthand alias <code>o</code> instead of the full executable name. To check available options, you can always run <code>o --help</code>.</p>
            </div>
        </section>

        <!-- COMMANDS REFERENCE -->
        <section id="commands">
            <h2>Commands Reference</h2>
            <p>Basic syntax: <code>o &lt;command&gt; [args]</code></p>
            
            <table>
                <thead>
                    <tr>
                        <th style="width: 25%;">Command</th>
                        <th>Description</th>
                        <th style="width: 35%;">Example</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td><code>add</code></td>
                        <td>Add a key with specific value, type, and optional metadata.</td>
                        <td><code>o add mykey "value" -t Data</code></td>
                    </tr>
                    <tr>
                        <td><code>update</code></td>
                        <td>Update an existing key's value or default options.</td>
                        <td><code>o update mykey "newvalue"</code></td>
                    </tr>
                    <tr>
                        <td><code>delete</code></td>
                        <td>Delete a key from storage. Ask confirmation by default.</td>
                        <td><code>o delete mykey -y</code></td>
                    </tr>
                    <tr>
                        <td><code>list</code></td>
                        <td>List all keys with their type, elevation, and preview.</td>
                        <td><code>o list -s searchterm</code></td>
                    </tr>
                    <tr>
                        <td><code>view</code> / <code>-v</code></td>
                        <td>Inspect raw stored metadata and value details for a key.</td>
                        <td><code>o view mykey</code> or <code>o mykey -v</code></td>
                    </tr>
                    <tr>
                        <td><code>-e</code></td>
                        <td>Run a <code>LocalPath</code> key elevated (admin/sudo), overriding how it was stored.</td>
                        <td><code>o mykey -e</code></td>
                    </tr>
                    <tr>
                        <td><code>docs</code></td>
                        <td>Open this documentation in your browser, or write it to a file.</td>
                        <td><code>o docs</code> or <code>o docs -o docs.html</code></td>
                    </tr>
                    <tr>
                        <td><code>backup</code></td>
                        <td>Create a timestamped backup of the current database.</td>
                        <td><code>o backup --password pass</code></td>
                    </tr>
                    <tr>
                        <td><code>export</code></td>
                        <td>Export database to a password-protected portable file.</td>
                        <td><code>o export backup.dat --password pass</code></td>
                    </tr>
                    <tr>
                        <td><code>import</code></td>
                        <td>Import keys from a password-protected file.</td>
                        <td><code>o import backup.dat --password pass</code></td>
                    </tr>
                    <tr>
                        <td><code>sync push / pull / status</code></td>
                        <td>Push/pull the encrypted vault through a git remote instead of a cloud-storage client. See the Git-Based Vault Sync section.</td>
                        <td><code>o sync push</code></td>
                    </tr>
                    <tr>
                        <td><code>config show</code></td>
                        <td>Display configuration file location, encryption type, global aliases/defaults, etc.</td>
                        <td><code>o config show</code></td>
                    </tr>
                    <tr>
                        <td><code>config set-location</code></td>
                        <td>Set a custom path for the encrypted data file.</td>
                        <td><code>o config set-location "/path/to/opener.dat"</code></td>
                    </tr>
                    <tr>
                        <td><code>config set-encryption</code></td>
                        <td>Switch between local (machine-tied) and portable (password) encryption. Use <code>-y</code> to skip the confirmation prompt.</td>
                        <td><code>o config set-encryption portable --password pass</code></td>
                    </tr>
                    <tr>
                        <td><code>config clear-password</code></td>
                        <td>Clear the cached portable-mode password from the local credential store.</td>
                        <td><code>o config clear-password</code></td>
                    </tr>
                    <tr>
                        <td><code>config set-url-aliases</code> / <code>clear-url-alias(es)</code></td>
                        <td>Set or clear the global alias map for a named placeholder. Setting replaces that placeholder's whole map - it doesn't merge.</td>
                        <td><code>o config set-url-aliases env d=-dev u=-uat p=</code></td>
                    </tr>
                    <tr>
                        <td><code>config set-default-params</code> / <code>clear-default-param(s)</code></td>
                        <td>Set or clear one global default value for a named placeholder.</td>
                        <td><code>o config set-default-params user kchirravuri</code></td>
                    </tr>
                    <tr>
                        <td><code>config set-provider smtp</code></td>
                        <td>Configure SMTP credentials for <code>EmailTemplate</code> keys.</td>
                        <td><code>o config set-provider smtp --server host --port 587 --username u --password p</code></td>
                    </tr>
                    <tr>
                        <td><code>config set-provider graph</code></td>
                        <td>Configure Microsoft Graph client-credential auth for sending email/calendar invites.</td>
                        <td><code>o config set-provider graph --tenant-id t --client-id c --client-secret s</code></td>
                    </tr>
                    <tr>
                        <td><code>config auth-graph</code></td>
                        <td>Device-code login to Microsoft Graph, for sending email/creating calendar events (not for vault storage/sync).</td>
                        <td><code>o config auth-graph</code></td>
                    </tr>
                    <tr>
                        <td><code>config set-sync-remote</code> / <code>set-sync-token</code></td>
                        <td>Set the git remote used by <code>o sync</code>, and (for HTTPS remotes) store an access token.</td>
                        <td><code>o config set-sync-remote git@github.com:me/vault.git</code></td>
                    </tr>
                    <tr>
                        <td><code>config enable-auto-sync</code> / <code>disable-auto-sync</code></td>
                        <td>Automatically push to the sync remote after add/update/delete/import.</td>
                        <td><code>o config enable-auto-sync</code></td>
                    </tr>
                </tbody>
            </table>
        </section>

        <!-- KEY TYPES -->
        <section id="key-types">
            <h2>Key Types Overview</h2>
            <p>Each key in Opener has a type. Let's look at the basic ones before proceeding to advanced JSON templates.</p>

            <div class="grid-2">
                <div class="card">
                    <h3>Data (Clipboard)</h3>
                    <p>Used to store passwords, tokens, DB connections. Executing it copies the value securely to your clipboard.</p>
                    <pre><code>o add token "secret_key" -t Data
o token  # Copies to clipboard</code></pre>
                </div>
                <div class="card">
                    <h3>JsonData</h3>
                    <p>Similar to Data but designed for structured JSON. Copies the raw string to clipboard and pretty-prints it in the terminal.</p>
                    <pre><code>o add body "{\"id\":12}" -t JsonData
o body  # Prints & copies</code></pre>
                </div>
            </div>
        </section>

        <!-- WEBPATH -->
        <section id="webpath">
            <h2>Web Shortcuts (<code>WebPath</code>)</h2>
            <p>Allows opening frequently used URLs in your default browser. Supports rich dynamic parameters.</p>
            
            <h3>1. Position-based placeholders</h3>
            <pre><code>o add jira "https://jira.company.com/browse/{0}" -t WebPath
o jira PROJ-425  # Opens: https://jira.company.com/browse/PROJ-425</code></pre>

            <h3>2. Named placeholders</h3>
            <p>You can define placeholders using angle brackets <code>&lt;placeholder&gt;</code> and supply values via <code>name=value</code> format or sequentially.</p>
            <pre><code>o add api "https://api-<env>.domain.com/<region>/users" -t WebPath
o api env=dev region=us  # Opens: https://api-dev.domain.com/us/users</code></pre>

            <h3>3. URL Aliases and Default Parameters</h3>
            <p>Make commands short by setting default values or alias mappings. If you configure aliases, entering a short letter resolves to a full replacement.</p>
            <pre><code># Add key with configuration
o add api "https://api-<env>.domain.com/data" -t WebPath \
  --url-aliases '{"env":{"d":"-dev","u":"-uat","p":""}}' \
  --default-params '{"env":"d"}'

o api env=u  # Opens: https://api-uat.domain.com/data
o api        # Opens: https://api-dev.domain.com/data (uses default "d")</code></pre>
        </section>

        <!-- LOCALPATH -->
        <section id="localpath">
            <h2>Local Scripts & Paths (<code>LocalPath</code>)</h2>
            <p>Executes scripts, executables, files, or opens directories in your system explorer.</p>

            <h3>1. Opening folders or files</h3>
            <pre><code>o add logs "C:\Users\Username\Logs" -t LocalPath
o logs  # Opens the directory in explorer</code></pre>

            <h3>2. Running scripts with elevation</h3>
            <p>Supply the <code>-e</code> / <code>--elevated</code> flag, or create the key with elevation to automatically run it with admin/sudo privileges.</p>
            <pre><code>o add hosts "C:\Windows\System32\drivers\etc\hosts" -t LocalPath -e
o hosts  # Opens hosts in notepad as administrator</code></pre>
        </section>

        <!-- REST CLIENT -->
        <section id="rest">
            <h2>REST API client (<code>Rest</code>)</h2>
            <p>Execute HTTP requests from your terminal. Value must be a valid JSON matching this schema:</p>
            <pre><code>{
  "url": "https://api.github.com/users/{0}",
  "method": "GET",
  "headers": {
    "User-Agent": "Opener-CLI"
  },
  "body": ""
}</code></pre>

            <div class="callout">
                <div class="callout-title">Pro Tip</div>
                <p>Instead of manually typing this JSON, scroll down to our interactive **JSON Key Builder** to generate this structure with form textboxes!</p>
            </div>

            <h3>Referencing another stored key</h3>
            <p><code>{{key:name}}</code> in <code>url</code>, <code>headers</code>, or <code>body</code> resolves to the raw stored value of another key in your vault, looked up by name (case-insensitive). Keep an API token in its own <code>Data</code> key and reference it from as many <code>Rest</code> keys as you like, instead of copy-pasting the secret into each one.</p>
            <pre><code>{
  "url": "https://api.github.com/users/{0}",
  "method": "GET",
  "headers": {
    "Authorization": "Bearer {{key:githubtoken}}"
  }
}</code></pre>

            <h3>Chaining requests (login, then call)</h3>
            <p>Instead of a single request, the value can be a <code>{ "steps": [...] }</code> array. Each step can <code>extract</code> values from its JSON response using a simple dot-separated path (e.g. <code>"data.token"</code> or <code>"items[0].id"</code>), and later steps reference them as <code>{{varName}}</code> in their <code>url</code>, <code>headers</code>, or <code>body</code>. Only the last step's response is printed. If a non-final step fails, the whole chain aborts instead of continuing with bad data.</p>
            <pre><code>{
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
}</code></pre>
        </section>

        <!-- EMAIL TEMPLATES -->
        <section id="email">
            <h2>Email Templates (<code>EmailTemplate</code>)</h2>
            <p>Allows sending templated emails via **SMTP** or **Microsoft Graph API**. Value must be a valid JSON matching this schema:</p>
            <pre><code>{
  "to": "recipient@company.com",
  "cc": "",
  "bcc": "",
  "subject": "Alert: <level>",
  "body": "System report details: <details>",
  "attachmentPath": "",
  "provider": "system"
}</code></pre>

            <h3>Providers:</h3>
            <ul>
                <li><code>system</code> (default): Opens your default system email client (e.g., Mail, Outlook) using the <code>mailto:</code> protocol.</li>
                <li><code>smtp</code>: Connects to a custom SMTP server (configured via <code>o config set-provider smtp</code>).</li>
                <li><code>graph</code>: Sends directly via Microsoft Graph API. (Requires completing <code>o config auth-graph</code> device login).</li>
            </ul>
        </section>

        <!-- CALENDAR EVENTS -->
        <section id="calendar">
            <h2>Calendar Events (<code>CalendarEvent</code>)</h2>
            <p>Creates calendar invites or schedules meetings. Value must be a valid JSON matching this schema:</p>
            <pre><code>{
  "subject": "Sync Meeting",
  "body": "Discussion about development roadmap",
  "invitees": "personA@company.com, personB@company.com",
  "durationMinutes": 30,
  "availability": "busy",
  "provider": "system",
  "startTime": "tomorrow 10:00"
}</code></pre>

            <h3>Features:</h3>
            <ul>
                <li><strong>Dynamic Start Time</strong>: Supports relative inputs like <code>"tomorrow 10am"</code>, <code>"today 3pm"</code>, <code>"next monday 10:30"</code>, which resolve dynamically at execution time.</li>
                <li><code>system</code> provider: Generates an `.ics` file locally and opens it in your default system calendar (e.g., Apple Calendar, Outlook).</li>
                <li><code>graph</code> provider: Creates the event directly in your Microsoft 365 Exchange calendar.</li>
            </ul>
        </section>

        <!-- TOTP -->
        <section id="totp">
            <h2>Two-Factor Codes (<code>Totp</code>)</h2>
            <p>Stores a 2FA seed and generates the current 6-digit code using RFC 6238 - the same algorithm Google Authenticator and Authy use, so it's fully compatible without any special "registration" step. The value is just the base32 secret, or a full <code>otpauth://</code> URI (the secret is extracted automatically) - it's encrypted in the same vault as every other key, no separate secret store.</p>
            <pre><code>o add github JBSWY3DPEHPK3PXP -t Totp
o github            # copies the current code to your clipboard
o github -r         # prints the current code instead</code></pre>

            <div class="callout">
                <div class="callout-title">Something to consider</div>
                <p>Storing a TOTP seed alongside your other secrets means a compromised vault exposes both a password <em>and</em> its 2FA code together. This is best suited to lower-stakes accounts (internal tools, staging environments) rather than the account tied to the vault's own recovery, like your primary email or GitHub.</p>
            </div>
        </section>

        <!-- INTERACTIVE PICKER -->
        <section id="picker">
            <h2>Interactive Picker</h2>
            <p>Running <code>o</code> with no arguments opens a searchable picker over all your stored keys - type to filter, select one, and it runs exactly like typing its name would. If the console isn't interactive (e.g. output is redirected/piped), it falls back to printing a plain list instead of hanging.</p>
            <pre><code>o   # opens the picker</code></pre>
            <p>This makes <code>o</code> usable as a quick launcher outside a terminal entirely: on Windows, press <strong>Win+R</strong>, type <code>o</code>, and hit Enter - a console window opens with the picker ready to go.</p>
        </section>

        <!-- GIT SYNC -->
        <section id="sync">
            <h2>Git-Based Vault Sync</h2>
            <p>Pushes/pulls your already-encrypted vault file through a git remote, instead of relying on a cloud-storage client's local sync agent (OneDrive's Files-On-Demand, for example, breaks direct file I/O - see the cloud sync notes elsewhere in this doc). Git only ever sees ciphertext.</p>
            <pre><code># One-time setup
o config set-sync-remote git@github.com:me/opener-vault.git      # SSH: uses your existing SSH keys
o config set-sync-remote https://github.com/me/opener-vault.git  # HTTPS: needs a token

o config set-sync-token ghp_xxxxxxxxxxxx   # only needed for an https:// remote

# Manual sync
o sync push
o sync pull      # backs up your current vault to .backup/ first
o sync status

# Opt-in: push automatically after add/update/delete/import
o config enable-auto-sync
o config disable-auto-sync</code></pre>

            <h3>Notes</h3>
            <ul>
                <li>An SSH remote needs no extra setup on Opener's side - it uses your existing SSH agent/keys. An HTTPS remote's token is stored in your OS keychain, in a slot separate from your vault's own unlock password.</li>
                <li>Auto-sync never fails the command that triggered it - a push failure just prints a warning.</li>
                <li>A pull conflict (two machines changed the vault before syncing) isn't auto-merged, since merging an opaque encrypted blob is meaningless - it aborts cleanly and tells you to resolve it manually. Your data is safe either way, since a pull always backs up first.</li>
            </ul>
        </section>

        <!-- JSON BUILDER -->
        <section id="builder">
            <h2>Interactive JSON Key Builder</h2>
            <p class="text-muted">Avoid formatting syntax mistakes. Select your desired key type, fill out the form, and get the exact command to add the key.</p>

            <div class="card" style="margin-top: 24px;">
                <div class="form-group">
                    <label for="builderType">Key Type</label>
                    <select id="builderType">
                        <option value="email">EmailTemplate</option>
                        <option value="rest">Rest (API Client)</option>
                        <option value="calendar">CalendarEvent</option>
                    </select>
                </div>

                <div class="form-group">
                    <label for="builderKeyName">Key Name</label>
                    <input type="text" id="builderKeyName" placeholder="e.g. send-update or create-meeting">
                </div>

                <hr style="border-color: var(--border-color); margin: 24px 0;">

                <!-- Dynamic Form Fields will go here -->
                <div id="dynamicFields"></div>

                <div style="margin-top: 24px;">
                    <label>Generated Value JSON</label>
                    <pre style="margin-top: 6px;"><code id="jsonOutput">{}</code></pre>
                </div>

                <div style="margin-top: 16px;">
                    <label>Add Command (Copy & paste in your shell)</label>
                    <pre style="margin-top: 6px;"><code id="commandOutput">o add ...</code></pre>
                </div>
            </div>
        </section>
    </main>

    <!-- JAVASCRIPT FOR DYNAMIC INTERACTIONS -->
    <script>
        // Sidebar Navigation highlighting
        const sections = document.querySelectorAll('section');
        const navLinks = document.querySelectorAll('.nav-link');

        window.addEventListener('scroll', () => {
            let current = '';
            sections.forEach(section => {
                const sectionTop = section.offsetTop;
                const sectionHeight = section.clientHeight;
                if (pageYOffset >= (sectionTop - 120)) {
                    current = section.getAttribute('id');
                }
            });

            navLinks.forEach(link => {
                link.classList.remove('active');
                if (link.getAttribute('href').substring(1) === current) {
                    link.classList.add('active');
                }
            });
        });

        // Search Filter in Sidebar
        const searchInput = document.getElementById('sidebarSearch');
        searchInput.addEventListener('input', (e) => {
            const term = e.target.value.toLowerCase();
            const navItems = document.querySelectorAll('.nav-links li');
            
            navItems.forEach(item => {
                const text = item.textContent.toLowerCase();
                if (text.includes(term)) {
                    item.style.display = 'block';
                } else {
                    item.style.display = 'none';
                }
            });
        });

        // Add Copy Button to Pre tags
        document.querySelectorAll('pre').forEach(pre => {
            if (pre.querySelector('code').id === 'jsonOutput' || pre.querySelector('code').id === 'commandOutput') return;
            const btn = document.createElement('button');
            btn.className = 'copy-btn';
            btn.innerText = 'Copy';
            btn.addEventListener('click', () => {
                const text = pre.querySelector('code').innerText;
                navigator.clipboard.writeText(text);
                btn.innerText = 'Copied!';
                setTimeout(() => btn.innerText = 'Copy', 2000);
            });
            pre.appendChild(btn);
        });

        // Interactive Key Builder Data and Logic
        const builderType = document.getElementById('builderType');
        const builderKeyName = document.getElementById('builderKeyName');
        const dynamicFields = document.getElementById('dynamicFields');
        const jsonOutput = document.getElementById('jsonOutput');
        const commandOutput = document.getElementById('commandOutput');

        const fieldsConfig = {
            email: [
                { id: 'emailTo', label: 'To (Recipient Email)', type: 'text', placeholder: 'boss@company.com' },
                { id: 'emailCc', label: 'Cc', type: 'text', placeholder: '' },
                { id: 'emailBcc', label: 'Bcc', type: 'text', placeholder: '' },
                { id: 'emailSubject', label: 'Subject', type: 'text', placeholder: 'Weekly Update' },
                { id: 'emailBody', label: 'Body (Template / Plain text)', type: 'textarea', placeholder: 'Hi Team, here is...' },
                { id: 'emailAttachment', label: 'Attachment File Path', type: 'text', placeholder: 'optional_path/report.pdf' },
                { id: 'emailProvider', label: 'Provider', type: 'select', options: ['system', 'smtp', 'graph'] }
            ],
            rest: [
                { id: 'restUrl', label: 'API URL', type: 'text', placeholder: 'https://api.github.com/users/{0}' },
                { id: 'restMethod', label: 'HTTP Method', type: 'select', options: ['GET', 'POST', 'PUT', 'DELETE', 'PATCH'] },
                { id: 'restBody', label: 'Body Payload (JSON or text)', type: 'textarea', placeholder: '' },
                { id: 'restHeaders', label: 'Headers', type: 'headers' }
            ],
            calendar: [
                { id: 'calSubject', label: 'Subject', type: 'text', placeholder: '1-on-1 Sync' },
                { id: 'calBody', label: 'Meeting Description', type: 'textarea', placeholder: 'Discuss current targets...' },
                { id: 'calInvitees', label: 'Invitees (Comma-separated emails)', type: 'text', placeholder: 'alice@com.com, bob@com.com' },
                { id: 'calDuration', label: 'Duration (Minutes)', type: 'text', placeholder: '30' },
                { id: 'calAvailability', label: 'Availability Status', type: 'select', options: ['busy', 'free', 'tentative', 'oof'] },
                { id: 'calStartTime', label: 'Start Time (e.g. tomorrow 10am)', type: 'text', placeholder: 'tomorrow 10:00' },
                { id: 'calProvider', label: 'Provider', type: 'select', options: ['system', 'graph'] }
            ]
        };

        let restHeadersList = [{ key: '', value: '' }];

        function renderFields() {
            const type = builderType.value;
            const config = fieldsConfig[type];
            dynamicFields.innerHTML = '';

            config.forEach(field => {
                const group = document.createElement('div');
                group.className = 'form-group';
                
                const labelElement = document.createElement('label');
                labelElement.innerText = field.label;
                group.appendChild(labelElement);

                if (field.type === 'text') {
                    const input = document.createElement('input');
                    input.type = 'text';
                    input.id = field.id;
                    input.placeholder = field.placeholder;
                    input.addEventListener('input', updateOutputs);
                    group.appendChild(input);
                } else if (field.type === 'textarea') {
                    const textarea = document.createElement('textarea');
                    textarea.id = field.id;
                    textarea.rows = 4;
                    textarea.placeholder = field.placeholder;
                    textarea.addEventListener('input', updateOutputs);
                    group.appendChild(textarea);
                } else if (field.type === 'select') {
                    const select = document.createElement('select');
                    select.id = field.id;
                    field.options.forEach(opt => {
                        const option = document.createElement('option');
                        option.value = opt;
                        option.text = opt;
                        select.appendChild(option);
                    });
                    select.addEventListener('change', updateOutputs);
                    group.appendChild(select);
                } else if (field.type === 'headers') {
                    const container = document.createElement('div');
                    container.id = 'headersContainer';
                    group.appendChild(container);
                    
                    const addBtn = document.createElement('button');
                    addBtn.className = 'btn-secondary';
                    addBtn.type = 'button';
                    addBtn.innerText = '+ Add Header';
                    addBtn.addEventListener('click', () => {
                        restHeadersList.push({ key: '', value: '' });
                        renderHeaders();
                        updateOutputs();
                    });
                    group.appendChild(addBtn);
                    
                    renderHeaders();
                }

                dynamicFields.appendChild(group);
            });

            updateOutputs();
        }

        function renderHeaders() {
            const container = document.getElementById('headersContainer');
            if (!container) return;
            container.innerHTML = '';

            restHeadersList.forEach((header, index) => {
                const row = document.createElement('div');
                row.className = 'header-row';

                const keyInput = document.createElement('input');
                keyInput.type = 'text';
                keyInput.placeholder = 'Header Key';
                keyInput.value = header.key;
                keyInput.addEventListener('input', (e) => {
                    restHeadersList[index].key = e.target.value;
                    updateOutputs();
                });

                const valInput = document.createElement('input');
                valInput.type = 'text';
                valInput.placeholder = 'Header Value';
                valInput.value = header.value;
                valInput.addEventListener('input', (e) => {
                    restHeadersList[index].value = e.target.value;
                    updateOutputs();
                });

                const deleteBtn = document.createElement('button');
                deleteBtn.className = 'btn-icon';
                deleteBtn.type = 'button';
                deleteBtn.innerHTML = '×';
                deleteBtn.addEventListener('click', () => {
                    restHeadersList.splice(index, 1);
                    renderHeaders();
                    updateOutputs();
                });

                row.appendChild(keyInput);
                row.appendChild(valInput);
                row.appendChild(deleteBtn);
                container.appendChild(row);
            });
        }

        function updateOutputs() {
            const type = builderType.value;
            let keyName = builderKeyName.value.trim();
            if (!keyName) keyName = '<key>';

            let json = {};
            let typeFlag = 'Data';

            if (type === 'email') {
                typeFlag = 'EmailTemplate';
                json = {
                    to: document.getElementById('emailTo')?.value || '',
                    cc: document.getElementById('emailCc')?.value || '',
                    bcc: document.getElementById('emailBcc')?.value || '',
                    subject: document.getElementById('emailSubject')?.value || '',
                    body: document.getElementById('emailBody')?.value || '',
                    attachmentPath: document.getElementById('emailAttachment')?.value || '',
                    provider: document.getElementById('emailProvider')?.value || 'system'
                };
            } else if (type === 'rest') {
                typeFlag = 'Rest';
                const headers = {};
                restHeadersList.forEach(h => {
                    if (h.key.trim()) {
                        headers[h.key.trim()] = h.value;
                    }
                });
                json = {
                    url: document.getElementById('restUrl')?.value || '',
                    method: document.getElementById('restMethod')?.value || 'GET',
                    headers: headers,
                    body: document.getElementById('restBody')?.value || ''
                };
            } else if (type === 'calendar') {
                typeFlag = 'CalendarEvent';
                let dur = parseInt(document.getElementById('calDuration')?.value || '30', 10);
                if (isNaN(dur)) dur = 30;
                json = {
                    subject: document.getElementById('calSubject')?.value || '',
                    body: document.getElementById('calBody')?.value || '',
                    invitees: document.getElementById('calInvitees')?.value || '',
                    durationMinutes: dur,
                    availability: document.getElementById('calAvailability')?.value || 'busy',
                    provider: document.getElementById('calProvider')?.value || 'system',
                    startTime: document.getElementById('calStartTime')?.value || ''
                };
            }

            const jsonStr = JSON.stringify(json, null, 2);
            jsonOutput.innerText = jsonStr;

            const escapedJsonStr = jsonStr.replace(/'/g, "'\\''");
            const command = `o add ${keyName} '${escapedJsonStr}' -t ${typeFlag}`;
            commandOutput.innerText = command;
        }

        builderType.addEventListener('change', () => {
            restHeadersList = [{ key: '', value: '' }];
            renderFields();
        });
        builderKeyName.addEventListener('input', updateOutputs);

        // Initial setup
        renderFields();
    </script>
</body>
</html>
""";
}
