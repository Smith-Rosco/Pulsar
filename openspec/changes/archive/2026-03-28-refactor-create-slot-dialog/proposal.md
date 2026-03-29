## Why

The current Create Slot dialog exposes too many equal-weight visual regions and too much always-visible explanatory text at once, which raises cognitive load during a task that should feel guided and incremental. As plugin types continue to grow, the current left-column selection and top preview structure also risk scaling poorly and making the primary creation flow harder to scan.

## What Changes

- Refocus the Create Slot dialog around a clearer step-by-step creation flow: choose slot type, choose action, complete required setup, then optionally polish appearance.
- Reduce reliance on card-style containers and repeated bordered boxes in favor of typography, spacing, alignment, and lighter separators.
- Move low-priority instructional text, examples, and appearance guidance into deeper disclosure patterns such as tooltips or secondary reveal affordances.
- Simplify or remove redundant explanatory blocks, duplicate headings, and non-essential summary content that competes with required configuration tasks.
- Rework the plugin type selection surface so it remains scannable and usable as the number of available plugin types increases.
- Rebalance preview and validation presentation so preview supports the workflow without dominating it, while critical validation remains visible in-context.

## Capabilities

### New Capabilities
- None.

### Modified Capabilities
- `slot-editor-information-hierarchy`: tighten the Create Slot dialog hierarchy so slot creation reads as a single guided workflow rather than a stack of competing preview and configuration regions.
- `slot-editor-progressive-disclosure`: push low-priority helper copy and appearance guidance into secondary disclosure while keeping required-state and validation cues visibly anchored in the active editing path.
- `slot-editor-density-and-layout`: reduce bordered card treatments and redundant headings in Create Slot, and introduce a denser, more scalable layout for plugin type selection and simple configuration rows.

## Impact

- Affected UI: `Pulsar/Pulsar/Views/Dialogs/Contents/AddSlotContent.xaml`
- Likely affected supporting logic: `Pulsar/Pulsar/ViewModels/Dialogs/AddSlotViewModel.cs`
- Potentially affected shared styles/resources: `Pulsar/Pulsar/Styles/SlotStyles.xaml`, `Pulsar/Pulsar/Styles/ButtonStyles.xaml`
- Validation, preview, and parameter metadata contracts remain in place, but their visual presentation in the Create Slot dialog will be re-prioritized.
