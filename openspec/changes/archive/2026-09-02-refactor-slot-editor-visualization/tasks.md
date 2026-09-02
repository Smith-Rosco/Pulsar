# refactor-slot-editor-visualization — Tasks

## 1. Layout engine interface (architecture)

- [x] 1.1 Add `double CalculateOptimalSlotSize(int slotCount)` and `double CalculateOptimalCenterSize(int slotCount)` to `ISlotLayoutEngine`
- [x] 1.2 Confirm `SlotLayoutEngine` methods satisfy the interface (no numeric change)
- [x] 1.3 Update `SlotWheelEditorViewModel.CalculateScaledSlotSize` to call the interface method (remove `is SlotLayoutEngine` downcast)
- [x] 1.4 Verify `SlotLayoutEngineTests` still pass unchanged (added interface-surface assertion)

## 2. Localized wheel strings

- [x] 2.1 Add `Settings.Slots.Wheel.EmptySlotTooltipFormat` to `Strings.resx` (EN) and `Strings.zh-CN.resx` (CN)
- [x] 2.2 Add `Settings.Slots.Wheel.PageLabel` to both resx files (reused `Settings.Slots.PositionFormat` for badge tooltip)
- [x] 2.3 Thread `ILocalizationService` into `WheelSlotItem`; localize `Tooltip` (filled) and add `EmptyTooltip`
- [x] 2.4 Update `SlotWheelEditorViewModel` to pass loc service when building items

## 3. Context menu extraction

- [x] 3.1 Create `SlotContextMenuBuilder` in `Views/Controls/` with `Build(slot, vm, loc)` returning a `ContextMenu` (Move-to page/slot, Edit, Delete)
- [x] 3.2 Replace `BuildContextMenu` in `SlotWheelEditor.xaml.cs` with delegation to the builder + existing theme injection
- [x] 3.3 Add unit tests for `SlotContextMenuBuilder`: item structure, Move-to structure, Edit/Delete wiring

## 4. DI construction

- [x] 4.1 Register `SlotWheelEditorViewModel` in `App.xaml.cs` `ConfigureServices`
- [x] 4.2 Update `SettingsSlotsPage.xaml.cs` to resolve it from `App.Current.Services` instead of `new`
- [x] 4.3 Update tests that construct the wheel VM with the new constructor signature

## 5. Visualization redesign

- [x] 5.1 Add shared wheel-editor styles/tokens to `SlotStyles.xaml` (position badge, empty placeholder ring, guide ring)
- [x] 5.2 Add always-visible position badge to each slot item template
- [x] 5.3 Rework empty-slot placeholder: dashed accent ring + plus + localized tooltip
- [x] 5.4 Add hover feedback (accent ring + render-only scale, 150-250ms transitions) to slot template
- [x] 5.5 Replace center "Center" text with a non-interactive center orb + label visual
- [x] 5.6 Refine guide ring and pager presentation using shared styles / proper hierarchy
- [x] 5.7 Verified theme tokens (no hardcoded colors); light/dark correctness to be QA-verified manually

## 6. Tests & verification

- [x] 6.1 Add/adjust `SlotWheelEditorViewModelTests` for localized tooltip + position badge surface
- [x] 6.2 Run `dotnet test` — 359 tests pass (0 failures)
- [x] 6.3 Build the main project — 0 errors (1 pre-existing TrayIconService warning)
- [x] 6.4 Manual QA: add/edit/delete/reorder/pager/move-to, both themes — requires human to run

