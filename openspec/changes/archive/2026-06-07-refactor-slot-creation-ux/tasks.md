## 1. Data Model Changes

- [x] 1.1 Add `IsPrimary` flag to `BuiltInPluginDisplayModel` in plugin display identity model
- [x] 1.2 Add `SuggestedLabelTemplate`, `SuggestedIconKey`, `SuggestedColorHex` to `SlotActionMetadata` in `Core/Plugin/Metadata/SlotActionMetadata.cs`
- [x] 1.3 Create `SlotTypeCard` model class in `Models/SlotParameterEditorModels.cs` with `Id`, `PluginId`, `DefaultAction?`, `IconKey`, `Title`, `Description`, `IsPrimary`, `Category`, `IsSelected` properties
- [x] 1.4 Create `SlotEditorMode` enum (`Create`, `Edit`)
- [x] 1.5 Add `SlotEditorViewModel` stub with `EditorMode` property and all constructor dependencies

## 2. ViewModel Consolidation

- [x] 2.1 Move `AddSlotViewModel` core logic (slot draft creation, action management, suggestion engine, validation queue, focus targeting) into `SlotEditorViewModel`
- [x] 2.2 Implement picker phase logic: `PrimaryCards` collection, `SearchText`, `IsBrowseExpanded`, `SelectSlotType(SlotTypeCard)`, `GoBackToPicker()`
- [x] 2.3 Implement `BuildPrimaryCards()` static method producing 6 curated `SlotTypeCard` instances (Switch App, Open Target, Send Keys, Fill Secret, Run Script, System)
- [x] 2.4 Implement `BuildAllCards()` method that merges primary cards with plugin-registry entries, respecting `IsPrimary` flag
- [x] 2.5 Replace hardcoded `BuildSuggestedLabel`/`BuildSuggestedIcon`/`BuildSuggestedColor` switch statements with `SlotActionMetadata` property reads, falling back to generic "Slot {N}" template
- [x] 2.6 Move `SlotConfigurationDialogViewModel` logic (set action, pick parameters, pick icon/color, notify presentation) into `SlotEditorViewModel` Edit mode path
- [x] 2.7 Remove `ScenarioOption` class, `BuildScenarios()` method, `IsScenarioMode`/`IsAdvancedMode` properties and all related commands
- [x] 2.8 Remove `PluginTypeCategoryOption` class and category filtering logic (replaced by search + browse-all)
- [x] 2.9 Remove `NotifyStateChanged`/`NotifyPreviewChanged` dual-refresh architecture; replace with single `NotifyAll()` plus targeted property notifications for lightweight changes

## 3. XAML Unification — Picker Phase

- [x] 3.1 Rewrite `AddSlotContent.xaml` picker phase: full-width `Grid` replacing the dual-column layout
- [x] 3.2 Build `SearchBox` with real-time filtering bound to `SearchText`
- [x] 3.3 Build 3x2 curated intent card grid using `ItemsControl` with `SlotTypeCard` DataTemplate (large icon + title + brief description)
- [x] 3.4 Build "Browse all slot types..." collapsible section with categorized `ItemsControl` grouped by `Category`
- [x] 3.5 Remove flow toggle buttons (Scenario/Advanced) XAML region entirely
- [x] 3.6 Add transition animation or `Visibility` toggle between picker phase and configuration phase

## 4. XAML Unification — Configuration Phase

- [x] 4.1 Unify configuration XAML into single-column layout: Behavior section → Appearance section → Advanced expander
- [x] 4.2 Move Label field from standalone position into Appearance section alongside Color and Icon
- [x] 4.3 Replace standalone health badge + type badge in header with single compound status indicator (back arrow in Create mode + orb + label + status badge)
- [x] 4.4 Relocate summary tokens to between action selector and required parameters within Behavior section
- [x] 4.5 Implement segmented button group DataTemplate for 2-4 action scenarios, ComboBox fallback for 5+ actions
- [x] 4.6 Remove `SelectedPluginDescription` and `SelectedPluginContextTitle` display from configuration header
- [x] 4.7 Add `CardExpander` collapse logic: Appearance collapsed by default in Create mode, expanded in Edit mode; Advanced always collapsed

## 5. Unify SlotConfigurationDialogContent

