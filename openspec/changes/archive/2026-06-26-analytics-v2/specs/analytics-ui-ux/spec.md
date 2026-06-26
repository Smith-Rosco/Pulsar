## ADDED Requirements

### Requirement: Time Range Filter
The Analytics page SHALL provide a combo box to filter displayed data by time range: All Time, Today, This Week, or This Month.

#### Scenario: User selects "This Week" filter
- **WHEN** the user selects "This Week" from the time range dropdown
- **THEN** `MostUsedPlugins` SHALL be recomputed using only executions from the last 7 days
- **AND** summary cards (total executions, active plugins, today, week) SHALL reflect the filtered data

#### Scenario: User selects "All Time" filter
- **WHEN** the user selects "All Time"
- **THEN** all data since tracking began SHALL be displayed without date filtering

#### Scenario: Filter persists across sort changes
- **WHEN** a time filter is active and the user clicks a sort column
- **THEN** sorting SHALL apply to the already-filtered dataset

### Requirement: Sortable Plugin List
The plugin list SHALL support click-to-sort on the columns: Executions, Success Rate, Avg Duration, and Last Used.

#### Scenario: User clicks "Success Rate" column header
- **WHEN** the user clicks the Success Rate column header
- **THEN** the plugin list SHALL reorder descending by success rate
- **AND** the clicked column header SHALL show a sort direction indicator

#### Scenario: User clicks same column twice
- **WHEN** the user clicks an already-sorted column
- **THEN** the sort direction SHALL toggle between ascending and descending

#### Scenario: Sort is retained across filter changes
- **WHEN** the user sorts by Avg Duration then changes the time filter
- **THEN** the filtered results SHALL remain sorted by Avg Duration

### Requirement: Actionable Recommendation Cards
Recommendation cards on the Analytics page SHALL include action buttons that perform the recommended action.

#### Scenario: User clicks "Disable Plugin" on an unused plugin recommendation
- **WHEN** a "DisableUnusedPlugin" recommendation is displayed and the user clicks "Disable Plugin"
- **THEN** the plugin SHALL be disabled via `IPluginRegistry.SetPluginStateAsync`
- **AND** the recommendation SHALL be removed from the list after the action succeeds

#### Scenario: User clicks "View Logs" on an error recommendation
- **WHEN** a "CheckPluginErrors" recommendation is displayed and the user clicks "View Logs"
- **THEN** the plugin log viewer dialog SHALL open for that plugin

#### Scenario: Action buttons are hidden when service unavailable
- **WHEN** the required service (IPluginRegistry or ILogService) is not injected
- **THEN** the corresponding action button SHALL be collapsed
