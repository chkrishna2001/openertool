# Project State

Last updated 2026-11-24 by "Add Template key type"

## Purpose & shape
- `o` (Opener): a cross-platform CLI key vault that also acts on what it stores (open URLs, run local paths, copy secrets, call REST APIs, send email, create calendar events, generate TOTP codes, fill text templates).
- C# / .NET (`net8.0;net10.0`), Native AOT publish (`PublishAot`), Spectre.Console UI, System.CommandLine. Also shipped as a NuGet global tool.
- Build/test: `dotnet test Opener.Tests/Opener.Tests.csproj`. Integration: `tests/integration_test.{sh,ps1}` (sandboxed via `OPENER_HOME` / `OPENER_DATA_DIR`).
- Release: `.github/workflows/publish.yml` (tests, AOT binaries, NuGet); `aot-validation.yml`; `deploy-docs.yml`. Installers in `scripts/`.

## Done
- Encrypted vault: local mode (DPAPI / machine key) and portable mode (AES-256-GCM + password, OS keychain with encrypted-file fallback).
- Key types: WebPath, LocalPath (incl. elevated, `.ps1` via PowerShell), Data, JsonData, Rest (headers, multi-step chains with `extract`), EmailTemplate, CalendarEvent, Totp, Template.
- Placeholders (`UrlTemplateResolver`): `{0}` positional and `<name>` named with global/per-key URL aliases and default params; used by WebPath, Rest url, EmailTemplate, CalendarEvent, Template.
- `{{key:name}}` references to another stored key in Rest (url/headers/body) and Template.
- Interactive picker (`o` with no args), git-based encrypted vault sync with opt-in auto-sync, backups, `o docs` generated HTML page.

## In progress
- Nothing.

## Remaining
- Rest resolves `{{key:name}}` before placeholders, so a referenced secret containing `{0}`/`<x>` text can be altered; Template already does placeholders first.
- Rest `headers`/`body` get `{{var}}`/`{{key:}}` but not `{0}`/`<name>` args.
- Optional: placeholders for LocalPath (placeholders consume args, the rest pass through as argv).

## Last session
- Added `Template` key type (`OKeyType.Template = 9`) via `UrlTemplateResolver.ResolveText` (strict `{N}` match: `{{0}}`, `{0:x}`, JSON/code braces stay literal). Placeholder warnings now go to stderr so `$(o key -r)` is clean. Fixed `$` in named values being treated as regex substitution tokens.
- Verified: all 126 unit tests pass on net8.0 and net10.0; sandboxed CLI smoke test of add/-r/stderr. Committed in 30963b4 (not pushed; AOT publish not run).

## Next task
- Decide whether Rest should resolve placeholders before `{{key:name}}` (see Remaining); done when the order is changed or documented and a test covers a secret containing `<x>`.

## Decisions & gotchas
- Data, JsonData and Totp are never templated: stored value comes back byte-for-byte (secrets can contain `{0}` / `<x>`; no escape syntax exists).
- `Resolve` (WebPath etc.) uses `string.Format` for `{N}`: stray braces throw and fall back with a warning, `{{`/`}}` collapse. `ResolveText` (Template) avoids `string.Format` for that reason.
- `OKeyType` values are persisted as numbers: only append new members, never renumber.
- Warnings must go to stderr (`ActionService.WriteWarning`); stdout is the value for `-r` consumers (scripts, agents).
- CLI smoke tests must set `OPENER_HOME` and `OPENER_DATA_DIR` to a sandbox, or they write to the real vault.
