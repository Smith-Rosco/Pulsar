## ADDED Requirements

### Requirement: Hourly Usage Heatmap Display
The Settings Analytics page SHALL display a heatmap of plugin usage by hour of day, aggregating data already collected in `PluginUsageStats.HourlyUsage`.

#### Scenario: User views hourly usage distribution
- **WHEN** the user navigates to the Analytics page and usage data exists
- **THEN** a horizontal bar chart SHALL display execution counts for each hour (0-23), with bar width proportional to the count

#### Scenario: No hourly data collected
- **WHEN** no hourly usage data exists (fresh install or no executions yet)
- **THEN** the hourly heatmap section SHALL be hidden

### Requirement: Error State on Data Load Failure
The Analytics page SHALL display an error message when usage data cannot be loaded, instead of silently clearing the loading indicator.

#### Scenario: Data load fails
- **WHEN** `SettingsAnalyticsPageViewModel.LoadAsync()` throws an exception
- **THEN** the page SHALL display a localized error message with a retry button
- **AND** the loading spinner SHALL be hidden

#### Scenario: User retries after error
- **WHEN** the user clicks the retry button in the error state
- **THEN** the system SHALL re-attempt `LoadAsync()`

### Requirement: Absolute Slot Heatmap Percentage
The slot usage heatmap SHALL display each slot's execution count as a percentage of total slot executions, not relative to the busiest slot.

#### Scenario: Slot percentage calculation
- **WHEN** Slot 1 has 100 executions, Slot 2 has 50 executions, and no other slots are used
- **THEN** Slot 1 SHALL display 67% and Slot 2 SHALL display 33%

### Requirement: Localized Analytics Display Strings
All user-visible text in the analytics UI SHALL be resolved through `ILocalizationService`, with no hardcoded English strings.

#### Scenario: Time-ago format in current locale
- **WHEN** a plugin was last used 5 minutes ago and the UI language is Chinese
- **THEN** the LastUsedFormatted text SHALL display the Chinese equivalent of "5 minutes ago"

#### Scenario: Duration format in current locale
- **WHEN** average execution time is 1500ms
- **THEN** DurationFormatted SHALL display "1.5 s" with the unit abbreviation from the current locale

### Requirement: Analytics Formatter Separation
Plugin analytics display formatting SHALL be extracted from `PluginViewModel` into a dedicated formatter class that takes raw data and produces formatted display strings.

#### Scenario: Formatter produces localized display model
- **WHEN** `PluginAnalyticsFormatter.Format(usageStats, healthReport, localizationService)` is called
- **THEN** it SHALL return a model with all display strings (UsageSummary, LastUsedSummary, HealthBadge, SuccessRateText, etc.) pre-formatted in the current locale
