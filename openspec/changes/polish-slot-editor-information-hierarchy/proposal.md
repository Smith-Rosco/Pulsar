## Why

The layered slot editor work improved the structural split between inline quick edit and full configuration, but the resulting UI still carries too much explanatory copy, too many competing section headers, and too much vertical weight. The current information hierarchy makes both the slots list and the full configuration dialog feel heavier than their jobs require, which increases scan cost and user hesitation during routine slot management.

## What Changes

- Redefine the collapsed slot card as a scan-first surface that shows only identity and health state, removing non-essential explanatory prose from the default view.
- Refine the expanded slot card into a compact quick-edit workspace with a restrained hierarchy, fewer visible headings, and denser field composition for high-frequency edits.
- Reorganize the full configuration dialog so its sections communicate a clearer editing model, separating always-visible status from on-demand help.
- Shift descriptive field guidance, examples, and low-priority instructional text toward tooltip or similar secondary disclosure patterns while keeping validation and required-state information visible.
- Formalize presentation rules for when slot editor information is always visible, compactly summarized, or disclosed on demand so the metadata-driven editor remains consistent across plugins.

## Capabilities

### New Capabilities
- `slot-editor-information-hierarchy`: Define how collapsed cards, expanded quick edit, and full configuration each present only the information appropriate to their layer.
- `slot-editor-progressive-disclosure`: Define which help and guidance content remains visible versus moves into tooltip-style secondary disclosure.
- `slot-editor-density-and-layout`: Define compact layout expectations for high-frequency slot editing, including row-based field presentation and vertical rhythm constraints.

### Modified Capabilities
- `layered-slot-parameter-editing`: Tighten the layered editing requirements so the quick-edit layer is explicitly scanable, compact, and free of redundant explanatory prose.
- `slot-parameter-authoring`: Refine authoring expectations so summaries, help text, and field grouping align with the new information hierarchy and disclosure model.

## Impact

- Affected UI: `Pulsar/Pulsar/Views/Pages/SettingsSlotsPage.xaml`, `Pulsar/Pulsar/Views/Controls/ExpandableCard.xaml`, and `Pulsar/Pulsar/Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml`.
- Affected view models/models: slot presentation state, summary-token usage, parameter help metadata consumption, and any view-model logic that classifies inline versus dialog content.
- Affected specs/docs: new capability specs for hierarchy, disclosure, and density expectations plus deltas for layered editing and slot parameter authoring.
- Affected design language: tooltip usage patterns, visible validation conventions, and compact field-layout guidance for metadata-driven forms.
