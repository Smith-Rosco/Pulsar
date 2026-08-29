# Configuration Backup & Restore

Date: 2026-08-29
Scope: User-facing feature + service seam
Status: Implemented (format v1)

## Overview

Settings → About → **Configuration Backup** lets a user export or restore the whole
Pulsar configuration as a single ZIP package:

- `Profiles.json` — the full config snapshot (profiles, slots, hotkeys, plugin settings).
- `secrets.json` — the PKI credential store, **when the user opts in**.

Backups are versioned (`manifest.json` → `formatVersion`), so a future format can
reject old readers / be migrated forward instead of silently misreading.

## Package layout (format v1)

| Entry | Present when | Content |
|-------|--------------|---------|
| `manifest.json` | always | format version, source app version, `createdAtUtc`, `containsSecrets`, `secretsProtected`, KDF metadata |
| `Profiles.json` | always | same JSON shape as the live file |
| `secrets.json` | store non-empty + no password | raw `Dictionary<Guid, SecretPayload>` (DPAPI blobs) |
| `secrets.protected.json` | store non-empty + password | per-secret AES-GCM sealed blobs |

## Why password protection exists (DPAPI portability)

Live secret blobs are sealed with Windows DPAPI at `CurrentUser` scope, so a raw
`secrets.json` backup can only be decrypted on the **same Windows user account and
machine**. To make a backup portable:

- **Export with password**: each secret is decrypted via the local protector, then
  re-sealed with a key derived from the password — PBKDF2-SHA256 (210k iterations,
  random 16-byte salt) + AES-256-GCM (12-byte nonce per secret, 16-byte tag).
- **Import with password**: secrets are unsealed with the password, then re-encrypted
  through the **target machine's** protector (fresh DPAPI), so the restored store
  works on the new machine.

`Label` / `Account` stay plaintext in the package exactly as in the live store; only
the password blob is protected.

## Semantics

- **Import is replace-all** for `Profiles.json`. The package represents a full state,
  not a merge. A confirmation dialog shows the package summary (profiles / slots /
  secrets / source version / creation time) before applying.
- **Secrets are only touched when the package contains them.** A package without
  secrets leaves the current credential store intact (shown in the confirm dialog).
- The current secret map is staged before import; if the config commit fails
  (e.g. validation), secrets are rolled back.
- `ConfigService.SaveAsync` runs the normal validation pipeline and keeps the rolling
  `.bak`, so an imported config gets the same safety net as any other write.
- After a successful import the app **prompts to restart** — hotkeys, theme, plugin
  settings and the menu all hold in-memory state, and the restart guarantees they are
  rebuilt from the restored file (see `HOTKEY_SERVICE_STALE_CONFIG_OVERWRITE.md`).

## Service seam

- `IConfigBackupService` (`Services/Interfaces/`) — `ExportAsync`, `InspectAsync`,
  `ImportAsync`; `InspectAsync` reads a package without applying it so the UI can
  prompt for the password and show a summary first.
- `ConfigBackupService` (`Services/`) — implementation; depends only on
  `IConfigService`, `IPkiSecretStore`, `ISecretProtector` (all already registered).
- `AboutViewModel` — export/import commands, file dialogs, option/password dialogs,
  localized error mapping.
- Tests: `Pulsar.Tests/Services/ConfigBackupServiceTests.cs` (round-trip, password
  portability across protectors, invalid-package/version/config/secrets rejection,
  wrong-password rollback safety, no-secrets semantics).

## Security notes

- The password is never persisted; KDF salt + iterations travel in the manifest.
- Wrong password / tampered ciphertext surfaces as `WrongPassword` via
  `CryptographicException` from AES-GCM authentication.
- A password-protected package still reveals profile/slot structure and secret
  labels/accounts (plaintext, same as live `secrets.json`). Treat the file as
  sensitive and warn users not to share it casually.

## Future work

- Optional inclusion of external plugin packages in the same ZIP.
- `--portable` mode (config path override) so a backup can double as a live folder.
- Auto-backup on major version upgrades / before destructive operations.