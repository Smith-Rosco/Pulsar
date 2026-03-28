## Why

The current Create Slot dialog is functionally complete but visually over-structured: too many bordered panels, repeated section containers, and stacked cards compete with the actual editing task. That makes the flow feel denser than necessary and works against the intended Apple-inspired direction, where hierarchy should come primarily from typography, spacing, and alignment rather than repeated surfaces.

## What Changes

- Redesign the Create Slot flow around typography-first hierarchy, using fewer container treatments and stronger use of spacing, alignment, and text scale
- Replace the current two-step wizard framing with a simpler progressive editing flow that keeps type selection, required configuration, and final polish in one coherent surface
- Reduce card-on-card presentation in parameter editing, preview, appearance, and summary sections so content reads as an editor rather than a stack of panels
- Rebalance information priority so required behavior setup is primary, appearance customization is secondary, and summaries become lightweight supporting context
- Align Create Slot and Slot Configuration dialog language so both editing surfaces follow the same calmer, lower-noise structure

## Capabilities

### New Capabilities

<!-- None -->

### Modified Capabilities
- `slot-editor-information-hierarchy`: change full configuration and creation surfaces to emphasize typography-led hierarchy, clearer primary-vs-secondary information priority, and fewer competing summary blocks
- `slot-editor-density-and-layout`: reduce excessive containerization, minimize redundant headings, and define denser-but-clear row and section layout rules for creation/editing flows
- `slot-editor-progressive-disclosure`: move lower-priority appearance and helper content behind lighter disclosure while keeping required-state and validation cues visible

## Impact

- `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml` - main Create Slot layout overhaul
- `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml.cs` - event wiring may need small adjustments for the new structure
- `Pulsar/Pulsar/ViewModels/Dialogs/AddSlotViewModel.cs` - wizard-oriented copy and state may be simplified to support a single-surface flow
- `Pulsar/Pulsar/Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml` - align edit dialog hierarchy with the new Create Slot design language
- `Pulsar/Pulsar/ViewModels/Dialogs/SlotConfigurationDialogViewModel.cs` - may need small presentation properties to support revised grouping and helper text
- No changes to plugin execution semantics, slot persistence, or core plugin metadata contracts