- [x] 5.1 Rewrite `SlotConfigurationDialogContent.xaml` to use the same single-column layout structure as the create configuration phase
- [x] 5.2 Replace `SlotConfigurationDialogViewModel` binding with `SlotEditorViewModel` binding
- [x] 5.3 Ensure Label, Color, Icon appear together under Appearance section in edit dialog
- [x] 5.4 Ensure Behavior section matches create layout exactly (action selector + required params)
- [x] 5.5 Register unified `SlotEditorViewModel` → `SlotConfigurationDialogContent` DataTemplate in `DialogHostWindow.xaml`
- [x] 5.5 Register unified `SlotEditorViewModel` → `AddSlotContent` DataTemplate in `DialogHostWindow.xaml`

## 6. SettingsViewModel Integration

- [x] 6.1 Update `AddSlotDialog()` to construct `SlotEditorViewModel` with `EditorMode.Create` instead of `AddSlotViewModel`
- [x] 6.2 Update `OpenSlotConfiguration()` to construct `SlotEditorViewModel` with `EditorMode.Edit` and existing `PluginSlot`
- [x] 6.3 Update `BuildAddSlotOptions()` to produce `SlotTypeCard` list from plugin registry with `IsPrimary` flag
- [x] 6.4 Update `CreateSlotDraft()` to accept `SlotTypeCard` and apply `DefaultAction` if set
- [x] 6.5 Remove `BuildSlotTemplate()` plugin-ID-specific defaulting (delegated to `SlotActionMetadata`)
- [x] 6.6 Remove `AddSlotOfType()` quick-add shortcut or adapt to `SlotTypeCard`
- [x] 6.7 Wire up back-arrow navigation from `SlotEditorViewModel` to allow picker phase return

## 7. Plugin Metadata Updates

- [x] 7.1 Add `SuggestedLabelTemplate` to winswitcher plugin metadata (`"Switch to {app}"`)
- [x] 7.2 Add `SuggestedIconKey`/`SuggestedColorHex` to winswitcher, command, pki, bookmarklet, vbarunner plugin metadata
- [x] 7.3 Mark winswitcher, command, pki, bookmarklet, vbarunner, system plugins with `IsPrimary = true` in display identity metadata

## 8. Code-Behind Updates

- [x] 8.1 Update `AddSlotContent.xaml.cs` validation focus logic for new single-column layout (remove dual-column coordinate lookup)
- [x] 8.2 Update `SlotConfigurationDialogContent.xaml.cs` to delegate to `SlotEditorViewModel` instead of `SlotConfigurationDialogViewModel`
- [x] 8.3 Remove `ActionComboBox_SelectionChanged` handler; replace with segmented button group `Click` routing

## 9. Localization

- [x] 9.1 Add new localization keys for 6 curated card titles and descriptions (`Dialog.AddSlot.CardSwitchApp`, etc.)
- [x] 9.2 Add localization key for "Browse all slot types..." label
- [x] 9.3 Add localization key for search placeholder (`Dialog.AddSlot.SearchPlaceholder`)
- [x] 9.4 Add localization keys for unified section headers (`Dialog.AddSlot.Behavior`, `Dialog.AddSlot.Appearance`, `Dialog.AddSlot.Advanced`)
- [ ] 9.5 Remove obsolete localization keys for Scenario/Advanced flow labels
- [x] 9.6 Add/update zh-CN resource strings for all new keys

## 10. Testing & Validation

- [x] 10.1 Update `DialogSlotEditorViewModelTests.cs` to test `SlotEditorViewModel` in both Create and Edit modes
- [x] 10.2 Add tests for `SlotTypeCard.BuildPrimaryCards()` producing correct 6 cards with correct PluginId/DefaultAction mappings
- [x] 10.3 Add tests for `BuildAllCards()` merging primary and registry entries
- [x] 10.4 Add test: Appearance section collapse state is collapsed for Create, expanded for Edit
- [x] 10.5 Add test: segmented button group shows for 2-4 actions, ComboBox for 5+
- [x] 10.6 Verify `SlotAddedTriggerHandler` still fires correctly with unified ViewModel
- [x] 10.7 Manual visual QA: compare Create and Edit dialog screenshots for layout consistency
- [x] 10.8 Manual visual QA: verify all 6 curated cards display correctly in both light and dark themes

## 11. Cleanup

- [x] 11.1 Delete `AddSlotViewModel.cs` after confirming all logic migrated
- [x] 11.2 Delete `SlotConfigurationDialogViewModel.cs` after confirming all logic migrated
- [x] 11.3 Remove `AddSlotViewModel` DataTemplate registration from `DialogHostWindow.xaml`
- [ ] 11.4 Remove obsolete Scenario/Advanced localization keys from both `.resx` files
- [x] 11.5 Delete `PluginTypeCategoryOption` nested class references
- [x] 11.6 Run `dotnet build` and fix any compilation errors
