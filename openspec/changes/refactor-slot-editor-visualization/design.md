# refactor-slot-editor-visualization — Design

## Context

The settings slot page uses `SlotWheelEditor` (a `UserControl`) backed by `SlotWheelEditorViewModel`. The wheel reuses `SlotLayoutEngine` (shared with the runtime menu) for ring geometry. Current pain points:

- `SlotWheelEditorViewModel.CalculateScaledSlotSize()` (line 240-247) downcasts `ISlotLayoutEngine` → `SlotLayoutEngine` to call `CalculateOptimalSlotSize`, which is not on the interface.
- `WheelSlotItem.Tooltip` (line 44) hardcodes `$"#{Slot.Slot} {Label}"` — not localized.
- `SlotWheelEditor.xaml.cs` contains a 55-line imperative ContextMenu builder (`BuildContextMenu` + `MoveToPageAndSlot` wiring) and inline drag-ghost creation.
- `SettingsSlotsPage.xaml.cs` constructs `SlotWheelEditorViewModel` via `new` (line 36).
- Visual: empty slots are faint dashed circles + "+"; center is raw "Center" text; occupied slots carry no position badge; no hover feedback; pager/guide ring are plain.

This design keeps the wheel direction and all existing interactions. It splits the work into a visualization capability and an architecture capability, both landing in the same change.

## Goals / Non-Goals

**Goals:**
- Make the editor wheel visually cohesive with the runtime menu and the app design language.
- Give every slot a clear position identity (badge) and empty slots a clear "add here" affordance.
- Remove the type-unsafe downcast and localize all tooltips.
- Extract the context menu into a testable builder and move ViewModel construction to DI.
- Keep all existing interaction models and layout math intact.

**Non-Goals:**
- Changing the wheel layout/positioning formulas or `SlotLayoutEngine` numbers.
- Changing the drag-reorder, pager, or move-to interaction behavior.
- Touching the runtime radial menu.
- Redesigning `SlotOrb` itself (reuse as-is).

## Decisions

### D1: Extend ISlotLayoutEngine with size methods (no formula change)

**Decision**: Add `double CalculateOptimalSlotSize(int slotCount)` and `double CalculateOptimalCenterSize(int slotCount)` to `ISlotLayoutEngine`. `SlotLayoutEngine` already implements both; they become explicit interface members (or just public methods satisfying the interface). Update `SlotWheelEditorViewModel.CalculateScaledSlotSize` to call through the interface.

**Rationale**: The downcast is the only reason the concrete type leaks into the editor VM. Both methods already exist on the concrete class with fixed behavior, so exposing them on the interface is zero-risk and removes the `is` check.

**Alternatives considered**:
- Keep the downcast and add a comment → leaves the smell; the interface is the single point of truth for layout consumers.
- Move size calc into a new DTO/result object → larger API churn for no behavioral gain.

### D2: Localized tooltip via ILocalizationService

**Decision**: `WheelSlotItem` gains a reference to `ILocalizationService` (passed at construction) and exposes localized `Tooltip` (filled) and a new `EmptyTooltip` (empty-slot "add at position N" hint). The position badge text uses `Settings.Slots.PositionFormat` ("Position {0}").

**Rationale**: AGENTS.md mandates no hardcoded user-facing strings. `WheelSlotItem` is a plain VM model constructed by the editor VM, so injecting the loc service is straightforward and testable.

**New localization keys**:
- `Settings.Slots.PositionFormat` — already exists ("Position {0}"), reused for badge tooltip/aria.
- `Settings.Slots.Wheel.EmptySlotTooltipFormat` — new: "Click to add a slot at position {0}".
- Badge display itself is just the number (no key needed); the position number is data, not a string.

### D3: Extract context menu into `SlotContextMenuBuilder`

