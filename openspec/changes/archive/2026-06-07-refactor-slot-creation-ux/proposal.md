## Why

The Slot creation dialog forces users through an unnecessary meta-choice between "Scenario" and "Advanced" flows, presents 15-20+ simultaneous information elements across a dual-column layout, and has divergent layouts between create and edit dialogs (Label belongs to different groups, Appearance sections differ). This creates cognitive overload, confuses first-time users, and violates the principle of progressive disclosure.

## What Changes

- **BREAKING**: Remove the Scenario/Advanced flow toggle and the `ScenarioOption` model. Replace with a unified **intent grid picker** — 6 large curated cards (Switch App, Open Target, Send Keys, Fill Secret, Run Script, System) plus a search bar and collapsible "Browse All" category browser.
- Collapse the left-column type picker and right-column configuration into a **two-phase single-view transition**: Phase 1 shows the picker full-width, Phase 2 shows configuration full-width with a back arrow.
- **Unify create and edit dialog layouts**: Both use the same single-column structure — Behavior section (action + required params) → Appearance section (Label + Color + Icon) → Advanced expander.
- **Reduce preview panel information density** from 7 simultaneous elements (orb, title, context title, description, health badge, type badge, summary tokens) to 3 (orb, label, compound status indicator).
- Introduce a unified `SlotTypeCard` model that represents both curated primary cards and secondary plugin entries, replacing the separate `ScenarioOption` + `PluginTypeOption` dual hierarchy.
- Delegate label/icon/color suggestion logic from the ViewModel's hardcoded switch statements to plugin metadata (`SlotActionMetadata`), enabling extension plugins to supply smart defaults.

## Capabilities

### New Capabilities
- `unified-slot-type-picker`: A single intent-grid picker replacing the dual-flow (Scenario/Advanced) type selection, supporting curated primary cards, search, and collapsible full browsing.
- `unified-slot-editor-layout`: A single-column editor layout shared identically between create and edit dialogs, with consistent Appearance grouping (Label + Color + Icon).
- `slot-type-card-model`: New data model (`SlotTypeCard`) unifying curated intent cards and plugin entries into one collection, with `IsPrimary` flag and optional `DefaultAction`.

### Modified Capabilities
- `scenario-based-slot-authoring`: **REMOVED**. The scenario/advanced dual-flow is fully replaced by the unified picker. The `ScenarioOption` class and `BuildScenarios()` method are deleted.
- `slot-editor-density-and-layout`: Layout changes from dual-column (332px picker + config) to two-phase single-view. Preview panel reduces from 7 to 3 visual elements. Header status badge moves to the title bar.
- `slot-editor-information-hierarchy`: Hierarchy shifts from flat (all sections visible simultaneously) to layered (Behavior always visible, Appearance collapsed by default in create but expanded in edit, Advanced always collapsed).
- `slot-editor-progressive-disclosure`: Disclosure model changes from "Scenario vs Advanced toggle + simultaneous panels" to "Intent grid → configure → polish" three-tier progressive flow.

## Impact

- **Views**: `AddSlotContent.xaml` (769 lines → ~450 lines rewrite), `SlotConfigurationDialogContent.xaml` (308 lines → merge target into unified editor)
- **ViewModels**: `AddSlotViewModel.cs` (1025 lines → ~500 lines simplified), `SlotConfigurationDialogViewModel.cs` (178 lines → merge target into unified editor), `SettingsViewModel.cs` (slot-creation methods)
- **Models**: `SlotParameterEditorModels.cs` (new `SlotTypeCard` class added), `SlotActionMetadata.cs` (new `SuggestedLabelTemplate`, `SuggestedIconKey`, `SuggestedColorHex` properties), `ProfilesConfig.cs` (no changes)
- **Deletions**: `ScenarioOption` class, `IsScenarioMode`/`IsAdvancedMode` properties and related commands, flow toggle XAML region (~150 lines)
- **Plugin metadata contract**: `IPluginMetadataRegistry` extensions — new `IsPrimary` flag on plugin display models for curated grid placement
