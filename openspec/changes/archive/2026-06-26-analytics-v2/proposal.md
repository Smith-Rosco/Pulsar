## Why

The analytics page (built in `pulsar-flow-enhancements`, hardened in `usage-analytics-refactor`) displays raw usage data but offers no interactivity: users cannot filter by time range, sort results, or act on recommendations. The system collects rich telemetry (daily stats, hourly patterns, slot preferences) but only surfaces a flat snapshot. Additionally, the entire analytics stack has zero dedicated unit tests, making every refactoring a regression risk. This change adds filtering, sorting, actionable insights, and a comprehensive test safety net.

## What Changes

- **Time range filter**: Combo box to switch between All Time / Today / This Week / This Month, filtering displayed plugin stats and recalculating summary metrics
- **Sortable plugin list**: Clickable column headers (Executions, Success Rate, Avg Duration, Last Used) to reorder the most-used plugins list
- **Actionable recommendations**: "Disable Plugin" and "View Logs" buttons directly in recommendation cards, wired to `IPluginRegistry` and log dialog
- **Usage trend sparklines**: Mini bar chart showing daily execution counts for the past 7/14 days per plugin, using the existing `DailyStats` data
- **Inactivity alerts**: Notification when a previously-frequent plugin goes unused for N days (configurable threshold)
- **Slot optimization recommendations**: Engine analyzes `SlotUsage` + `HourlyUsage` to suggest moving frequently-used plugins to prime-access slots
- **Data export**: "Export CSV" button downloads analytics as comma-separated values for external analysis
- **Full unit test suite**: xUnit tests for `PluginUsageTracker`, `PluginHealthMonitor`, `PluginRecommendationEngine`, and `SettingsAnalyticsPageViewModel`

## Capabilities

### New Capabilities
- `analytics-test-coverage`: Unit tests covering all analytics services and the analytics ViewModel
- `analytics-ui-ux`: Time range filter, sortable plugin table, and clickable recommendation actions
- `analytics-advanced-features`: Trend sparklines, inactivity alerts, slot optimization recommendations, CSV export

### Modified Capabilities
- `usage-analytics-ui`: Enhanced with sortable columns, time filter controls, and actionable recommendation cards

## Impact

- **Affected files**:
  - `Pulsar/Pulsar.Tests/` — new test classes: `PluginUsageTrackerTests`, `PluginHealthMonitorTests`, `PluginRecommendationEngineTests`, `SettingsAnalyticsPageViewModelTests`
  - `ViewModels/Settings/SettingsAnalyticsPageViewModel.cs` — time filter, sorting, trend sparkline data, export command
  - `Views/Pages/SettingsAnalyticsPage.xaml` — filter combo, sortable headers, sparkline template, export button, actionable rec cards
  - `Views/Pages/SettingsAnalyticsPage.xaml.cs` — event handlers if needed
  - `Services/PluginRecommendationEngine.cs` — slot optimization + inactivity alert generation
  - `Services/Interfaces/IPluginRecommendationEngine.cs` — may add new recommendation types
  - `Resources/Strings.resx` / `Strings.zh-CN.resx` — new localization keys for filters, sorting, export, alerts

- **New dependency**: None. All features use existing `DailyStats` / `SlotUsage` / `HourlyUsage` data already collected by `PluginUsageTracker`. Sorting and filtering are pure ViewModel operations. CSV generation uses standard `System.Text.StringBuilder`.
