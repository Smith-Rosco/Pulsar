## 1. Test Suite — PluginUsageTracker

- [x] 1.1 Create `Pulsar.Tests/Services/PluginUsageTrackerTests.cs` with Moq mock for `ILogger<PluginUsageTracker>`
- [x] 1.2 Test `RecordExecution` — single call updates all fields (totals, daily, hourly, avg time, slot, mode)
- [x] 1.3 Test concurrent recording — 4 threads × 100 calls = 400 TotalExecutions
- [x] 1.4 Test `GetStats` returns cloned copy that doesn't mutate internal state
- [x] 1.5 Test `GetAllStats` returns all plugins
- [x] 1.6 Test `GetMostUsedPlugins(N)` returns top N sorted by TotalExecutions
- [x] 1.7 Test `GetUnusedPlugins(days)` filters correctly by LastUsed threshold
- [x] 1.8 Test `SaveAsync` / `LoadAsync` roundtrip preserves all data
- [x] 1.9 Test daily stats cleanup removes entries older than 30 days
- [x] 1.10 Test `Dispose` saves dirty data

## 2. Test Suite — PluginHealthMonitor

- [x] 2.1 Create `Pulsar.Tests/Services/PluginHealthMonitorTests.cs`
- [x] 2.2 Test `RecordSuccess` / `RecordError` populate recent execution buffer
- [x] 2.3 Test `CalculateHealthScore` returns 100 for 100% success rate
- [x] 2.4 Test `CalculateHealthScore` penalizes errors (50% error rate = score 75)
- [x] 2.5 Test `CalculateHealthScore` penalizes circuit breaker trips
- [x] 2.6 Test `CalculateHealthScore` penalizes unused plugins
- [x] 2.7 Test `RecordCircuitBreakerTrip` sets breaker state to open
- [x] 2.8 Test `RecordCircuitBreakerRecovery` sets breaker state to closed
- [x] 2.9 Test `DetermineHealthStatus` returns correct status (Healthy/Warning/Critical/Unused)
- [x] 2.10 Test `GetAllHealthReports` returns reports for all tracked plugins

## 3. Test Suite — PluginRecommendationEngine

- [x] 3.1 Create `Pulsar.Tests/Services/PluginRecommendationEngineTests.cs`
- [x] 3.2 Test never-used plugin triggers `DisableUnusedPlugin` recommendation
- [x] 3.3 Test plugin unused >30 days triggers `DisableUnusedPlugin` recommendation
- [x] 3.4 Test high error rate (>20%) triggers `CheckPluginErrors` recommendation
- [x] 3.5 Test circuit breaker trips trigger recommendation
- [x] 3.6 Test core plugins (`CanDisable=false`) are excluded from recommendations
- [x] 3.7 Test `GetRecommendationsForPlugin` returns only recommendations for that plugin

## 4. Test Suite — SettingsAnalyticsPageViewModel

- [x] 4.1 Create `Pulsar.Tests/ViewModels/SettingsAnalyticsPageViewModelTests.cs`
- [x] 4.2 Test `LoadAsync` populates `MostUsedPlugins` from tracker data
- [x] 4.3 Test `LoadAsync` populates `SlotHeatmap` and `HourlyHeatmap`
- [x] 4.4 Test `LoadAsync` computes summary metrics (total executions, active count, today, week)
- [x] 4.5 Test `LoadAsync` handles empty data (HasData=false, no exception)
- [x] 4.6 Test `LoadAsync` sets `HasError=true` and `ErrorMessage` on exception
- [x] 4.7 Test `RefreshCommand` re-invokes `LoadAsync`

## 5. Loc Keys for New Features

- [x] 5.1 Add time filter labels to `Strings.resx`/`zh-CN.resx`: `Settings.Analytics.FilterAllTime`, `FilterToday`, `FilterThisWeek`, `FilterThisMonth`
- [x] 5.2 Add sort labels: `Settings.Analytics.SortByExecutions`, `SortBySuccessRate`, `SortByDuration`, `SortByLastUsed`
- [x] 5.3 Add recommendation action labels: `Settings.Analytics.DisablePlugin`, `ViewLogs`
- [x] 5.4 Add export/trend labels: `Settings.Analytics.ExportCsv`, `TrendNoData`, `Recommendation.MoveSlot`
- [x] 5.5 Add inactivity alert labels: `Plugin.Recommendation.InactiveTitle`, `InactiveFormat`

## 6. Time Range Filter + Column Sorting

- [x] 6.1 Add `AnalyticsTimeRange` enum (AllTime/Today/ThisWeek/ThisMonth) and `SortColumn` enum to ViewModel
- [x] 6.2 Add `TimeRange` and `SortColumn`/`SortAscending` observable properties to `SettingsAnalyticsPageViewModel`
- [x] 6.3 Implement `ApplyFilter()` method — recompute `MostUsedPlugins` using `DailyStats` for date-bounded queries
- [x] 6.4 Implement `ApplySort()` method — sort the filtered list by the selected column
- [x] 6.5 Add `SetSortCommand` and `SetFilterCommand` relay commands
- [x] 6.6 Wire filter/sort property changes to auto-refresh the collections
- [x] 6.7 Add filter ComboBox and sort column headers to `SettingsAnalyticsPage.xaml`

## 7. Actionable Recommendations

- [x] 7.1 Add `ActionCommand` and `ActionParameter` string properties to `PluginRecommendation` model
- [x] 7.2 Add `DisablePluginCommand` and `ViewLogsCommand` to `SettingsAnalyticsPageViewModel`
- [x] 7.3 Wire command buttons in recommendation card DataTemplate using `RelativeSource AncestorType=Page` binding
- [x] 7.4 Remove recommendation from list after disable action succeeds
- [x] 7.5 Refresh analytics data after plugin state change

## 8. Trend Sparklines

- [x] 8.1 Add `DailyTrendItem` class (Date, Count, MaxCount, BarWidth) to ViewModel
- [x] 8.2 Add `TrendData` list property to `AnalyticsItem` (7 daily bars)
- [x] 8.3 Populate trend data from `pluginStats.DailyStats` in `LoadAsync()`
- [x] 8.4 Add sparkline rendering template to `SettingsAnalyticsPage.xaml` — thin `Border` strips in plugin card

## 9. Advanced Features — Engine

- [x] 9.1 Add `RecommendationType.InactivePlugin` and `RecommendationType.OptimizeSlotPlacement` to enum
- [x] 9.2 Implement `CheckInactivePlugin()` in `PluginRecommendationEngine` — alerts when >50 execs plugin goes 7+ days unused
- [x] 9.3 Implement `CheckSlotOptimization()` in `PluginRecommendationEngine` — suggests moving from high-slot to slot 1-2
- [x] 9.4 Add loc keys for new recommendation types
- [x] 9.5 Wire new recommendation types into `GetRecommendations()`

## 10. CSV Export

- [x] 10.1 Add `ExportCsvCommand` to `SettingsAnalyticsPageViewModel`
- [x] 10.2 Implement CSV generation method using `StringBuilder` with RFC 4180 escaping
- [x] 10.3 Show `SaveFileDialog` to get output path, write file
- [x] 10.4 Add "Export CSV" button to `SettingsAnalyticsPage.xaml`
- [x] 10.5 Disable export button when `HasData` is false

## 11. Build & Verify

- [x] 11.1 Run `dotnet build` — ensure no compilation errors
- [x] 11.2 Run `dotnet test` — verify all new and existing tests pass
- [ ] 11.3 Spot-check analytics page: filter dropdown, sort headers, sparklines, actionable buttons, CSV export
