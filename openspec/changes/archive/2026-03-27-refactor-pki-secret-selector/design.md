## Context

Slot parameter editors in both add-slot and edit-slot dialogs currently use a generic `TextBox + picker button` template for every picker-backed field. For PKI `secretId`, this leaks the raw stored GUID into a control that implies free-form editing, while the actual user intent is to choose a secret reference and see human-readable metadata. The current picker flow also writes directly into `slot.Args`, bypassing the `PluginSlot` notification mechanism that `SlotParameterEditorField` depends on for UI refresh.

This change crosses shared parameter rendering, slot mutation behavior, and PKI-specific presentation rules, so it benefits from an explicit design before implementation.

## Goals / Non-Goals

**Goals:**
- Represent secret-backed slot parameters as selector UI, not editable raw text.
- Preserve stored configuration as `secretId` while exposing a separate display layer for label/account metadata.
- Ensure picker-triggered updates raise the same notifications as typed edits so dialogs, validation, and summaries refresh immediately.
- Stop relying on `slot.Label` as a fallback channel for showing the selected secret name.
- Keep add-slot and edit-slot experiences consistent by sharing the same parameter rendering rules.

**Non-Goals:**
- Redesign the secret creation/edit dialogs themselves.
- Change runtime PKI injection behavior or secret storage format.
- Introduce new external dependencies or a broad metadata-schema overhaul beyond what is needed for selector rendering.

## Decisions

### 1. Split stored parameter value from display value
`SlotParameterEditorField` will continue to own the persisted value through `Value`, but it should also expose selector-oriented display properties such as `DisplayValue`, `SecondaryDisplayValue`, `IsReadOnlySelector`, and empty-state text. For `SlotPickerIntent.Secret`, these properties resolve the selected secret's label/account using a dedicated lookup path rather than returning the raw GUID.

Why this approach:
- It preserves the configuration contract expected by plugins.
- It avoids teaching the UI to parse GUIDs or fetch secret metadata ad hoc.
- It keeps summary and validation logic close to the field model.

Alternatives considered:
- Reuse `Value` and format the GUID into a label in XAML: rejected because it mixes storage and presentation and does not solve refresh semantics.
- Overwrite `slot.Label` with the secret name everywhere: rejected because slot naming and parameter display are different concerns.

### 2. Use template branching for picker-backed selectors
The shared parameter templates in `SlotConfigurationDialogContent` and `AddSlotContent` should branch between editable text input and selector-style presentation. Secret parameters should render as a display surface with primary/secondary text and action buttons such as `Select` or `Change`, while file/process pickers can continue using the existing mixed text-entry pattern unless explicitly migrated later.

Why this approach:
- It fixes the PKI UX without destabilizing all other picker-backed fields.
- It creates a reusable selector pattern for future non-text parameters.
- It keeps current visual structure mostly intact while correcting semantics.

Alternatives considered:
- Introduce a new control library abstraction for all field editors immediately: rejected as too large for the current fix.
- Make the `TextBox` read-only only for secret fields: rejected because it still looks like a text editor and does not expose richer metadata.

### 3. Centralize argument writes behind a notification-safe path
Picker flows should stop mutating `slot.Args[...]` directly for user-facing parameter changes. Instead, parameter updates should go through `PluginSlot`'s indexer or a small helper such as `SetArgument`, ensuring `Item[]` change notifications fire consistently.

Why this approach:
- It fixes the observed stale UI bug at the source.
- It improves consistency across future picker flows.
- It reduces hidden coupling between field refresh logic and mutation sites.

Alternatives considered:
- Manually call `OnPropertyChanged` after direct dictionary writes: rejected because it is easy to forget and spreads fragile logic.

### 4. Resolve secret display metadata from a dedicated source of truth
Secret selector display should be built from a lookup that merges persisted secrets and pending unsaved edits, using the same effective source set as `SecretPickerViewModel`. The field model or an injected resolver should expose the selected secret label/account for display without changing the stored `secretId`.

Why this approach:
- It keeps the editor accurate even when the user creates or edits a secret before saving all settings.
- It avoids duplicating secret-label fallback rules across views.
- It decouples secret presentation from slot title management.

Alternatives considered:
- Resolve only against persisted secrets: rejected because pending new/edit flows would appear stale.
- Store secret label redundantly in slot args: rejected because it introduces drift risk and complicates migration.

## Risks / Trade-offs

- [Shared field model grows UI-specific properties] -> Mitigate by limiting new properties to selector presentation and keeping raw persistence logic unchanged.
- [Secret metadata lookup becomes unavailable in some dialog construction paths] -> Mitigate by defining a small resolver contract or precomputed map that both add/edit dialogs can supply consistently.
- [Partial migration leaves add-slot and edit-slot inconsistent] -> Mitigate by updating both shared templates within the same change.
- [Changing slot-label fallback may alter what some existing slots display] -> Mitigate by scoping the change to parameter display first and preserving current slot labels unless the user explicitly edits them.
- [Safe-state summaries may conflict with richer on-screen labels] -> Mitigate by keeping summaries/logging safe while allowing in-dialog display of non-secret metadata such as label/account.

## Migration Plan

- No persisted config migration is required because stored `secretId` values remain unchanged.
- Update dialog rendering and field models behind the existing PKI parameter metadata.
- Validate behavior against existing slots with saved secrets and pending unsaved secrets.
- Rollback is low risk: revert selector-template changes and field display helpers while preserving data compatibility.

## Open Questions

- Should secret labels be considered sensitive enough to hide from summary chips outside the edit dialog, or is showing the label in local UI acceptable?
- Do we want a dedicated `EditorKind` metadata enum now, or should this change rely on `PickerIntent.Secret` plus field-derived flags and defer broader metadata expansion?
