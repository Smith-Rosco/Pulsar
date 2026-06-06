## 1. Phase 0: Safety Net (no behavior change)

- [x] 1.1 Add `Interlocked.CompareExchange` guard in `RadialMenuViewModel.Show()` — replace `private bool _isLoading` with `private int _isLoading` (0=idle, 1=loading) using atomic compare-exchange
- [x] 1.2 Add `Debug.Assert(Application.Current.Dispatcher.CheckAccess())` at top of `Show()` as defense-in-depth
- [x] 1.3 Add `ConfigureAwait(false)` to `PluginExecutionPipeline.ExecuteAsync()` internal awaits (library code that doesn't need UI context)
- [x] 1.4 Run `dotnet build` and `dotnet test` to confirm zero regression

## 2. Phase 1: Thread-Safe Runtime Dictionaries

- [x] 2.1 Replace `Dictionary<string, IPulsarPlugin>` with `ConcurrentDictionary<string, IPulsarPlugin>` in `PluginRuntimeStateStore`
- [x] 2.2 Replace `Dictionary<string, PluginRuntimeSnapshot>` with `ConcurrentDictionary<string, PluginRuntimeSnapshot>` in `PluginRuntimeStateStore`
- [x] 2.3 Replace `Dictionary<string, int>` with `ConcurrentDictionary<string, int>` in `PluginCircuitBreakerPolicy`
- [x] 2.4 Replace `Dictionary<string, DateTime>` with `ConcurrentDictionary<string, DateTime>` in `PluginCircuitBreakerPolicy`
- [x] 2.5 Update `RecordFailure()` to use `AddOrUpdate()` for atomic increment instead of `_failureCounts[pluginId]++`
- [x] 2.6 Update `RecordSuccess()` to use `TryRemove()` instead of `Remove()`
- [x] 2.7 Change `IPluginCatalog.Descriptors` and `IPluginRuntimeStateStore.Plugins` return type from `IDictionary` to `IReadOnlyDictionary`
- [x] 2.8 Update all internal callers of `_plugins.Values` and `_descriptors.Values` to snapshot with `.ToList()` or use `ConcurrentDictionary.Values` directly
- [x] 2.9 Run `dotnet test --filter "PluginRuntimeKernelTests"` to verify state store correctness

## 3. Phase 2: Hook Thread → UI Dispatcher Boundary

- [x] 3.1 In `HotkeyService.CheckAndExecute()`, wrap `item.Action.Invoke()` with `Application.Current.Dispatcher.InvokeAsync(() => item.Action.Invoke())`
- [x] 3.2 Apply same dispatch pattern to `GlobalMouseService` mouse event handlers if they also run on hook thread
- [x] 3.3 Verify `RadialMenuViewModel.Show()` now always runs on UI thread — remove any stale `CheckAccess()` patterns that become redundant
- [ ] 3.4 Manual smoke test: rapidly press Ctrl+Q 10 times, verify no double-menu, no crash, no "hook unresponsive" behavior

## 4. Phase 3: PulsarContext Immutability

- [x] 4.1 Remove `CurrentPluginId` property and `PermissionInterceptor` property from `PulsarContext` (lines 29, 34)
- [x] 4.2 Add `CurrentPluginId` and `PermissionInterceptor` to `PluginExecutionContext` — set via `BeginScope()` parameters
- [x] 4.3 Update `PulsarContext.CheckPermission()` to read from `PluginExecutionContext.Current` instead of `this.CurrentPluginId` / `this.PermissionInterceptor`
- [x] 4.4 Update `PluginExecutionPipeline.ExecuteAsync()` to pass `permissionInterceptor` to `PluginExecutionContext.BeginScope()`
- [x] 4.5 Update `PluginRegistryV2.ExecuteAsync()` (before deletion in Phase 6) to use `PluginExecutionContext` instead of `context.CurrentPluginId = ...`
- [x] 4.6 Run `dotnet test --filter "PulsarContextTests"` to verify context immutability

## 5. Phase 4: DI Composition of Plugin Runtime

- [x] 5.1 Create `Services/PluginRuntimeServiceCollectionExtensions.cs` with `AddPluginRuntime()` extension method
- [x] 5.2 Register `PluginCatalog` as `IPluginCatalog` singleton
- [x] 5.3 Register `PluginRuntimeStateStore` as `IPluginRuntimeStateStore` singleton
- [x] 5.4 Register `PluginCircuitBreakerPolicy` as `IPluginBreakerPolicy` singleton
- [x] 5.5 Register `PluginExecutionPipeline` as `IPluginExecutionPipeline` singleton
- [x] 5.6 Register `PluginLoader` as singleton (factory: needs plugin directory path from config)
- [x] 5.7 Register `PluginRuntimeKernel` as `IPluginRuntimeKernel` singleton
- [x] 5.8 Register `PluginRegistry` as `IPluginRegistry` singleton with constructor injection of all above
- [x] 5.9 Update `App.xaml.cs` to call `serviceCollection.AddPluginRuntime()` and change `PluginRegistry` registration to `IPluginRegistry`
- [x] 5.10 Update `CommandPageProvider` and `PluginActionStrategy` to accept `IPluginRegistry` instead of `PluginRegistry`
- [x] 5.11 Update `AppStartupCoordinator` to use `IPluginRegistry` interface
- [x] 5.12 Run `dotnet build` — fix any DI resolution errors

## 6. Phase 5: Async Hygiene

- [x] 6.1 Delete `WindowService.ActivateWindowDetailed()` sync wrapper (line 842) and `ActivateWindow()` sync wrapper (line 847)
- [x] 6.2 Update `WindowService.SwitchToProcessAsync()` to await `ActivateWindowDetailedAsync()` directly (already inside `Task.Run`)
- [x] 6.3 Replace `_ = executeSelectionAsync()` discard in `RadialMenuInputCoordinator.HandleModifierRelease()` with awaited try/catch wrapper
- [x] 6.4 Replace `_ = Task.Run(async () => await _configService.SaveAsync(config))` fire-and-forget in `ConfigService` with `IBackgroundWorkScheduler.Enqueue()`
- [x] 6.5 Audit remaining `async void` methods: convert service callbacks (not WPF event handlers) to `async Task` where possible
- [x] 6.6 Verify `OnExit` sync-over-async (`Task.Run(...).GetAwaiter().GetResult()`) is intentional and safe (it is — shutdown must block)
- [x] 6.7 Run `dotnet test` — verify no deadlocks introduced

## 7. Phase 6: Dead Code Removal

- [x] 7.1 Extract `PermissionInterceptor` logic from `PluginRegistryV2` if not already done in Phase 4
- [x] 7.2 Delete `Services/PluginRegistryV2.cs` (568 lines)
- [x] 7.3 Delete `Services/PluginRepository.cs` (if not already deleted by `architecture-hygiene-pass`)
- [x] 7.4 Delete `Core/Converters/LegacySlotConverter.cs` (if not already deleted by `architecture-hygiene-pass`)
- [x] 7.5 Remove any remaining `#pragma warning disable CS0618` suppressions referencing deleted types
- [x] 7.6 Run `dotnet build` — verify no compilation errors from deleted types

## 8. Phase 7: Validation

- [x] 8.1 Run full test suite: `dotnet test`
- [x] 8.2 Run simulator: `dotnet run --project Pulsar/Pulsar.Simulator/Pulsar.Simulator.csproj`
- [ ] 8.3 Manual QA checklist: hotkey invocation, quick-switch (<250ms), slow-switch (>250ms), plugin execution (PKI, WinSwitcher, BasicCommand), circuit breaker trip (force 3 crashes), settings window open/close
- [x] 8.4 Update `AGENTS.md` if any architectural invariants changed
- [x] 8.5 Update `ARCHITECTURE.md` sections 2.2 (Runtime Kernel) and 3.1 (PulsarContext) to reflect new DI composition and immutability
