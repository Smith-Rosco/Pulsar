## Context

Pulsar's execution pipeline has no enforced thread boundary between the Windows keyboard hook thread (`WH_KEYBOARD_LL` callback), the WPF UI dispatcher, and the plugin runtime. Three coordinated issues exist:

1. **Hook thread leaks into UI code**: `GlobalKeyboardHook.HookCallback()` runs on an OS-managed hook thread. The registered action delegate (`() => Show(mode)`) is invoked synchronously on that thread, and `Show()` directly accesses WPF-bound properties, calls `Process.GetProcessById()`, and awaits async operations — all on the hook thread.

2. **None of the plugin runtime dictionaries are thread-safe**: `PluginRuntimeStateStore._plugins`, `_snapshots`, `PluginCircuitBreakerPolicy._failureCounts`, and `_brokenCircuits` are plain `Dictionary<K,V>`. While concurrent plugin execution does not currently occur, the `PluginExecutionPipeline.ExecuteAsync()` method signature is `async Task` (inviting concurrency), and the `PluginRuntimeStateStore` API exposes the raw `_plugins` dictionary directly via `IDictionary<string, IPulsarPlugin> Plugins`.

3. **`PulsarContext` violates its own immutability contract**: The architecture document states "Context is read-only, preventing plugin side effects", but `CurrentPluginId` and `PermissionInterceptor` are mutated on every execution via `internal set`.

The existing `architecture-hygiene-pass` change handles DI module extraction and dead code removal. This change is complementary — it handles thread safety and async correctness without overlapping scope.

### Current execution flow (problematic)

```
┌─────────────────────────────────────────────────────────────────┐
│  Windows WH_KEYBOARD_LL hook thread (no dispatcher)              │
│    └─ HookCallback() → OnKeyDown() → CheckAndExecute()           │
│         └─ item.Action.Invoke()  ← SYNCHRONOUS on hook thread    │
│              └─ Show(mode)  ← async void, no marshal, no guard   │
│                   ├─ PulsarContext.Capture()  ← blocks hook       │
│                   ├─ IsVisible = true  ← WPF DP from non-UI      │
│                   └─ await LoadAsync() ← async on hook thread    │
└─────────────────────────────────────────────────────────────────┘
```

## Goals / Non-Goals

**Goals:**
- Guarantee that no plugin runtime or UI code executes on the keyboard hook thread
- Make all shared-mutable dictionaries thread-safe via `ConcurrentDictionary`
- Make `PulsarContext` fully immutable after construction (match documented contract)
- Eliminate all `.GetAwaiter().GetResult()` sync-over-async patterns
- Audit fire-and-forget patterns for proper exception handling
- Compose plugin runtime components through DI instead of manual `new`
- Delete `PluginRegistryV2` (dead code)

**Non-Goals:**
- Enable true concurrent plugin execution (still sequential; just making the data structures safe)
- Rewrite `RadialMenuViewModel` (only thread-safety fixes)
- Change plugin contract (`IPulsarPlugin`, `PluginBase<T>`)
- Introduce new third-party dependencies
- Extract DI modules into extension methods (done by `architecture-hygiene-pass`)

## Decisions

### Decision 1: Dispatch at the HotkeyService boundary

**Chosen**: Insert `Dispatcher.InvokeAsync()` in `HotkeyService.CheckAndExecute()` at the `item.Action.Invoke()` call site.

```csharp
// HotkeyService.cs, CheckAndExecute()
// Before:
item.Action.Invoke();

// After:
Application.Current.Dispatcher.InvokeAsync(() => item.Action.Invoke());
```

**Rationale**: This is the single choke point where ALL hotkey actions transition from hook thread to application code. Placing the dispatch here means:
- No individual handler needs to remember to dispatch
- New hotkey actions registered via `HotkeyService.RegisterAction()` are automatically safe
- `GlobalKeyboardHook` and `GlobalMouseHook` remain pure native interop wrappers (no WPF dependency)

**Alternatives considered**:
- *Dispatch at `Show()` entry*: Fragile — relies on every handler remembering to add `CheckAccess()`. Future hotkey handlers could miss it.
- *Dispatch at `HookCallback()` via `BeginInvoke`*: Forces ALL hook processing to the UI thread, including hotkey detection logic. Unnecessary overhead for key state tracking.
- *`IHotkeyActionBroker` abstraction*: Over-engineering. The `Dispatcher` IS the action broker for a WPF application. Adding an interface here creates indirection without value until there's a non-WPF head.