**Decision**: Create `Pulsar.Views.Controls.SlotContextMenuBuilder` (or `Pulsar.Services` if it must not reference WPF — see note) with a method `Build(PluginSlot slot, SlotWheelEditorViewModel vm, ILocalizationService loc)` returning a `ContextMenu`. It reproduces the current Move-to-page → slot, Edit, Delete structure. The code-behind calls it and applies the existing `ApplyThemeToContextMenu` theme injection.

**Note on layer**: `ContextMenu`, `MenuItem`, and click-wiring are WPF UI types, so a builder producing a real `ContextMenu` belongs in the UI layer (`Views/Controls`) and is unit-testable by inspecting the constructed `ContextMenu.Items`. To keep it testable without a running window, the builder constructs items directly (as the current code does) but isolated in its own class; tests assert item headers/Count and invoke click handlers to verify `MoveToPageAndSlot` / `EditRequested` wiring.

**Rationale**: 55 lines of menu logic in code-behind makes the control hard to reason about and the "Move to" flow untestable. Extracting it gives a seam for unit tests and matches the repo's "ViewModel/state over UI" testing principle (menu structure is state we can assert).

**Alternatives considered**:
- Declarative XAML ContextMenu with bound ItemsSource → dynamic nested page/slot submenus with per-item click targets are awkward in pure XAML and would still need code-behind for Click wiring.
- Keep inline but add tests via a testing hook → no improvement over extraction.

### D4: DI construction of SlotWheelEditorViewModel

**Decision**: Register `SlotWheelEditorViewModel` in `App.xaml.cs` `ConfigureServices` (transient or singleton; the wheel VM is per-settings-session, so transient is fine) and resolve it in `SettingsSlotsPage` via `App.Current.Services.GetRequiredService<SlotWheelEditorViewModel>()`. Its `ISlotLayoutEngine` dependency is already provided by the container.

**Rationale**: Removes `new` from code-behind, aligns with the DI-first convention, and makes tests construct the VM the same way they already do (constructor injection).

### D5: Visual layer via SlotStyles + per-slot template changes

**Decision**: Keep the existing per-slot template structure in `SlotWheelEditor.xaml` but:
- Add a position badge (small `Border` + `TextBlock`) top-left of each slot, always visible, using shared styles from `SlotStyles.xaml`.
- Add hover feedback: an accent ring `Ellipse` whose opacity is driven by a `DataTrigger` on the item's `IsMouseOver` (with a Storyboard for 150-250ms transitions); apply a subtle `ScaleTransform` on the item root.
- Rework the empty-slot placeholder: dashed accent ring + centered plus + position number, with a tooltip bound to `EmptyTooltip`.
- Replace the center "Center" text with a small center orb + label visual (non-interactive).
- Move guide-ring and pager styling into `SlotStyles.xaml` where it can be shared, and refine the pager's text hierarchy (page number primary, counts secondary).

All new brushes use existing theme tokens (`Theme.Accent`, `TextFillColor*`, `CardBackgroundFillColor*`) so both themes stay correct. No new hardcoded colors.

**Rationale**: The changes are additive to the existing template and reuse `SlotOrb` and theme tokens, keeping risk low and matching the runtime menu's feel.

## Risks / Trade-offs

- **[Risk] Position badges may crowd small slots at high slot counts** → badges are small (e.g., 14px) and sit at the top edge; slot size min is 38px so there is room. If crowding appears, badges can be shown on hover only — but the spec requires always-visible, so we keep it visible and small.
- **[Risk] Hover scale animations on many slots could cause layout jitter** → transforms are render-only (`RenderTransform`), not layout-affecting; the runtime menu already relies on this pattern.
- **[Trade-off] ContextMenu builder produces WPF types (UI-layer test)** → this is acceptable: the test asserts menu structure and command wiring, not pixel rendering; it follows the repo's existing pattern of lightweight, headless VM tests.
- **[Trade-off] More localization keys** → required by AGENTS.md; new keys are small and mirrored in both resx files.
