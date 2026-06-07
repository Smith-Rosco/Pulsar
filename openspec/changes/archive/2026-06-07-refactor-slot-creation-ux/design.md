## Context

The Slot creation dialog (`AddSlotContent.xaml` + `AddSlotViewModel.cs`) uses a dual-flow architecture: users choose between "Scenario" (4 curated intent cards) and "Advanced" (category-filtered plugin browsing) in a two-column layout (332px left picker + config on right). The edit dialog (`SlotConfigurationDialogContent.xaml` + `SlotConfigurationDialogViewModel.cs`) uses a single-column layout with different grouping (Label belongs to Appearance in edit, but is standalone in create). Both dialogs exist as separate ViewModels despite sharing 80%+ of their structure.

The ViewModel (~1025 lines) manages two parallel flows, hardcoded suggestion logic, and complex state refresh machinery (`NotifyStateChanged` vs `NotifyPreviewChanged`). The preview panel shows 7 simultaneous information elements. The "Scenario vs Advanced" toggle is a meta-decision that exposes implementation concepts (plugin, action, category) to users prematurely.

## Goals / Non-Goals

**Goals:**
- Eliminate the Scenario/Advanced flow toggle; present a single unified type picker with 6 curated intent cards as default, plus search and collapsible full browsing
- Replace dual-column create layout with two-phase single-view: Phase 1 (picker full-width) → Phase 2 (configuration full-width with back arrow)
- Unify create and edit dialog layouts into one structure: Behavior → Appearance → Advanced
- Merge `AddSlotViewModel` and `SlotConfigurationDialogViewModel` into a single `SlotEditorViewModel` with `Mode` (Create/Edit) disambiguation
- Reduce preview panel from 7 to 3 visual elements (orb, label, compound status)
- Move label/icon/color suggestion logic from ViewModel switch statements to `SlotActionMetadata` plugin metadata properties
- Preserve all existing slot functionality (parameter validation, pickers, action selection, drag-drop reorder, persistence)

**Non-Goals:**
- Changing the SettingsSlotsPage slot card display
- Modifying the radial menu execution pipeline or `SlotViewModel`
- Changing parameter field rendering (`SlotParameterEditorField` template) beyond appearance grouping
- Altering the `Profiles.json` serialization format
- Redesigning the `SlotOrb` control or `SlotPresentation` model
- Adding new slot types or plugin categories

## Decisions

### D1: Two-phase single-view instead of dual-column layout

**Choice**: Phase 1 shows the type picker full-width. After selection, the view transitions to Phase 2 (configuration full-width). A back arrow allows returning to Phase 1.

**Alternatives considered**:
- *Keep dual-column with left-picker always visible*: Rejected because the left column becomes dead space after selection (332px wasted), and the simultaneous display of picker + config creates the information density problem users complain about.
- *Single-column with picker at top, config below (scroll)*: Rejected because the picker remains visible and consumes vertical space during configuration. Users would scroll past it repeatedly.

**Rationale**: A two-phase view aligns with progressive disclosure principles. Phase 1 has exactly one job (pick what to do). Phase 2 has exactly one job (configure the chosen slot). The back arrow provides a clear undo path without clutter.

### D2: SlotTypeCard model unifies ScenarioOption and PluginTypeOption

**Choice**: Create `SlotTypeCard` with `Id`, `PluginId`, `DefaultAction?`, `IconKey`, `Title`, `Description`, `IsPrimary`, `Category`. Build 6 primary cards from a curated list (matching current scenarios), and secondary cards from the plugin registry.

**Alternatives considered**:
- *Keep separate models with an adapter*: Rejected because it perpetuates the dual-flow mental model in code.
- *Use PluginTypeOption with a decorator*: Rejected. PluginTypeOption maps 1:1 to plugins, but curated cards map to plugin+action combos (e.g., both "Open Target" and "Send Keys" map to `com.pulsar.command` with different actions).

**Rationale**: A single `IsPrimary` flag cleanly separates the curated first-view grid from the full browse list. The optional `DefaultAction` field allows curated cards to pre-select a specific action without requiring the user to understand plugin internals.

### D3: Merge AddSlotViewModel + SlotConfigurationDialogViewModel into SlotEditorViewModel

**Choice**: Single `SlotEditorViewModel` with constructor parameter `SlotEditorMode { Create, Edit }`. In Create mode, starts in picker phase and transitions to config after selection. In Edit mode, starts directly in config phase with existing slot data.

**Alternatives considered**:
- *Keep separate ViewModels with shared base class*: Rejected. The edit ViewModel (178 lines) is essentially a subset of the create ViewModel (1025 lines). A shared base class would still leave 80% of logic in the create VM.
- *Keep completely separate*: Rejected per the uniformity goal.

**Rationale**: Both dialogs share: parameter field exposure (`RequiredParameters`, `OptionalParameters`, `AdvancedParameters`), action management (`AvailableActions`, `SetAction`), appearance pickers (`PickIconAsync`, `PickColorAsync`), validation display, and the footer button contract. The only meaningful difference is whether a type picker phase precedes configuration.

### D4: Delegate suggestions to SlotActionMetadata

**Choice**: Add `SuggestedLabelTemplate`, `SuggestedIconKey`, `SuggestedColorHex` properties to `SlotActionMetadata`. The `SlotEditorViewModel` uses these for initial pre-fill, falling back to a generic "Slot {N}" template if absent.

**Alternatives considered**:
- *Plugin-level interface method*: Adding `GetSuggestedLabel(SlotParameterEditorField[])` to plugin interfaces. Rejected because it requires plugins to reference WPF model types.
- *Keep hardcoded switch*: Rejected — tightly couples the dialog to specific plugin IDs and blocks extension plugins from providing smart defaults.

**Rationale**: Metadata properties are declarative, testable, and already the pattern used for parameter definitions. Extension plugins get this for free by adding properties to their existing `SlotActionMetadata` declarations.

### D5: Preview simplification

**Choice**: The configuration header shows: `← [orb] Switch to Chrome  [⚠ Needs Setup]`. Summary tokens move into the Behavior section (between action selector and required params). Health badge merges into orb color ring. Plugin description and type badge are removed from config view.

**Rationale**: The orb already communicates type via icon and color. The health state is already communicated by the header status badge and orb ring color. Redundant information increases scanning time without adding decision value.

## Risks / Trade-offs

- **[Risk] Regression in tutorial triggers**: The `SlotAddedTriggerHandler` watches for slot configuration events. Unified ViewModel must still emit the same events → Mitigation: Preserve the `_createSlotDraft` delegate and `RequestClose` callback contract. Add unit tests verifying trigger handler compatibility.
- **[Risk] Extension plugins relying on current PluginTypeOption shape**: Some extension plugins may assume the `PluginTypeOption` model shape → Mitigation: `PluginTypeOption` is internal to the ViewModel and not part of the public plugin contract (`IPluginMetadataRegistry` returns `BuiltInPluginDisplayModel`). No breaking change to public API.
- **[Risk] Large XAML rewrite could introduce visual regressions**: Moving from 769-line to ~450-line XAML for create, and merging edit → Risk: Phased implementation with visual comparison snapshots at each phase boundary.
- **[Trade-off] Two-phase view adds a transition**: Users who frequently create slots of the same type will experience one extra click → Acceptable because: (a) slot creation is infrequent (set up once, use many times), (b) the back arrow enables quick correction if wrong type selected, (c) this is the same number of clicks as the current scenario flow.