**⚠️ Caveat**: `InvokeAsync()` is asynchronous (returns `DispatcherOperation`). The hotkey action no longer blocks the hook. This is intentional — the hook must be non-blocking per Windows contract. Existing callers that relied on synchronous side effects (e.g., `Show()` reading `_pendingQuickSwitch` immediately after invocation) already use async patterns and are unaffected.

### Decision 2: ConcurrentDictionary for all runtime state

**Chosen**: Replace all four `Dictionary<K,V>` fields with `ConcurrentDictionary<K,V>`.

| Class | Field | New Type |
|-------|-------|----------|
| `PluginRuntimeStateStore` | `_plugins` | `ConcurrentDictionary<string, IPulsarPlugin>` |
| `PluginRuntimeStateStore` | `_snapshots` | `ConcurrentDictionary<string, PluginRuntimeSnapshot>` |
| `PluginCircuitBreakerPolicy` | `_failureCounts` | `ConcurrentDictionary<string, int>` |
| `PluginCircuitBreakerPolicy` | `_brokenCircuits` | `ConcurrentDictionary<string, DateTime>` |

**Rationale**: `ConcurrentDictionary` provides lock-free reads and fine-grained write locking. All four dictionaries are accessed from async execution paths. The performance overhead is negligible (the dictionaries store at most dozens of entries — Pulsar has ~6 plugins).

**Atomic operations**: `RecordFailure()` currently does `_failureCounts[pluginId]++` (non-atomic). Changed to `_failureCounts.AddOrUpdate(pluginId, 1, (_, count) => count + 1)`. `RecordSuccess()` calls `_failureCounts.TryRemove(pluginId, out _)`.

**`IDictionary` exposure**: `IPluginCatalog.Descriptors` and `IPluginRuntimeStateStore.Plugins` currently expose `IDictionary<K,V>`. Changed to expose `IReadOnlyDictionary<K,V>` via `ConcurrentDictionary`'s `Keys` and indexer, preventing external mutation.

### Decision 3: Interlocked guard for Show()

**Chosen**: Replace `_isLoading` (bool) with `_isLoading` (int) using `Interlocked.CompareExchange`.

```csharp
// Before:
private bool _isLoading;
if (IsVisible || _isLoading) return;
_isLoading = true;
try { ... }
finally { _isLoading = false; }

// After:
private int _isLoading; // 0 = idle, 1 = loading
if (IsVisible || Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0) return;
try { ... }
finally { Interlocked.Exchange(ref _isLoading, 0); }
```

**Rationale**: `CompareExchange` provides a lock-free atomic test-and-set. No allocation, no kernel transition. The hook thread already checks `IsVisible` before entering — the Interlocked guard prevents the N+1 case where hook thread #2 enters between hook thread #1's `_isLoading = true` and its first `await` suspension point.

### Decision 4: Move mutable state from PulsarContext to PluginExecutionContext

**Chosen**: Remove `CurrentPluginId` and `PermissionInterceptor` from `PulsarContext`. Add them to `PluginExecutionContext`.

```csharp
// PluginExecutionContext.cs gains:
public string? CurrentPluginId { get; }
public PermissionInterceptor? PermissionInterceptor { get; }

public static PluginExecutionContext Begin(
    string pluginId, string action, string? targetProcessName,
    PermissionInterceptor? permissionInterceptor = null)
{
    // Store in AsyncLocal as before, plus new fields
}
```

**Rationale**: `PluginExecutionContext` already exists as an `AsyncLocal`-based scope for logging enrichment. It is the correct home for per-execution data — it's already scoped to a single `ExecuteAsync` call, already disposed at the end, and already propagated through the async call chain. Adding two fields here is a natural extension.

`PulsarContext` becomes truly immutable after its private constructor: all fields are `{ get; }` or `Lazy<Task<...>>` with no setters (not even `internal`).

