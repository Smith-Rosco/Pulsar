## ADDED Requirements

### Requirement: PluginUsageTracker Unit Tests
The `PluginUsageTracker` service SHALL have dedicated xUnit tests covering all public methods and edge cases.

#### Scenario: Recording execution updates all fields
- **WHEN** `RecordExecution` is called with valid parameters
- **THEN** TotalExecutions, SuccessCount/FailureCount, AverageExecutionTimeMs, DailyStats, SlotUsage, task/action mode counts, and HourlyUsage SHALL all be updated correctly

#### Scenario: Concurrent recording is thread-safe
- **WHEN** multiple threads simultaneously record executions for the same plugin
- **THEN** the final TotalExecutions SHALL equal the total number of calls across all threads

#### Scenario: LoadAsync restores saved state
- **WHEN** stats are saved to disk and a new tracker instance loads them
- **THEN** all properties (plugin IDs, counts, daily stats, slot usage) SHALL match the saved values

#### Scenario: Clone produces independent copy
- **WHEN** `GetStats` returns a cloned `PluginUsageStats`
- **THEN** mutating the returned object SHALL NOT affect the tracker's internal state

#### Scenario: Cleanup removes entries older than 30 days
- **WHEN** daily stats contain entries older than 30 days
- **THEN** those entries SHALL be removed after the next `RecordExecution` call

### Requirement: PluginHealthMonitor Unit Tests
The `PluginHealthMonitor` service SHALL have dedicated xUnit tests for all health state transitions.

#### Scenario: 100% success yields health score 100
- **WHEN** a plugin has 100 successful executions and 0 failures
- **THEN** `CalculateHealthScore` SHALL return 100

#### Scenario: 50% error rate penalizes score
- **WHEN** a plugin has 50 successes and 50 failures in the recent buffer
- **THEN** `CalculateHealthScore` SHALL return less than 100 (specifically reduced by the error-rate penalty)

#### Scenario: Circuit breaker trips reduce health score
- **WHEN** `RecordCircuitBreakerTrip` is called 3 times for a plugin
- **THEN** `CalculateHealthScore` SHALL be reduced by 30 points (10 per trip)

#### Scenario: Unused plugin gets Unused status
- **WHEN** a plugin's `LastUsed` is more than 30 days ago
- **THEN** `GetHealthReport` SHALL return `PluginHealthStatus.Unused`

### Requirement: PluginRecommendationEngine Unit Tests
The `PluginRecommendationEngine` SHALL have dedicated xUnit tests for all recommendation types.

#### Scenario: Unused plugin triggers DisableUnusedPlugin recommendation
- **WHEN** a plugin has never been executed
- **THEN** `GetRecommendations` SHALL include a recommendation with type `DisableUnusedPlugin`

#### Scenario: High error rate triggers CheckPluginErrors recommendation
- **WHEN** a plugin has a health report with ErrorRate > 20%
- **THEN** `GetRecommendations` SHALL include a recommendation with type `CheckPluginErrors`

#### Scenario: Core plugins are excluded from recommendations
- **WHEN** a plugin's `CanDisable` is false
- **THEN** `GetRecommendations` SHALL NOT include recommendations for that plugin

### Requirement: SettingsAnalyticsPageViewModel Unit Tests
The `SettingsAnalyticsPageViewModel` SHALL have dedicated xUnit tests for data loading and error handling.

#### Scenario: LoadAsync populates collections
- **WHEN** `LoadAsync` completes with a tracker returning 3 plugins with usage data
- **THEN** `MostUsedPlugins` SHALL contain 3 items and `HasData` SHALL be true

#### Scenario: LoadAsync handles empty data gracefully
- **WHEN** `LoadAsync` completes with a tracker returning zero plugins
- **THEN** `HasData` SHALL be false and no exception SHALL be thrown

#### Scenario: LoadAsync sets error state on failure
- **WHEN** `LoadAsync` encounters an exception from the tracker
- **THEN** `HasError` SHALL be true and `ErrorMessage` SHALL be non-empty
