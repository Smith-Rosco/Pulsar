## MODIFIED Requirements

### Requirement: Plugin Usage Analytics Display
The Settings application SHALL display usage statistics gathered by the `PluginUsageTracker` to help users optimize their configurations. All user-visible text in the analytics view SHALL be localized through `ILocalizationService`. The view SHALL include an error state when data cannot be loaded, and SHALL display an hourly usage distribution heatmap. The plugin list SHALL support time-range filtering and column sorting. Recommendation cards SHALL include actionable buttons.

#### Scenario: User views most used plugins
- **WHEN** the user navigates to the Analytics tab in the Settings window
- **THEN** the system SHALL display a ranked list of the top most frequently executed plugins with success rate, average duration, slot/mode breakdown, and daily usage trend sparklines

#### Scenario: User views execution performance
- **WHEN** the user views the usage stats for a specific plugin
- **THEN** the system SHALL display its average execution time, success/failure counts, and slot usage distribution

#### Scenario: User views hourly usage heatmap
- **WHEN** the user scrolls to the hourly usage section on the Analytics page
- **THEN** the system SHALL display a 24-hour bar chart showing execution distribution across hours

#### Scenario: Data load error handling
- **WHEN** the analytics data fails to load due to an exception
- **THEN** the system SHALL display a localized error message and retry button instead of a blank page

#### Scenario: User filters by time range
- **WHEN** the user selects a time range filter (Today / Week / Month / All Time)
- **THEN** all displayed data SHALL be recomputed for that time range

#### Scenario: User sorts by column
- **WHEN** the user clicks a sortable column header
- **THEN** the plugin list SHALL reorder by that column, with an indicator showing the current sort direction

#### Scenario: User acts on a recommendation
- **WHEN** the user clicks an action button on a recommendation card
- **THEN** the corresponding action (disable plugin or view logs) SHALL execute
