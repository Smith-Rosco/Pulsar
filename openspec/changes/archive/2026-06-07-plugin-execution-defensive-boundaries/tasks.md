## 1. Interface Contracts (Breaking Changes)

- [x] 1.1 Add `CancellationToken cancellationToken = default` to `IPulsarPlugin.ExecuteAsync()` in `Core/Plugin/IPulsarPlugin.cs`
- [x] 1.2 Add `CancellationToken cancellationToken = default` to `IPluginExecutionPipeline.ExecuteAsync()` in `Core/Plugin/Runtime/PluginRuntimeKernel.cs`
- [x] 1.3 Add `CancellationToken cancellationToken = default` to `IPluginRuntimeKernel.ExecuteAsync()` in `Core/Plugin/Runtime/PluginRuntimeKernel.cs`
- [x] 1.4 Add `CancellationToken cancellationToken = default` to `IActionStrategy.ExecuteAsync()` in `ViewModels/Strategies/IActionStrategy.cs` (if exists), or to all strategy implementations directly

## 2. Pipeline Timeout Implementation

- [x] 2.1 In `PluginExecutionPipeline.ExecuteAsync()`, create a linked `CancellationTokenSource` with `CancelAfter(TimeSpan.FromSeconds(30))`
- [x] 2.2 Pass the linked token to `plugin.ExecuteAsync()` call
- [x] 2.3 Add `catch (OperationCanceledException)` clause that transitions plugin to `Faulted`, records breaker failure with `TimeoutException`, and returns `Blocked` outcome with `Critical` severity
- [x] 2.4 Update `PluginExecutionRequest` to include or accept the incoming cancellation token
- [x] 2.5 In `PluginRuntimeKernel.ExecuteAsync()`, accept and forward `CancellationToken` to `_pipeline.ExecuteAsync()`
- [x] 2.6 In `PluginRegistry.ExecuteAsync()` facade, accept and forward `CancellationToken` to `_runtimeKernel.ExecuteAsync()`

## 3. Plugin Implementations (All 6)

- [x] 3.1 Update `PkiPlugin.ExecuteAsync()` to accept `CancellationToken` parameter and forward to `_executionService.ExecuteAsync()` if supported
- [x] 3.2 Update `WinSwitcherPlugin.ExecuteAsync()` to accept `CancellationToken` parameter and forward where applicable
- [x] 3.3 Update `SystemCommandPlugin.ExecuteAsync()` to accept `CancellationToken` parameter
- [x] 3.4 Update `VbaRunnerPlugin.ExecuteAsync()` to accept `CancellationToken` parameter and forward to internal async operations
- [x] 3.5 Update `SimpleCommandPlugin.ExecuteAsync()` to accept `CancellationToken` parameter
- [x] 3.6 Update `BookmarkletRunnerPlugin.ExecuteAsync()` to accept `CancellationToken` parameter

## 4. Strategy Layer Propagation

- [x] 4.1 Update `SlotStrategies.cs` (all strategy `ExecuteAsync` methods) to accept and propagate `CancellationToken`
- [x] 4.2 Update `CreateProfileStrategy.ExecuteAsync()` to accept `CancellationToken` parameter
- [x] 4.3 Update `RadialMenuInputCoordinator.ExecuteSelectionAsync()` to create a cancellation token and pass it through the strategy chain
- [x] 4.4 Update `PluginBase<T>.ExecuteAsync()` abstract signature to include `CancellationToken` parameter

## 5. ConfigService Thread Safety

- [x] 5.1 Add `private readonly object _cacheLock = new();` to `ConfigService`
- [x] 5.2 In `SaveAsync()`, move `_cachedConfig = config;` to AFTER the successful file write (disk-first ordering)
- [x] 5.3 In `LoadInternalAsync()`, wrap the `_cachedConfig != null` read and `_cachedConfig = loaded` write in `lock (_cacheLock)`
- [x] 5.4 In `SaveAsync()`, wrap `_cachedConfig = config;` in `lock (_cacheLock)` after disk write

## 6. Dispose() Sync-Over-Async Fixes

- [x] 6.1 Replace `UnloadAsync().GetAwaiter().GetResult()` in `PluginHost.Dispose()` with `Task.Run(() => UnloadAsync()).ContinueWith(...)` fire-and-forget pattern with error logging
- [x] 6.2 Replace `SaveAsync().GetAwaiter().GetResult()` in `PluginUsageTracker.Dispose()` with `Task.Run(() => SaveAsync()).ContinueWith(...)` fire-and-forget pattern with error logging

## 7. Dead Code Cleanup

- [x] 7.1 Fix `PluginExecutionPipeline` line 387: `CanDisable ? Enabled : Enabled` → determine correct intent or simplify to `PluginLifecycleState.Enabled`
- [x] 7.2 Fix `PluginRuntimeKernel.ApplyProfileAsync()` line 665: `CanDisable ? Enabled : Enabled` → same fix as 7.1

## 8. Verification

- [x] 8.1 Run `dotnet build Pulsar/Pulsar/Pulsar.csproj` and ensure zero compilation errors
- [x] 8.2 Run `dotnet test Pulsar/Pulsar.Tests/Pulsar.Tests.csproj` and ensure all existing tests pass
- [x] 8.3 Write a unit test for `PluginExecutionPipeline` that verifies timeout triggers `Faulted` transition and breaker `RecordFailure` call
- [x] 8.4 Write a unit test for `ConfigService` that verifies cache is not updated when disk write fails
- [ ] 8.5 Manual smoke test: invoke each plugin type (PKI, WinSwitcher, SimpleCommand) and verify normal execution is unaffected