**Breaking change**: Plugins that accessed `context.CurrentPluginId` or `context.PermissionInterceptor` will need to use `PluginExecutionContext.Current.CurrentPluginId` instead. Audit shows zero plugin code accesses these fields directly (they're only set/read by the execution pipeline itself).

### Decision 5: DI composition via AddPluginRuntime() extension

**Chosen**: Register all previously-manually-composed components as DI singletons through an extension method.

```csharp
// App.xaml.cs, ConfigureServices:
serviceCollection.AddPluginRuntime(options =>
{
    options.PluginDirectory = Path.Combine(baseDir, "Plugins");
});

// New file: Services/PluginRuntimeServiceCollectionExtensions.cs
public static IServiceCollection AddPluginRuntime(
    this IServiceCollection services, Action<PluginRuntimeOptions> configure)
{
    services.AddSingleton<IPluginCatalog, PluginCatalog>();
    services.AddSingleton<IPluginRuntimeStateStore, PluginRuntimeStateStore>();
    services.AddSingleton<IPluginBreakerPolicy, PluginCircuitBreakerPolicy>();
    services.AddSingleton<IPluginExecutionPipeline, PluginExecutionPipeline>();
    services.AddSingleton<PluginLoader>(sp => { ... });
    services.AddSingleton<IPluginRuntimeKernel, PluginRuntimeKernel>();
    services.AddSingleton<IPluginRegistry, PluginRegistry>();
    return services;
}
```

**Rationale**: Each component becomes independently mockable in tests. The `PluginRegistry` constructor simplifies from 30 lines of manual `new` to 3-4 constructor-injected parameters. This makes `PluginRegistry` a true facade, matching the architecture document's description.

**Alternatives considered**:
- *Factory pattern via `IPluginRuntimeKernelFactory`*: Overkill. These are application-global singletons with simple constructors. A factory adds an extra interface per component.
- *Keep current manual composition*: Prevents testability. Every test that needs a breaker policy must manually wire up the entire pipeline.

### Decision 6: Sync wrapper removal strategy

**Chosen**: Delete `ActivateWindowDetailed(ProcessWindowInfo)` and `ActivateWindow(ProcessWindowInfo)` synchronous wrappers. Update the single caller (`WindowService.SwitchToProcessAsync` line 378) to use the async version directly.

```csharp
// WindowService.SwitchToProcessAsync, already inside Task.Run:
// Before:
if (!ActivateWindow(targetWindow)) ...

// After:
var result = await ActivateWindowDetailedAsync(targetWindow);
if (!result.Success) ...
```

**Rationale**: `SwitchToProcessAsync` already runs on a thread-pool thread via `Task.Run()`, so calling async from it is safe. The wrappers existed solely to avoid the `await` keyword inside the lambda — removing them simplifies the API and eliminates two deadlock-prone methods.

For the remaining `.GetAwaiter().GetResult()` calls in `App.xaml.cs` (`OnExit`), they are intentionally blocking (shutdown must complete synchronously) and already wrapped in `Task.Run()` to avoid deadlock. No change needed.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| `Dispatcher.InvokeAsync()` in hotkey path adds ~1ms latency to menu appearance | Quick-switch threshold is 250ms. 1ms is 0.4% of the budget. Unnoticeable. |
| `Interlocked.CompareExchange` on `_isLoading` could theoretically starve under extreme rapid-fire (user presses hotkey 1000x/sec) | Human fingers max out at ~10 presses/sec. The guard is per-attempt, not queue-based, so no starvation. |
| `PluginRuntimeStateStore.IDictionary` → `IReadOnlyDictionary` is a source breaking change | Only `PluginRuntimeKernel` and `PluginRegistry` access these directly. Both are updated in the same change. |
| Deleting `PluginRegistryV2` loses the `PermissionInterceptor` integration | `PermissionInterceptor` is extracted into the active pipeline before deletion (see Decision 4). |
| `ConcurrentDictionary` has different enumeration semantics (snapshot vs live) | `GetAllPlugins()` and `GetAll()` already enumerate with `.ToList()` or return `.Values` which is a snapshot in ConcurrentDictionary. |

## Open Questions

1. **Should we add a `Debug.Assert(CheckAccess())` in `Show()` as a defense-in-depth?** The dispatch in `HotkeyService` guarantees it, but an assertion catches future code paths that call `Show()` directly. *Leaning: yes.*

2. **Should `PluginCircuitBreakerPolicy` be split into a separate service (as DI already allows) or kept as a single class?** Currently it handles both the breaker logic and user notification (via `ITrayService`). The notification is a side effect that could be separated. *Leaning: keep as-is for now; separate if testing pain emerges.*

3. **Does `BackgroundWorkScheduler` guarantee serial execution?** The `_ = executeSelectionAsync()` in `RadialMenuInputCoordinator` is currently the only fire-and-forget that touches plugin state. Moving it to `BackgroundWorkScheduler` requires confirming the scheduler's serialization contract first.
