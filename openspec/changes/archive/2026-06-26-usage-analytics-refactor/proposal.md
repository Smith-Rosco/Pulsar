## Why

The usage analytics system (introduced in `pulsar-flow-enhancements`) has a fragile dual-path data collection architecture where slot/mode tracking is split across `RecordExecution` (pipeline) and `RecordSlotUsage` (strategies). This causes double-counted hourly data, risks missing slot metadata when execution paths change, and violates the single-writer principle. Additionally, the UI layer contains seven hardcoded English strings violating the project's localization invariant, and has no error state for failed data loads.

## What Changes

- **Merge data collection paths**: Eliminate `RecordSlotUsage`, pass slot/mode info through `RecordExecution` so all telemetry flows through a single call site
- **Fix double-counted HourlyUsage**: Remove duplicate `HourlyUsage` increments in `RecordSlotUsage` that compound with `RecordExecution`
- **Thread safety hardening**: Make `_isDirty` updates atomic via `Volatile.Write` / `Interlocked.Exchange`
- **Localize all hardcoded strings**: Replace English strings in `AnalyticsItem`, `PluginRecommendationEngine`, and `PluginViewModel` with `ILocalizationService` lookups or resource keys
- **Add error state to analytics UI**: Show an error message when `LoadAsync` fails instead of silently clearing the loading spinner
- **Fix slot heatmap percentage**: Change from relative-to-max-slot to absolute percentage of total slot executions, so users see real usage intensity
- **Surface HourlyUsage data**: Add an hourly heatmap section to the analytics page (data already collected, never displayed)
- **Separate analytics from PluginViewModel**: Extract analytics display properties into a dedicated `PluginAnalyticsViewModel` to reduce `PluginViewModel`'s 5+ responsibilities

## Capabilities

### New Capabilities
- `usage-tracking-data-integrity`: Fixes the dual-path slot/mode recording, double-counted hourly data, and thread-safety issues in `PluginUsageTracker`
- `usage-analytics-ui-enhancements`: Adds hourly heatmap display, error state, fixes slot heatmap percentage calculation, localizes all display strings

### Modified Capabilities
- `usage-analytics-ui`: Existing display requirements are enhanced with hourly heatmap visualization and error state handling. The localization requirement (hardcoded strings) is tightened — all user-visible text in analytics must route through `ILocalizationService`.

## Impact

- **Affected files**:
  - `Services/PluginUsageTracker.cs` — merge `RecordSlotUsage` into `RecordExecution`, atomic dirty flag
  - `Services/Interfaces/IPluginUsageTracker.cs` — remove `RecordSlotUsage`, update `RecordExecution` signature to always accept slot/mode
  - `Core/Plugin/Runtime/PluginRuntimeKernel.cs` — pass slot/mode info through pipeline completion
  - `ViewModels/Strategies/SlotStrategies.cs` — remove `RecordSlotUsage` calls
  - `ViewModels/Settings/SettingsAnalyticsPageViewModel.cs` — add error state, hourly heatmap data, localization
  - `ViewModels/Settings/AnalyticsItem.cs` — localize hardcoded format strings
  - `ViewModels/Settings/PluginViewModel.cs` — extract analytics display to new model
  - `Views/Pages/SettingsAnalyticsPage.xaml` — hourly heatmap section, error state trigger
  - `Services/PluginRecommendationEngine.cs` — localize recommendation messages
  - `Resources/Strings.resx` / `Strings.zh-CN.resx` — new localization keys

- **Breaking changes**: `RecordSlotUsage` is removed from `IPluginUsageTracker`. All callers must migrate to the unified `RecordExecution` overload that includes slot/mode. This is an internal interface change (no plugin-facing API impact).
