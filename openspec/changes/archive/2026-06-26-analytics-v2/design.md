## Context

The analytics page (`SettingsAnalyticsPage`) currently displays a static snapshot: top-20 plugins sorted by total executions, a slot heatmap, an hourly heatmap, and passive recommendations. There is no filtering, sorting, or ability to act on the displayed data. Every page load re-computes from the full dataset. The backend (`PluginUsageTracker`, `PluginHealthMonitor`, `PluginRecommendationEngine`) has zero dedicated test coverage.

This design covers three additions: user-facing interactivity (filters, sort, actions), advanced analytics (trends, alerts, optimization, export), and a test suite.

## Goals / Non-Goals

**Goals:**
1. Add time range filter (All Time / Today / This Week / This Month) that filters displayed data
2. Add click-to-sort on plugin list columns (Executions, Success Rate, Duration, Last Used)
3. Make recommendation cards actionable with "Disable Plugin" and "View Logs" buttons
4. Display per-plugin daily trend as a mini horizontal bar sparkline
5. Generate inactivity alerts via the recommendation engine
6. Generate slot optimization suggestions via the recommendation engine
7. Add CSV export for the displayed analytics data
8. Unit test coverage for `PluginUsageTracker`, `PluginHealthMonitor`, `PluginRecommendationEngine`, `SettingsAnalyticsPageViewModel`

**Non-Goals:**
- Real-time/live-updating analytics (requires SSE or polling — future feature)
- Line/area charts (requires charting library — excluded to avoid dependency)
- Export to PDF or other formats (CSV only for v2)
- Plugin recommendation auto-apply (user manually acts on recommendations)

## Decisions

### Decision 1: ViewModel-Level Time Filtering

**Choice**: Store the full unfiltered dataset privately in the ViewModel. Apply a `TimeRange` enum filter (`AllTime`, `Today`, `ThisWeek`, `ThisMonth`) that recomputes `MostUsedPlugins`, summary metrics, and heatmaps from the cached data without re-querying the tracker.

**Rationale**: Avoids N+1 query pattern. The full dataset is already in memory from `GetAllStats()`. Filtering is O(n) in-memory and runs on every filter change — negligible for <50 plugins.

**Alternatives considered**:
- *Re-query tracker with date parameters* — adds complexity to `IPluginUsageTracker` for filtering that can be done trivially in memory
- *ComputedObservableCollection with filter predicate* — overkill for a single page with manual refresh pattern

### Decision 2: Manual Sort on Column Click

**Choice**: Add a `SortProperty` enum and `SortDirection` flag to the ViewModel. When a column header is clicked, set the sort state and re-order the `ObservableCollection` by clearing and re-adding in sorted order.

**Rationale**: `ObservableCollection` doesn't support `ICollectionView` naturally, and `CollectionViewSource.GetDefaultView` has quirks with `ObservableCollection`. Manual re-sort is simpler and more predictable with WPF's `ItemsControl` data templates.

**Alternatives considered**:
- *ICollectionView/ListCollectionView* — more idiomatic but trickier with the existing `ItemsControl` pattern; changing to `ListView` would be a bigger XAML refactor
- *ComboBox for sort field* — introduces additional control for a secondary action; direct column click is more intuitive

### Decision 3: Sparkline Rendering Without Charting Library

**Choice**: Use a thin WPF `Grid` with `Border` elements per day, where width is proportional to that day's execution count relative to the max. No third-party charting dependency.

**Rationale**: The daily stats are at most 14 values (7 days). A simple horizontal bar strip of thin rectangles is clean, fast to render, and requires zero new dependencies. Pulsar explicitly avoids charting libraries.

### Decision 4: Actionable Recommendations via Command Parameter

**Choice**: Each `PluginRecommendation` model gains an `ActionCommand` parameter (the plugin ID). The ViewModel exposes `DisablePluginCommand` and `ViewLogsCommand` that resolve via `IPluginRegistry` and `IDialogService`. Buttons in the recommendation card bind `Command` to the page's DataContext and `CommandParameter` to the PluginId.

**Challenge**: `DataContext` of items inside `ItemsControl.ItemTemplate` is the recommendation item, not the page ViewModel. Solution: use `RelativeSource AncestorType=Page` to reach the page's DataContext, or a `Tag` bridge pattern per the UserControl binding lessons.

**Decision**: Use explicit `Command` binding with `RelativeSource`:
```xml
Command="{Binding DataContext.DisablePluginCommand, RelativeSource={RelativeSource AncestorType=Page}}"
CommandParameter="{Binding PluginId}"
```

### Decision 5: CSV Export

**Choice**: Add an `ExportCsvCommand` to the ViewModel. Generate CSV in-memory using `StringBuilder`, write via `SaveFileDialog` or to a known path. Columns: Rank, PluginId, DisplayName, TotalExecutions, SuccessRate, AvgDurationMs, FavoriteSlot, PrimaryMode, LastUsed.

No external CSV library needed. Use RFC 4180 escaping (quote fields containing commas or quotes).

### Decision 6: Test Organization

**Choice**: Create test files mirroring the source structure:
- `Pulsar.Tests/Services/PluginUsageTrackerTests.cs`
- `Pulsar.Tests/Services/PluginHealthMonitorTests.cs`
- `Pulsar.Tests/Services/PluginRecommendationEngineTests.cs`
- `Pulsar.Tests/ViewModels/SettingsAnalyticsPageViewModelTests.cs`

Use `Moq` for dependency mocking (already in the test project). Tests instantiate the concrete implementations directly for unit tests; mock only external dependencies (`ILogger<T>`, `IPluginRegistry`).

## Risks / Trade-offs

- **[Risk] ObservableCollection clear/re-add on sort causes UI flicker** → **Mitigation**: Use `SuppressNotification` pattern or batch replace. If flicker is visible, switch to replacing the entire collection reference.
- **[Risk] Sparkline with many days could overflow** → **Mitigation**: Cap display at 7 days; older DailyStats keys auto-pruned by the tracker at 30 days.
- **[Risk] Actionable recommendations depend on services not available in all contexts** → **Mitigation**: `SettingsAnalyticsPageViewModel` already injects `IPluginRegistry`. Add optional `IDialogService` and `ILogService` with null checks.
- **[Trade-off] CSV export uses a file dialog** → WPF `SaveFileDialog` requires STA thread; already on UI thread from command binding.
- **[Trade-off] Sorting is client-side only** → Full dataset is already loaded; server-side sorting offers no benefit for <100 items.
