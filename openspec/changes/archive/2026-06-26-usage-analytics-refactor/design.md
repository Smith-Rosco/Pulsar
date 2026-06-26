## Context

The usage analytics system has three data collection layers (`PluginUsageTracker`, `PluginHealthMonitor`, `PluginRecommendationEngine`) and two UI consumers (`SettingsAnalyticsPage`, `PluginViewModel`). The system is functional but has accumulated technical debt since its introduction in the `pulsar-flow-enhancements` change. Performance profiling and code review identified five specific issues that motivate this refactoring.

Current data flow:

```
RecordExecution(id, success, time)        → totals, daily, hourly, avg time
RecordSlotUsage(id, slot, mode)           → slot, mode, hourly (DUPLICATE)
```

Two call sites must both fire for complete data; hourly data is double-counted.

## Goals / Non-Goals

**Goals:**

1. Eliminate dual-path slot/mode recording — all telemetry flows through a single `RecordExecution` call
2. Fix double-counted `HourlyUsage` (incremented by both `RecordExecution` and `RecordSlotUsage`)
3. Localize all hardcoded English strings in analytics display code
4. Add error state to analytics page (currently silent failure on `LoadAsync` exception)
5. Fix slot heatmap to show absolute usage percentage instead of relative-to-max
6. Surface `HourlyUsage` data as an hourly heatmap in the UI
7. Improve thread safety of `_isDirty` flag
8. Extract analytics formatting from `PluginViewModel` into a dedicated formatter

**Non-Goals:**

- Adding trend graphs or charting libraries (future feature, out of scope)
- Adding data export functionality (future feature, out of scope)
- Rewriting `PluginRecommendationEngine` localization architecture (add keys only)
- Full PluginViewModel split into separate services (partial refactor only — extract formatter)
- Adding test coverage (separate future change)

## Decisions

### Decision 1: Merge RecordSlotUsage into RecordExecution

**Choice**: Remove `RecordSlotUsage` from `IPluginUsageTracker`. Extend the `RecordExecution` signature to always accept `slotIndex` (int, default 0) and `mode` (string, default empty). Callers in the execution pipeline pass slot/mode directly; the 4-param convenience overload calls the 6-param with defaults.

**Rationale**: The current dual-path design requires two coordinated calls for complete telemetry. If a new execution path forgets `RecordSlotUsage`, slot data silently goes missing. Merging into one call enforces completeness at the interface level.

**Alternatives considered**:
- *Keep both but document contract* — doesn't prevent human error, no compiler enforcement
- *Event-based recording* — adds complexity (event bus/mediator pattern) without proportional benefit for a single-consumer system

### Decision 2: Atomic _isDirty

**Choice**: Use `Volatile.Write(ref _isDirty, true)` for writes and `Volatile.Read(ref _isDirty)` for reads. Alternatively, wrap in `Interlocked.Exchange`.

**Rationale**: The current plain `bool _isDirty` read/write is not guaranteed to be visible across threads on all memory models. While the race is benign (worst case: extra save), it should be fixed.

**Alternatives considered**:
- *Use `volatile` keyword* — equivalent but less explicit about intent
- *Wrap in `lock`* — unnecessary heavyweight synchronization for a flag

### Decision 3: Localization Strategy for AnalyticsItem

**Choice**: Pre-compute localized strings in `SettingsAnalyticsPageViewModel.LoadAsync()` using injected `ILocalizationService`, store them as computed properties (now settable with `init`→`set` or constructor params). For `PluginRecommendationEngine`, inject `ILocalizationService` and use resource keys for title/message generation.

**Rationale**: `AnalyticsItem` properties like `LastUsedFormatted` and `DurationFormatted` are data-bound in XAML and must be properties, not method calls. Pre-computing during ViewModel construction keeps the model clean while enabling localization. The engine already has DI — adding `ILocalizationService` is trivial.

**Alternatives considered**:
- *Pass `ILocalizationService` into `AnalyticsItem` constructor* — couples display model to service layer, violates model hygiene
- *Use XAML `StringFormat` with resource bindings* — WPF `StringFormat` cannot reference dynamic resources from the loc system

### Decision 4: PluginViewModel Analytics Formatter

**Choice**: Create `PluginAnalyticsFormatter` helper class that takes raw `PluginUsageStats`, `PluginHealthReport`, and `ILocalizationService`, and produces formatted display strings. `PluginViewModel.LoadAnalytics()` calls the formatter and sets its display properties via `OnPropertyChanged`.

**Rationale**: Extracts 10 formatting methods and properties from the 434-line `PluginViewModel` without changing the WPF binding surface. Minimal risk, maximum clarity.

**Alternatives considered**:
- *Full PluginAnalyticsViewModel* — breaks existing XAML bindings, higher regression risk
- *Do nothing* — leaves 5+ responsibilities in one class, compounds over time

### Decision 5: Hourly Heatmap Data Model

**Choice**: Add `HourlyHeatmapItem` class (similar to existing `SlotHeatmapItem`) and compute aggregated hourly usage across all plugins in `LoadAsync()`. Display as a horizontal bar chart below the slot heatmap.

**Rationale**: `HourlyUsage` data is already collected (dictionary keyed 0-23) but never displayed. Zero additional recording cost — pure display addition.

## Risks / Trade-offs

- **[Risk] Interface breaking change**: Removing `RecordSlotUsage` from `IPluginUsageTracker` requires updating all callers. → **Mitigation**: All callers are internal (pipeline kernel, strategy classes). Controlled refactoring with compiler verification.
- **[Risk] Data loss during migration**: If `RecordExecution` refactoring misses a call site, slot data stops being recorded. → **Mitigation**: Static analysis via `dotnet build` catches missing method calls. Pipeline kernel is the primary recording point — it already has slot/mode context.
- **[Risk] Regression in PluginViewModel**: Extracting formatter could introduce null-reference issues if `ILocalizationService` is not available. → **Mitigation**: Formatter defensively handles null `ILocalizationService` with English fallbacks (same as current `_loc?[...] ?? "..."` pattern).
- **[Trade-off] AnalyticsItem settability**: Making format properties settable (from `init`-only) creates a mutation surface that didn't exist before. → **Acceptable**: The properties are only set once during ViewModel construction, same behavior as `init`.
