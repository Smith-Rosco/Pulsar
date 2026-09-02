## Why

The slot configuration page (Settings → Slots) was recently migrated from a card list to a WYSIWYG radial wheel editor (`SlotWheelEditor`). While the wheel matches the runtime radial menu's layout, the editor still has two classes of problems:

1. **Visual/interaction quality is weak.** The editor wheel looks flat and utilitarian next to the runtime menu: empty slots are faint dashed circles with a bare "+", there is no hover feedback on orbs, the center area shows a raw "Center" text hint instead of a recognizable center-state, occupied slots show no position identity, and the pager/guide ring carry no hierarchy. The user explicitly wants a redesign of this visualization.

2. **Architecture debt accumulates.** `SlotWheelEditorViewModel` is 380 lines mixing layout computation, paging, reorder, and highlight concerns; `SlotWheelEditor.xaml.cs` is 341 lines of code-behind including a hand-built imperative ContextMenu and imperative drag-ghost creation. `SlotWheelEditorViewModel.CalculateScaledSlotSize()` downcasts the injected `ISlotLayoutEngine` to the concrete `SlotLayoutEngine` to reach a method missing from the interface — a type-safety smell. `WheelSlotItem.Tooltip` is a hardcoded English string (`$"#{Slot.Slot} {Label}"`) violating the localization rule. The ViewModel is constructed via `new` in code-behind instead of DI.

This change refactors both the visualization and the surrounding architecture while keeping the radial wheel direction and the existing interaction model (drag-reorder, pager, right-click move-to, hover edit/delete).

## What Changes

- **Visual redesign of the editor wheel** (see `slot-wheel-editor-visualization` spec):
  - Position identity badges on each slot (e.g., "1".."8" / "Position {0}"), so users can correlate wheel positions to slot numbers.
  - Hover/active feedback on orbs (accent ring + subtle scale) matching the runtime menu's feel.
  - Richer empty-slot affordance: dashed accent ring + plus icon + tooltip, clearly signaling "click to add a slot here".
  - A proper center-state visual (center orb + label) instead of the raw "Center" text.
  - Improved guide ring and pager presentation, with shared slot styles.
- **Architecture refactor** (see `slot-wheel-editor-architecture` spec):
  - Extend `ISlotLayoutEngine` with `CalculateOptimalSlotSize` / `CalculateOptimalCenterSize` so callers stop downcasting to `SlotLayoutEngine`.
  - Localize `WheelSlotItem.Tooltip` (and any new strings) through `ILocalizationService`.
  - Extract the imperative right-click context menu from code-behind into a reusable, testable builder using the existing `ILocalizationService`.
  - Construct `SlotWheelEditorViewModel` via DI rather than `new` in code-behind.
  - Keep `SlotLayoutEngine` behavioral contract identical (existing tests must keep passing).

## Capabilities

### New Capabilities

- `slot-wheel-editor-visualization`: Visual/interaction redesign of the settings wheel editor (position badges, hover states, empty-slot affordance, center state, guide ring + pager polish).
- `slot-wheel-editor-architecture`: Type-safe layout-engine surface, localized strings, extracted context-menu builder, and DI construction.

### Modified Capabilities

- `slot-layout-engine`: `ISlotLayoutEngine` gains two methods; the concrete `SlotLayoutEngine` implementation is unchanged behaviorally.

## Impact

- **Affected code**:
  - `Views/Controls/SlotWheelEditor.xaml` + `.xaml.cs` — visual redesign + context-menu extraction
  - `ViewModels/Settings/SlotWheelEditorViewModel.cs` — remove downcast, add localization plumbing
  - `ViewModels/Settings/WheelSlotItem.cs` — localized tooltip, position badge surface
  - `Services/Interfaces/ISlotLayoutEngine.cs` — add slot/center-size methods
  - `Views/Pages/SettingsSlotsPage.xaml.cs` — DI construction
  - `Styles/SlotStyles.xaml` — shared editor-wheel styles/tokens
  - `Resources/Strings.resx` / `Strings.zh-CN.resx` — new/localized keys
- **Dependencies**: Reuses `SlotOrb`, `ILocalizationService`, existing layout math; no changes to `Profiles.json` or plugin data model.
- **No breaking changes**: `SlotLayoutEngine` numeric behavior and existing tests are preserved. `SettingsSlotsPage` public surface unchanged.
