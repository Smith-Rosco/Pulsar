## Why

The new metadata-driven plugin parameter editor improves correctness and discoverability, but it now overloads expanded slot cards with too many fields and too much explanatory text. This hurts the primary settings job on the slots page: scanning, comparing, and lightly adjusting many slots without losing spatial rhythm.

## What Changes

- Split slot editing into two deliberate layers: a compact inline quick-edit experience inside the slot card, and a dedicated full-configuration dialog for parameter-heavy actions.
- Redefine the slot card's default and expanded states around scanability, status clarity, and high-frequency edits rather than full-form authoring.
- Introduce parameter presentation rules that decide which fields belong in quick edit versus full configuration based on complexity, explanation density, picker requirements, and dependency relationships.
- Add summary and status affordances so each slot communicates configuration health and key parameter state without requiring expansion.
- Capture plugin authoring requirements for slot parameter metadata so built-in and future plugins expose enough information to support layered presentation consistently.

## Capabilities

### New Capabilities
- `slot-parameter-authoring`: Define the list-facing slot parameter authoring experience around scanable summaries, lightweight inline edits, and escalation into full configuration when needed.
- `layered-slot-parameter-editing`: Present slot editing as a two-level experience with compact list-focused editing and a dedicated deep-configuration surface.
- `plugin-slot-parameter-metadata-contract`: Define the metadata plugin authors must provide so slot parameters can be summarized, prioritized, and escalated into advanced editing consistently.

### Modified Capabilities
<!-- None -->

## Impact

- Affected UI: `Pulsar/Pulsar/Views/Pages/SettingsSlotsPage.xaml`, slot card layout/state handling, and a new or extended dialog surface under `Pulsar/Pulsar/Views/Dialogs/`.
- Affected view models/models: slot editor view-model state, parameter grouping/summarization logic, and metadata-driven complexity classification used to decide quick-edit versus full-config presentation.
- Affected specs/docs: a new capability spec for layered slot editing, a new capability spec covering plugin metadata requirements, and updated requirements for slot parameter authoring.
- Affected plugins: built-in plugin metadata definitions and plugin development guidance so plugin authors provide summary-friendly labels, importance tiers, and advanced-editor hints.
