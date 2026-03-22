## ADDED Requirements

### Requirement: ProcessWindowMatcher SHALL extract data loading logic

The class `ViewModels/Strategies/ProcessWindowMatcher.cs` SHALL contain all logic for transforming raw window data into slot groups, including grouping by process name, matching against configuration, and building the slot list.

#### Scenario: BuildSlotList produces correct structure
- **WHEN** BuildSlotList is called with a list of ProcessWindowInfo
- **THEN** it returns a list of MatchedWindowGroup objects with Config and Windows populated

### Requirement: ProcessWindowMatcher SHALL match windows to configured slots

The matcher SHALL match window process names against configured slot app names (from profile configuration), supporting both "app" argument and "Label" fallback.

#### Scenario: Configured slot matching
- **WHEN** configuration has slot 1 with app "code"
- **AND** a window from process "Code.exe" exists
- **THEN** the matched group for slot 1 contains that window

### Requirement: ProcessWindowMatcher SHALL support unconfigured windows

Windows that don't match any configured slot SHALL be placed in the slot list in order after all configured slots.

#### Scenario: Unconfigured windows placement
- **WHEN** 3 windows exist but only slot 1 is configured
- **THEN** slots 2 and 3 contain the unconfigured windows in process name order

### Requirement: ProcessWindowMatcher SHALL be independently testable

The class SHALL have no dependencies on UI frameworks (ObservableCollection, etc.) and can be instantiated with just ProfilesConfig.

#### Scenario: Matcher instantiation
- **WHEN** new ProcessWindowMatcher(config) is created
- **THEN** it can call BuildSlotList without any UI dependencies