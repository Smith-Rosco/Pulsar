# PKI Plugin

**Plugin ID**: `com.pulsar.pki`
**Version**: `1.0.0`
**Type**: Core Plugin
**Author**: Pulsar Team

## Overview

The PKI plugin is Pulsar's core credential-injection plugin. It keeps the Pulsar-facing plugin contract thin and delegates runtime work to layered PKI services that validate requests, load secrets, decrypt them, build an injection plan, and execute the plan through Windows-specific adapters.

## Supported Actions

### `fill`

Inject a saved secret into the currently focused external application.

### `inject`

Legacy runtime alias for `fill`. It stays supported for compatibility but is not exposed as a separate UI action.

## Parameters

- `secretId` (required): GUID of the saved secret to inject
- `autoEnter` (optional): whether Enter is pressed after the password is injected; defaults to `false`
- `autoSubmit` (legacy alias): accepted as a compatibility alias for `autoEnter`

## Runtime Architecture

The PKI runtime is split into four layers:

1. `PkiPlugin`
   - exposes plugin metadata
   - preserves action compatibility (`fill` + `inject`)
   - delegates execution to `IPkiExecutionService`
2. `PkiExecutionService`
   - validates slot arguments and `PulsarContext`
   - loads the secret from the shared store
   - decrypts the secret
   - converts the request into a deterministic `InjectionPlan`
3. Shared PKI domain/application services
   - `IPkiSecretStore`
   - `ISecretProtector`
   - `IPkiSecretMetadataResolver`
   - `IInjectionExecutor`
   - `IFocusRestorer`
4. Windows adapters
   - `SecretRepository`
   - `CredentialsManager`
   - `SendKeysInjectionExecutor`
   - `WindowsFocusRestorer`
   - input/focus helper adapters under `Plugins/Core/Pki/Services/Input/`

## Injection Policy

PKI credential fill is explicitly SendKeys-first.

For multi-field credential injection, the supported path is:

1. hide the Pulsar launcher
2. restore focus to the captured target window
3. wait for a short stabilization delay
4. type the account value when present
5. press Tab
6. type the password value
7. optionally press Enter

UI Automation helpers may still exist in infrastructure for non-PKI scenarios, but PKI credential fill does not depend on UIA-first behavior.

## Shared Secret Management

Runtime and settings flows use the same PKI secret contracts:

- the same `secrets.json` payload shape is preserved
- the same DPAPI protection service is used for edit-time encryption and runtime decryption
- the same metadata resolver is used to merge persisted secrets, pending edits, and legacy label fallbacks

Secrets remain stored at `%AppData%\Pulsar\secrets.json`.

## Failure Boundaries

PKI execution distinguishes these stages internally:

- validation
- secret lookup
- decryption
- launcher hiding
- focus restoration
- injection execution

Errors are reported without logging plaintext account or password values.

## Security Notes

- secret encryption uses Windows DPAPI bound to the current user
- plaintext secret material is not persisted in logs
- `secretId` remains the stable slot reference; legacy display labels do not mutate stored identity

## Validation Coverage

PKI validation is covered by layered tests for:

- request validation and compatibility aliases
- secret lookup and decryption failures
- deterministic injection-plan generation
- SendKeys execution-stage failures
- secret store compatibility with existing payloads
- pending and persisted secret metadata resolution

## Last Updated

`2026-03-27`
