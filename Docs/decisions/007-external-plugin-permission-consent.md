# ADR-007: External Plugin Permission Consent

**Status**: Accepted
**Date**: 2026-08-16
**Deciders**: Pulsar Development Team

---

## Context

External plugin DLLs execute in-process with full CLR trust. The loader previously
instantiated every discovered plugin type during discovery to read metadata, which
meant untrusted constructors ran before the user saw a permission prompt. The
manifest contained a `permissions` list, but nothing enforced it at runtime.

## Decision

1. External plugin descriptors are built from `plugin.manifest.json` and reflection only; no plugin instance is created during discovery.
2. External plugins cannot declare the Core tier.
3. Well-known permission tokens are defined in `PluginPermissions`. Unknown tokens fail closed.
4. The Settings UI inspects a ZIP before installation, displays the requested permissions, and requires explicit approval.
5. Approved permissions are persisted in `PluginProfile.GrantedPermissions`.
6. `PluginExecutionPipeline` blocks execution with `PluginErrorCode.AccessDenied` when a manifest permission is missing.
7. First activation may register richer metadata from `IPluginMetadataProvider`; this happens after consent.
8. Uninstall revokes persisted grants.

## Consequences

- External plugin constructors never run before user consent.
- A package that requests unknown or unapproved permissions fails installation or execution.
- External plugin metadata is initially manifest-only; action metadata may be incomplete until first activation.

## Future Work

- Package signatures / publisher trust.
- Per-action permission checks inside `PluginExecutionContext`.

---

**Change History**:
- v1.0.0 (2026-08-16): Initial version
