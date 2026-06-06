## Why

The plugin execution pipeline has no thread safety boundaries between the OS keyboard hook thread, the WPF UI dispatcher, and the internally composed runtime components. Three synchronized dictionaries in production code use plain `Dictionary<K,V>` without locks, `async void` fire-and-forget calls bypass exception propagation at the application entry point, and 11 `.GetAwaiter().GetResult()` calls risk deadlock. These are not theoretical — Windows will silently unhook `WH_KEYBOARD_LL` if the callback blocks for more than a few seconds, permanently disabling all hotkeys until restart.

## What Changes

- **Hotkey dispatch boundary**: All hotkey action invocations pass through `Dispatcher.InvokeAsync()` before reaching any UI or plugin code. The keyboard hook thread never synchronously touches WPF properties, plugin state, or `PulsarContext`.
- **Thread-safe runtime dictionaries**: `PluginRuntimeStateStore._plugins`, `PluginRuntimeStateStore._snapshots`, `PluginCircuitBreakerPolicy._failureCounts`, and `PluginCircuitBreakerPolicy._brokenCircuits` migrate from `Dictionary` to `ConcurrentDictionary`.
- **Atomic UI guard**: `_isLoading` in `RadialMenuViewModel.Show()` replaced with `Interlocked.CompareExchange` pattern.
- **Eliminate sync-over-async**: All 11 `.GetAwaiter().GetResult()` call sites converted to proper `await`. Synchronous wrappers (`ActivateWindowDetailed`, `ActivateWindow`) removed or moved to callers who already run on thread-pool threads.
- **Fire-and-forget audit**: `_ = executeSelectionAsync()` discard in `RadialMenuInputCoordinator` replaced with try/catch-await to prevent silent exception loss. Remaining `_ = Task.Run(...)` fire-and-forget calls moved to `IBackgroundWorkScheduler`.
- **Plugin runtime DI composition**: `PluginCircuitBreakerPolicy`, `PluginExecutionPipeline`, `PluginRuntimeKernel`, `PluginCatalog`, and `PluginRuntimeStateStore` registered as DI singletons via extension method instead of manual `new` in `PluginRegistry` constructor.
- **PulsarContext immutability restored**: `CurrentPluginId` and `PermissionInterceptor` moved into `PluginExecutionContext` (already exists for logging). `PulsarContext` becomes fully readonly after construction, matching its documented contract.
- **Remove PluginRegistryV2 code**: The 568-line `PluginRegistryV2.cs` is dead code — never DI-registered, never called. Its permission interceptor logic is extracted into the active pipeline; the rest is deleted.

No breaking changes. No user-visible behavior change. Existing tests and simulator must pass.

## Capabilities

### New Capabilities

- `thread-safe-plugin-state`: Plugin runtime state stores and circuit breaker counters use thread-safe collections with atomic transitions, preventing data races under concurrent execution.

### Modified Capabilities

- `plugin-runtime-kernel`: Runtime kernel components (catalog, state store, breaker policy, execution pipeline) are now composed via DI instead of manual instantiation, and state stores use `ConcurrentDictionary`.
- `lightweight-context-capture`: `PulsarContext` becomes fully immutable after construction. Mutable per-execution data (`CurrentPluginId`, `PermissionInterceptor`) moves to `PluginExecutionContext`.
- `keyboard-hook-focus-sync`: Hotkey action dispatch explicitly marshals to the UI dispatcher thread before executing any UI or plugin code.

## Impact

**Affected code**:
- `Services/PluginRegistry.cs` — constructor simplified from manual composition to DI constructor injection
- `Core/Plugin/Runtime/PluginRuntimeKernel.cs` — `PluginCircuitBreakerPolicy`, `PluginRuntimeStateStore`, `PluginCatalog` use `ConcurrentDictionary`; `PluginCircuitBreakerPolicy` constructor parameter `ILogger` made non-nullable
- `Core/Plugin/PulsarContext.cs` — `CurrentPluginId` and `PermissionInterceptor` removed; moved to `PluginExecutionContext`
- `Core/Plugin/PluginExecutionContext.cs` — gains `CurrentPluginId` and `PermissionInterceptor` properties
- `ViewModels/RadialMenuViewModel.cs` — `Show()` adds dispatcher assertion and `Interlocked` guard; `_isLoading` bool → `_isLoading` int
- `ViewModels/RadialMenuInputCoordinator.cs` — `_ = executeSelectionAsync()` → awaited with try/catch
- `Services/HotkeyService.cs` — `item.Action.Invoke()` → `Dispatcher.InvokeAsync(item.Action)`
- `Services/WindowService.cs` — sync wrappers `ActivateWindowDetailed()`, `ActivateWindow()` removed or refactored
- `Services/AppStartupCoordinator.cs` — uses `IPluginRegistry` interface instead of concrete `PluginRegistry`
- `Services/ConfigService.cs` — fire-and-forget `_ = Task.Run(...)` save moved to `IBackgroundWorkScheduler`
- `Services/PluginRegistryV2.cs` — deleted; permission interceptor logic extracted
- `App.xaml.cs` — registers new runtime services via `AddPluginRuntime()` extension; `PluginRegistry` registration replaced with `IPluginRegistry`
- `Pulsar.Tests/Plugin/PluginRuntimeKernelTests.cs` — updated to use DI-composed instances or mock interfaces

**Dependencies**: None. No external API changes.

**Risk**: Medium. The changes touch the critical hotkey-to-execution path. Each phase is independently testable via existing unit tests and the simulator.
