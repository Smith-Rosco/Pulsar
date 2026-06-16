# Input Injection Architecture

**Status**: Published
**Scope**: Architecture
**Applies To**: PKI plugin, text injection scenarios, modifier key detection
**Last Updated**: 2026-03-27

---

## Overview

Pulsar supports multiple input-injection techniques, but PKI credential fill now has an explicit architectural rule: multi-field PKI injection is SendKeys-first.

This document distinguishes between:

- general input-injection capabilities in the codebase
- the stricter PKI credential-injection policy used by `com.pulsar.pki`

---

## PKI Injection Policy

For PKI credential fill, Pulsar uses a deterministic execution plan built by application services and executed through Windows adapters.

The supported PKI sequence is:

1. hide the launcher
2. restore focus to the captured target window
3. wait for stabilization
4. send the account text when present
5. send `Tab`
6. send the password text
7. optionally send `Enter`

Why SendKeys-first for PKI:

- multi-field fill depends on queue-ordered key processing
- UIA `SetValue` replaces content instead of behaving like typed input
- rapid focus transitions make UIA unreliable for account -> tab -> password flows
- SendKeys respects the OS input queue and matches the proven runtime behavior

For PKI, UIA-first execution is not a supported policy.

---

## PKI Layering

PKI runtime execution is split into these layers:

### Plugin Adapter

`PkiPlugin` is a thin adapter responsible for:

- plugin metadata
- action dispatch (`fill`, legacy `inject`)
- delegating runtime work to `IPkiExecutionService`

### Application Layer

`PkiExecutionService` is responsible for:

- validating slot arguments
- resolving the requested secret
- decrypting the stored payload
- converting the request into a deterministic `InjectionPlan`

### Shared PKI Contracts

- `IPkiSecretStore`
- `ISecretProtector`
- `IPkiSecretMetadataResolver`
- `IInjectionExecutor`
- `IFocusRestorer`

These contracts are shared between runtime and settings flows so storage, protection, and metadata logic remain consistent.

### Windows Infrastructure

- `SecretRepository`
- `CredentialsManager`
- `SendKeysInjectionExecutor`
- `WindowsFocusRestorer`
- input helpers under `Plugins/Core/Pki/Services/Input/`

---

## Failure Boundaries

PKI execution surfaces stage-specific outcomes for:

- validation
- secret lookup
- decryption
- launcher hiding
- focus restoration
- injection execution

This makes failures easier to test and diagnose without exposing plaintext secret material.

---

## Modifier Key State Detection (RDP Fix)

In Remote Desktop environments, modifier states can desynchronize between client and host. Pulsar mitigates this with a hybrid modifier-state tracker in `GlobalKeyboardHook`.

### Configuration

In `Profiles.json`:

```json
{
  "Settings": {
    "Input": {
      "ModifierStateMode": "Hybrid",
      "EnableModifierStateLogging": false
    }
  }
}
```

- `Hybrid`: recommended; uses tracked modifier state
- `Legacy`: relies on `GetKeyState()` for compatibility

---

## Focus Management

PKI fill relies on captured invocation context instead of querying live state inside the plugin.

Focus flow:

1. the launcher captures the target window handle before Pulsar takes focus
2. PKI execution hides the launcher
3. focus restoration returns to `PulsarContext.TargetWindowHandle`
4. a short delay allows the target window to stabilize
5. SendKeys-based injection begins

This preserves the plugin-system invariant that runtime plugins use `PulsarContext` rather than querying live window state directly.

---

## Related Documents

- `Docs/Plugins/PkiPlugin.md`
- `Docs/archive/PKI_REFACTORING_AND_BUGS.md`
- `Docs/architecture/PLUGIN_SYSTEM.md`
- `Docs/lessons/RDP_MODIFIER_KEY_STUCK.md`

---

## Change History

- `v1.0.0` (2026-03-03): initial extraction from agent guidance
- `v1.1.0` (2026-03-09): added RDP modifier-state guidance
- `v1.2.0` (2026-03-27): documented layered PKI runtime and SendKeys-first PKI policy
