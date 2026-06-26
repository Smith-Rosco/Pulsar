## 1. Data Collection Layer

- [x] 1.1 Update `IPluginUsageTracker` interface: remove `RecordSlotUsage`, add slot/mode params to `RecordExecution`
- [x] 1.2 Merge `RecordSlotUsage` logic into `RecordExecution` in `PluginUsageTracker.cs` — slot, mode, and hourly now recorded in single call
- [x] 1.3 Remove duplicate `HourlyUsage` increment in former `RecordSlotUsage` path
- [x] 1.4 Make `_isDirty` thread-safe using `Volatile.Write`/`Volatile.Read`
- [x] 1.5 Update `PluginRuntimeKernel.Complete()` to pass slot/mode through `RecordExecution`
- [x] 1.6 Update `SlotStrategies.PluginActionStrategy` to remove `RecordSlotUsage` calls, pass slot/mode via `RecordExecution` instead
- [x] 1.7 Update any remaining `RecordSlotUsage` callers to use unified `RecordExecution`

## 2. Localization Keys

- [x] 2.1 Add `Settings.Analytics.*` keys to `Strings.resx`: `JustNow`, `MinutesAgoFormat`, `HoursAgoFormat`, `DaysAgoFormat`, `DurationMs`, `DurationS`, `FavoriteSlotFormat`, `ModeTask`, `ModeAction`, `ErrorLoading`, `SlotPercentageFormat`
- [x] 2.2 Add `Plugin.Recommendation.*` keys to `Strings.resx`: `UnusedNeverUsed`, `UnusedDaysFormat`, `UnusedTitle`, `HighErrorTitle`, `HighErrorFormat`, `CircuitBreakerTitle`, `CircuitBreakerFormat`, `DisableAction`, `ViewLogsAction`
- [x] 2.3 Add corresponding Chinese translations to `Strings.zh-CN.resx` for all new keys

## 3. Display Models

- [x] 3.1 Update `AnalyticsItem` — replace computed format properties with pre-set localized strings; change properties from `init`-only to settable for ViewModel population
- [x] 3.2 Add `HourlyHeatmapItem` class (Hour, TotalExecutions, Percentage, BarWidth, Label) to `SettingsAnalyticsPageViewModel.cs`
- [x] 3.3 Create `PluginAnalyticsFormatter` helper class with `Format(PluginUsageStats, PluginHealthReport, ILocalizationService)` returning formatted display strings

## 4. ViewModel Layer

- [x] 4.1 Inject `ILocalizationService` into `SettingsAnalyticsPageViewModel`, use it to pre-compute localized `AnalyticsItem` properties in `LoadAsync()`
- [x] 4.2 Add `HasError`, `ErrorMessage` observable properties; set in `LoadAsync()` catch block
- [x] 4.3 Add `ObservableCollection<HourlyHeatmapItem>` and compute hourly heatmap from aggregated `allStats.HourlyUsage`
- [x] 4.4 Fix slot heatmap percentage: change from `slotTotal / maxSlotTotal * 100` to `slotTotal / totalAllSlotExecutions * 100`
- [x] 4.5 Inject `ILocalizationService` into `PluginRecommendationEngine`, replace hardcoded English strings with localized resource keys
- [x] 4.6 Replace manual `OnPropertyChanged` string formatting in `PluginViewModel.LoadAnalytics()` with calls to `PluginAnalyticsFormatter`

## 5. UI Layer

- [x] 5.1 Add error state UI to `SettingsAnalyticsPage.xaml` — error message card with retry button, bound to `HasError`/`ErrorMessage`
- [x] 5.2 Add hourly heatmap section to `SettingsAnalyticsPage.xaml` — 24-bar horizontal chart using same pattern as slot heatmap
- [x] 5.3 Verify page loads with `IThemeService.ApplyTheme()` after `InitializeComponent()` (per theme injection timing rules)

## 6. Build & Verify

- [x] 6.1 Run `dotnet build` — ensure no compilation errors from interface changes
- [x] 6.2 Run `dotnet build Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` — verify test project compiles with refactored interfaces
- [x] 6.3 Run `dotnet test` — verify all existing tests pass
- [ ] 6.4 Spot-check analytics page rendering in both English and Chinese locales
