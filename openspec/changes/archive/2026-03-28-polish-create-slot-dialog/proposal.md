## Why

The recent Create Slot dialog refactor improved structure, but the result still over-weights the top region with explanatory copy and a broad validation banner, which distracts from the user's actual task: choosing a slot type and completing required setup. The plugin type picker also contains a rendering defect where type icons display as four square glyph boxes because the dialog bypasses the shared icon rendering pipeline and treats hex icon keys as literal text.

## What Changes

- Further reduce the visual weight of the Create Slot header so it behaves as a lightweight workflow/status bar rather than a competing content region.
- Shrink and reposition the live slot preview into a compact supporting surface that stays visible without dominating the dialog.
- Reorganize plugin type selection into a scan-first categorized surface with consistent card sizing so the picker scales cleanly as plugin count grows.
- Replace the current raw glyph text rendering in plugin type cards with the shared icon-key rendering path used elsewhere in Pulsar, so hex icon keys, emoji, and file-backed icons render consistently.
- Rebalance validation presentation so critical correctness signals remain visible while normal draft-state hints stop occupying prime visual real estate.
- Clarify the right-hand authoring flow around selected type context, action choice, required details, and secondary polish.

## Capabilities

### New Capabilities
- `slot-editor-shared-icon-rendering`: ensure Create Slot and related picker surfaces render slot/plugin icon keys through the shared icon interpretation pipeline instead of direct raw-text glyph rendering.

### Modified Capabilities
- `slot-editor-information-hierarchy`: further reduce header prominence, make the main authoring path visually dominant, and keep preview/validation as supporting context rather than peer regions.
- `slot-editor-progressive-disclosure`: demote non-critical helper copy and draft-state feedback out of the header while keeping important validation cues visible near the active workflow.
- `slot-editor-density-and-layout`: restructure plugin type selection into a more uniform, scan-first layout with consistent card sizing and clearer separation from the configuration column.

## Impact

- Affected UI: `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml`
- Likely affected supporting logic: `Pulsar/Pulsar/ViewModels/Dialogs/AddSlotViewModel.cs`, `Pulsar/Pulsar/ViewModels/SettingsViewModel.cs`
- Likely affected shared rendering path: `Pulsar/Pulsar/Helpers/IconHelper.cs`, `Pulsar/Pulsar/Views/Controls/SlotOrb.xaml.cs`
- New or updated delta specs under `openspec/changes/polish-create-slot-dialog/specs/`
- No intended changes to slot persistence, plugin metadata contracts, or picker command behavior
