## Why

Change A (`cascade-submenu-foundation`) and Change B (`cascade-submenu-layout`) shipped the data model (`SubSlotDescriptor`, `PluginSlot.SubActions`), the strategy-based coordinator, and Ring/Fan geometry — but the cascade is still unreachable by users: `HandleGlobalMouseClickAsync` has no branch to open a cascade, `SlotEditorViewModel`/`SlotConfigurationDialogContent` have no UI to author `SubActions`, and there is no default injection. Without Change C the entire Direction 3 feature is runtime-ready but invisible in the product.

## What Changes

- **Cascade drill-in entry**: left-clicking a root slot whose `SubSlots` is non-empty opens a `CascadeSubMenuDescriptor` submenu (Fan by default, Ring selectable). Modifier-release still executes the slot's own action (unchanged). Window-group drill-in is untouched.
- **Sub-action editor**: `SlotEditorViewModel` + `SlotConfigurationDialogContent` gain a "Sub-Actions" editing section — add/remove/reorder sub-actions, each with plugin/action/args/label/icon/color — and a layout-style picker (Fan/Ring) on the Behavior section. Persisted via `PluginSlot.SubActions`.
- **Smart default injection**: common slot types receive sensible default sub-actions when a slot is created, matching StarPie's convention (e.g., clipboard tools, system tools) — overridable by the user in the editor.
- **Localization**: all new UI strings via `ILocalizationService` (EN + zh-CN).

## Capabilities

### New Capabilities
- `cascade-submenu-editor`: Authoring surface for sub-actions inside the unified slot editor (add/remove/reorder, per-sub-action plugin/action/args/label/icon/color, layout-style picker), persisting to `PluginSlot.SubActions`.
- `cascade-submenu-entry`: Left-click drill-in from a root slot with `SubSlots` to the cascade submenu, routed through the coordinator seam; window-group drill-in and modifier-release semantics preserved.
- `cascade-smart-defaults`: Automatic injection of sensible default sub-actions when a slot is created, overridable in the editor.

### Modified Capabilities
- `cascade-submenu-model`: `SubSlots` must populate at runtime from persisted `SubActions` (already done in `CommandPageProvider`) and remain editable through the editor; the editor-owned mutation path is added.
- `unified-slot-editor-layout`: The Behavior section gains a sub-action editor block and layout-style picker, following the existing section hierarchy (Behavior → Appearance → Advanced).

## Impact

- **Affected code**:
  - `ViewModels/Dialogs/SlotEditorViewModel.cs` — sub-action collection + commands (add/remove/move), layout-style property, existing parameter-field reuse for sub-action args
  - `Views/Dialogs/Contents/SlotConfigurationDialogContent.xaml` + `.xaml.cs` — new "Sub-Actions" section UI (bound via existing `ItemsControl`/`Tag` bridge pattern), layout-style picker
  - `ViewModels/MenuSession.cs` — cascade drill-in branch in `HandleGlobalMouseClickAsync`; route to coordinator
  - `Models/ProfilesConfig.cs` / `Models/SubSlotDescriptor.cs` — no schema change (already persisted)
  - `Services/SmartSubActionDefaults.cs` (new) — per-plugin-type default sub-action catalog
  - `App.xaml.cs` — register defaults service (if DI-worthy)
  - `Resources/Strings.resx` + `Strings.zh-CN.resx` — new `Dialog.AddSlot.SubActions.*` + `Dialog.AddSlot.LayoutStyle.*` keys
- **APIs**: no breaking changes; `SlotEditorViewModel` gains optional members. `IMenuSession` unchanged (cascade already routes via `SubMenuDescriptor`).
- **Tests**: `SlotEditorViewModelTests` (sub-action CRUD, layout style, persistence), `MenuSession` cascade-entry tests, `SmartSubActionDefaultsTests`, existing window-submenu + editor suites stay green.
- **Dependencies**: none new.

**Out of scope**: Fan/Ring geometry tuning (Change B shipped); second-level *cascade-of-cascade* nesting; sub-ring theming (visual polish, later).
