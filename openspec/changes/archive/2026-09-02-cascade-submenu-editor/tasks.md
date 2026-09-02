## 1. Model plumbing

- [x] 1.1 Add `CascadeLayoutStyle` (`SubMenuLayoutStyle?`, camelCase `layoutStyle`) to `PluginSlot` in `Models/ProfilesConfig.cs`; verify it compiles and legacy config without the key deserializes to null
- [x] 1.2 Add `SubSlotEditorRow` wrapper (PluginId/Action/Args/Label/IconKey/ColorHex + observable props, metadata-derived action label) in `ViewModels/Dialogs/`; verify it compiles
- [x] 1.3 Add round-trip tests: slot with `layoutStyle` persists/restores; legacy slot defaults to null; `SubActions` round-trip unchanged

## 2. Sub-action editor UI

- [x] 2.1 Extend `SlotEditorViewModel` with `ObservableCollection<SubSlotEditorRow> SubActions` + commands (AddSubAction, RemoveSubAction, MoveSubActionUp/Down) and `CascadeLayoutStyle` picker property; verify `SlotEditorViewModelTests` compile
- [x] 2.2 Materialize `List<SubSlotDescriptor>` from rows on `Save()` and copy `Args` working dictionaries in/out (D4); verify save/cancel persistence tests pass
- [x] 2.3 Add "Sub-Actions" section to `SlotConfigurationDialogContent.xaml` inside Behavior (below required parameters): rows via existing `SlotParameterFieldTemplate` + `ItemsControl.Tag` bridge (D2), add/remove/reorder affordances, empty-state "add" affordance, Fan/Ring picker; add localized keys to `Strings.resx` (EN) + `Strings.zh-CN.resx` (ZH)
- [x] 2.4 Wire per-row plugin/action selector so switching action re-resolves `SlotParameterEditorField`s via `_metadataRegistry`; unknown plugin/action shows inline validation instead of crashing
- [x] 2.5 Verify `dotnet build Pulsar/Pulsar/Pulsar.csproj` succeeds (0 errors) and `SlotEditorViewModel`/layout tests green

## 3. Cascade entry point

- [x] 3.1 In `MenuSession.HandleGlobalMouseClickAsync`, add cascade branch (D5): window-group first (unchanged), else `slot.SubSlots.Count > 0` → build `CascadeSubMenuDescriptor(slot.SubSlots, slot.CascadeLayoutStyle ?? Fan, slot.Label)` and `EnterSubMenuAsync`; empty `SubSlots` → existing action execution
- [x] 3.2 Add `MenuSession` entry tests: cascade slot left-click opens cascade with chosen layout style; empty-slot left-click executes action; window-group drill-in unchanged; modifier-release on cascade slot executes slot action
- [x] 3.3 Verify `dotnet build` 0 errors and `GroupedSlotInteractionTests`/`MenuSessionGestureTests` stay green

## 4. Smart default injection

- [x] 4.1 Create `Services/SmartSubActionDefaults.cs` with `IReadOnlyList<SubSlotDescriptor>? ForPlugin(string pluginId, string action)` catalog for known types (e.g., clipboard/send-keys and system-tools catalogs)
- [x] 4.2 Inject defaults in `SlotEditorWorkspace.CreateSlotDraft` (D6): assign `slot.SubActions` only for new drafts; register in `App.xaml.cs` if DI-worthy
- [x] 4.3 Add `SmartSubActionDefaultsTests`: known type → injected list, unknown type → empty, edit-mode slot untouched, re-creation re-injects afresh

## 5. Tests & verification

- [x] 5.1 Run `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` — full suite green (baseline 827+, no regressions in window/group/gesture/editor suites)
- [x] 5.2 Manual QA (requires human): create a clipboard/system-tools slot → defaults pre-filled; edit/add/remove/reorder sub-actions + pick layout style → save/reload restores; left-click opens Fan/Ring cascade and selects children; modifier-release and window-group paths unaffected; both themes
