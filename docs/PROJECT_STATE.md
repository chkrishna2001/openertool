# Project State

Last updated 2026-11-24 by "Release 1.2.0"

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
- Rest url/headers/body resolve in one pass (`UrlTemplateResolver.ResolveSinglePass` via `ActionService.ResolveRestText`): args, `<name>` values, key references and extracted vars are inserted once and never rescanned.
- Interactive picker (`o` with no args), git-based encrypted vault sync with opt-in auto-sync, backups, `o docs` generated HTML page.

## In progress
- Nothing.

## Remaining
- Template and WebPath still resolve in two passes (`{0}` then `<name>`), so a CLI arg containing `<x>` text gets rescanned; Template should move to `ResolveSinglePass`.
- Optional: placeholders for LocalPath (placeholders consume args, the rest pass through as argv).

## Last session
- Released 1.2.0: `Template` key type, stderr warnings, `$` fix, and Rest single-pass substitution (one tokenizing pass over url/header/body text; a referenced secret reaches the request unchanged and an extracted value containing `{{key:...}}` is not resolved).
- Verified: all 130 unit tests pass on net8.0 and net10.0 on merged main. Version bumped in `Opener.csproj`, CHANGELOG section `[1.2.0]`, tag `v1.2.0` pushed (triggers NuGet publish, AOT binaries, GitHub release).
- Release steps: bump `<Version>` in `Opener/Opener.csproj`, rename `[Unreleased]` to `## [x.y.z] - date` in CHANGELOG (publish.yml reads release notes from that heading), push main, then push tag `vx.y.z`.

## Next task
- Switch `ActionService.ResolveTemplate` to `UrlTemplateResolver.ResolveSinglePass` (placeholders on, `{{key:}}` only); done when a test shows a Template CLI arg containing `<x>` / `{{key:x}}` stays literal and existing Template tests pass.

## Decisions & gotchas
- Data, JsonData and Totp are never templated: stored value comes back byte-for-byte (secrets can contain `{0}` / `<x>`; no escape syntax exists).
- `Resolve` (WebPath etc.) uses `string.Format` for `{N}`: stray braces throw and fall back with a warning, `{{`/`}}` collapse. `ResolveText` (Template) avoids `string.Format` for that reason.
- Values from Rest `extract` come from the server: they must never be rescanned for `{{key:name}}`, or a response could exfiltrate any vault secret into the next request. Never "fix" ordering by swapping passes; keep one pass over the original text.
- Rest `headers`/`body` deliberately get no `{0}`/`<name>` (XML bodies are full of `<tag>`); only `{{key:}}`/`{{var}}`. In a Rest url only an exact `{N}` is a placeholder (no `string.Format`).
- `OKeyType` values are persisted as numbers: only append new members, never renumber.
- Warnings must go to stderr (`ActionService.WriteWarning`); stdout is the value for `-r` consumers (scripts, agents).
- CLI smoke tests must set `OPENER_HOME` and `OPENER_DATA_DIR` to a sandbox, or they write to the real vault.
