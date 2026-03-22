## 1. Metadata Model

- [x] 1.1 Add action-level slot parameter metadata types to the plugin metadata model and registry access layer.
- [x] 1.2 Define parameter metadata fields for labels, descriptions, required state, examples, placeholders, validation hints, and picker intents.
- [x] 1.3 Add support for retrieving action metadata for a plugin and action combination from the metadata registry.

## 2. Built-in Plugin Definitions

- [x] 2.1 Add canonical action parameter metadata for `com.pulsar.winswitcher`, including distinct definitions for `switch`, `launch`, and `activate`.
- [x] 2.2 Add canonical action parameter metadata for `com.pulsar.command`, `com.pulsar.pki`, `com.pulsar.bookmarklet`, and `com.pulsar.vbarunner`.
- [x] 2.3 Reconcile mismatched legacy parameter names with canonical metadata names and preserve backward compatibility through alias handling or migration.

## 3. Slot Editor UX

- [x] 3.1 Replace hard-coded plugin parameter templates in the slot editor with action-aware metadata-driven parameter rendering.
- [x] 3.2 Add required/optional or advanced grouping, field-level descriptions, and example/input-format guidance to slot parameter editing.
- [x] 3.3 Integrate supported specialized picker affordances for metadata-declared intents such as process, file, and secret selection.

## 4. Validation and Save Flow

- [x] 4.1 Extend configuration validation to evaluate slot args against metadata-defined action parameter requirements.
- [x] 4.2 Surface actionable slot-level validation feedback in the settings experience before plugin execution.
- [x] 4.3 Ensure runtime plugin validation remains intact as a final safeguard after editor and save-time validation are added.

## 5. Compatibility and Verification

- [x] 5.1 Verify existing saved slot configurations continue to load without data loss under the new editor model.
- [x] 5.2 Add or update tests covering action-aware parameter rendering inputs, slot validation rules, and backward-compatible parameter aliases.
- [x] 5.3 Run project validation for the affected surface area and confirm the change is ready for user-facing implementation review.
