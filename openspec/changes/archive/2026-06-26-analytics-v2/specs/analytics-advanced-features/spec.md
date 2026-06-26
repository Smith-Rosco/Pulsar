## ADDED Requirements

### Requirement: Usage Trend Sparkline Display
Each plugin card in the most-used list SHALL display a mini sparkline showing daily execution counts for the past 7 days.

#### Scenario: Plugin has daily usage data
- **WHEN** a plugin has DailyStats entries for the past 7 days
- **THEN** a horizontal bar sparkline SHALL be rendered with 7 thin bars, each proportional to that day's execution count

#### Scenario: Plugin has no recent daily data
- **WHEN** a plugin has no DailyStats entries for the past 7 days
- **THEN** the sparkline area SHALL be empty or show "No recent data"

### Requirement: Plugin Inactivity Alerts
The recommendation engine SHALL generate alerts when a plugin that was previously used frequently goes unused for a configurable threshold period.

#### Scenario: Frequently-used plugin becomes inactive
- **WHEN** a plugin with >50 total executions has no executions for 7 consecutive days
- **THEN** the recommendation engine SHALL generate an "InactivePlugin" recommendation

#### Scenario: Never-frequently-used plugin is not alerted
- **WHEN** a plugin has <10 total executions and goes unused
- **THEN** no inactivity alert SHALL be generated for that plugin

### Requirement: Slot Optimization Recommendations
The recommendation engine SHALL analyze slot usage patterns and suggest moving frequently-used plugins to faster-access slot positions.

#### Scenario: Plugin used mostly from corner slot
- **WHEN** a plugin's `FavoriteSlot` is 3 or higher (sub-optimal position) and has >100 total executions
- **THEN** the engine SHALL generate a recommendation suggesting moving it to Slot 1 with a message like "Consider moving to a prime-access slot"

#### Scenario: Plugin already in optimal slot
- **WHEN** a plugin's `FavoriteSlot` is 1 or 2 (top-access positions)
- **THEN** no slot optimization recommendation SHALL be generated

### Requirement: CSV Data Export
The Analytics page SHALL provide an export button that downloads the displayed analytics data as a CSV file.

#### Scenario: User exports analytics data
- **WHEN** the user clicks "Export CSV"
- **THEN** a Save File dialog SHALL open, defaulting to "pulsar-analytics.csv"
- **AND** the file SHALL contain columns: Rank, PluginId, DisplayName, TotalExecutions, SuccessRate, AvgDurationMs, FavoriteSlot, PrimaryMode, LastUsed

#### Scenario: No data to export
- **WHEN** the user clicks "Export CSV" and there is no analytics data
- **THEN** the export button SHALL be disabled or show a notification that there is nothing to export

#### Scenario: CSV properly escapes special characters
- **WHEN** a plugin DisplayName contains a comma or quote character
- **THEN** the CSV export SHALL enclose the field in double quotes and escape internal quotes per RFC 4180
