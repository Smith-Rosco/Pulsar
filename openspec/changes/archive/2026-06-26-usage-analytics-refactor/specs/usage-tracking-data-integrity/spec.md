## ADDED Requirements

### Requirement: Single Path Slot/Mode Recording
The `IPluginUsageTracker.RecordExecution` method SHALL always accept slot index and mode parameters, and SHALL record slot usage, mode counts, and hourly usage as part of every execution recording. The separate `RecordSlotUsage` method SHALL be removed from the interface.

#### Scenario: Complete telemetry recorded in one call
- **WHEN** the execution pipeline calls `RecordExecution(pluginId, success, executionTimeMs, profileName, slotIndex, mode)`
- **THEN** the system SHALL update all telemetry fields (TotalExecutions, success/failure counts, DailyStats, AverageExecutionTimeMs, SlotUsage, TaskModeExecutions/ActionModeExecutions, HourlyUsage) in a single atomic operation

#### Scenario: Backward-compatible convenience overload
- **WHEN** a caller invokes `RecordExecution(pluginId, success, executionTimeMs)` without slot/mode params
- **THEN** the system SHALL record execution telemetry with slotIndex=0 and mode="" (no slot/mode data added, but totals and daily stats still increment)

### Requirement: No Double-Counted Hourly Data
Hourly usage data SHALL be incremented exactly once per plugin execution, regardless of which code path triggers the recording.

#### Scenario: Single increment per execution
- **WHEN** a plugin executes successfully via the pipeline
- **THEN** the HourlyUsage for the current hour SHALL be incremented by exactly 1

### Requirement: Thread-Safe Dirty Flag
The `PluginUsageTracker._isDirty` flag SHALL use atomic operations for all reads and writes to ensure visibility across threads.

#### Scenario: Concurrent execution records
- **WHEN** two threads simultaneously record executions for different plugins
- **THEN** the dirty flag SHALL be observably set to true on both threads before either returns

### Requirement: Localized Recommendation Messages
The `PluginRecommendationEngine` SHALL generate recommendation messages using localization resource keys, not hardcoded English strings.

#### Scenario: Recommendation in current locale
- **WHEN** the recommendation engine generates a "DisableUnusedPlugin" recommendation
- **THEN** the title and message text SHALL use `ILocalizationService` to resolve localized strings in the current UI language

#### Scenario: Fallback for missing keys
- **WHEN** a localization key is not found for the current locale
- **THEN** the engine SHALL fall back to English as defined in `Strings.resx`
