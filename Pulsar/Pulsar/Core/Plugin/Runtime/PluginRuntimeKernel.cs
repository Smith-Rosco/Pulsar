using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.Core.Plugin.Runtime
{
    public enum PluginLifecycleState
    {
        Unloaded = 0,
        Loaded = 1,
        Enabled = 2,
        Disabled = 3,
        Running = 4,
        Faulted = 5,
        Recovering = 6
    }

    public enum PluginExecutionOutcomeKind
    {
        Success = 0,
        HandledFailure = 1,
        Exception = 2,
        Blocked = 3
    }

    public sealed class PluginRuntimeSnapshot
    {
        public required string PluginId { get; init; }

        public PluginLifecycleState State { get; init; }

        public Exception? LastError { get; init; }

        public DateTime? LoadedAtUtc { get; init; }

        public DateTime? UnloadedAtUtc { get; init; }
    }

    public readonly struct PluginExecutionOutcome
    {
        public PluginExecutionOutcome(PluginResult result, PluginExecutionOutcomeKind kind)
        {
            Result = result;
            Kind = kind;
        }

        public PluginResult Result { get; }

        public PluginExecutionOutcomeKind Kind { get; }

        public bool IsTelemetrySuccess => Kind == PluginExecutionOutcomeKind.Success;
    }

    public class PluginCatalog
    {
        private readonly Dictionary<string, PluginDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, PluginDescriptor> Descriptors => _descriptors;

        public IEnumerable<PluginDescriptor> GetAll()
        {
            return _descriptors.Values;
        }

        public void RegisterDescriptors(IEnumerable<PluginDescriptor> descriptors)
        {
            foreach (var descriptor in descriptors)
            {
                _descriptors.TryAdd(descriptor.Id, descriptor);
            }
        }

        public bool TryGetDescriptor(string pluginId, out PluginDescriptor? descriptor)
        {
            return _descriptors.TryGetValue(pluginId, out descriptor);
        }

        /// <summary>
        /// Drops a descriptor from the catalog when a plugin is uninstalled at
        /// runtime. Stale descriptors would otherwise keep referencing types
        /// from an unloaded plugin assembly.
        /// </summary>
        public bool RemoveDescriptor(string pluginId)
        {
            return _descriptors.Remove(pluginId);
        }
    }

    public class PluginRuntimeStateStore
    {
        private readonly ConcurrentDictionary<string, IPulsarPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, PluginRuntimeSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, IPulsarPlugin> Plugins => _plugins;

        public PluginLifecycleState GetState(string pluginId)
        {
            return GetSnapshot(pluginId).State;
        }

        public PluginRuntimeSnapshot GetSnapshot(string pluginId)
        {
            if (_snapshots.TryGetValue(pluginId, out var snapshot))
            {
                return snapshot;
            }

            snapshot = new PluginRuntimeSnapshot
            {
                PluginId = pluginId,
                State = _plugins.ContainsKey(pluginId) ? PluginLifecycleState.Loaded : PluginLifecycleState.Unloaded
            };

            _snapshots.TryAdd(pluginId, snapshot);
            return snapshot;
        }

        public void SetPlugin(IPulsarPlugin plugin, PluginLifecycleState state)
        {
            _plugins[plugin.Id] = plugin;
            Transition(plugin.Id, state);
        }

        public void Transition(string pluginId, PluginLifecycleState state, Exception? error = null)
        {
            var snapshot = GetSnapshot(pluginId);

            if (!IsValidTransition(snapshot.State, state))
            {
                throw new InvalidOperationException(
                    $"Invalid plugin lifecycle transition for '{pluginId}': {snapshot.State} -> {state}.");
            }

            _snapshots[pluginId] = new PluginRuntimeSnapshot
            {
                PluginId = pluginId,
                State = state,
                LastError = error,
                LoadedAtUtc = snapshot.LoadedAtUtc ?? (state is PluginLifecycleState.Loaded or PluginLifecycleState.Enabled or PluginLifecycleState.Disabled or PluginLifecycleState.Running or PluginLifecycleState.Recovering or PluginLifecycleState.Faulted ? DateTime.UtcNow : null),
                UnloadedAtUtc = state == PluginLifecycleState.Unloaded ? DateTime.UtcNow : snapshot.UnloadedAtUtc
            };
        }

        private static bool IsValidTransition(PluginLifecycleState current, PluginLifecycleState next)
        {
            // Idempotent re-entry into the same state is always allowed.
            if (current == next)
            {
                return true;
            }

            return next switch
            {
                // Terminal / recovery states can be reached from any prior state.
                PluginLifecycleState.Faulted or PluginLifecycleState.Unloaded => true,

                PluginLifecycleState.Loaded => current is PluginLifecycleState.Unloaded or PluginLifecycleState.Faulted,
                PluginLifecycleState.Enabled => current is PluginLifecycleState.Unloaded or PluginLifecycleState.Loaded or PluginLifecycleState.Disabled or PluginLifecycleState.Running or PluginLifecycleState.Faulted or PluginLifecycleState.Recovering,
                PluginLifecycleState.Disabled => current is PluginLifecycleState.Loaded or PluginLifecycleState.Enabled or PluginLifecycleState.Running,
                PluginLifecycleState.Running => current is PluginLifecycleState.Enabled or PluginLifecycleState.Faulted or PluginLifecycleState.Recovering,
                PluginLifecycleState.Recovering => current is PluginLifecycleState.Faulted,
                _ => false
            };
        }

        public bool TryGetPlugin(string pluginId, out IPulsarPlugin? plugin)
        {
            return _plugins.TryGetValue(pluginId, out plugin);
        }

        public void RemovePlugin(string pluginId)
        {
            _plugins.TryRemove(pluginId, out _);
            Transition(pluginId, PluginLifecycleState.Unloaded);
        }
    }

    public readonly struct PluginBreakerAvailability
    {
        public PluginBreakerAvailability(bool allowed, string? message = null, bool recovered = false)
        {
            Allowed = allowed;
            Message = message;
            Recovered = recovered;
        }

        public bool Allowed { get; }

        public string? Message { get; }

        public bool Recovered { get; }
    }

    /// <summary>
    /// Payload for <see cref="PluginCircuitBreakerPolicy.Tripped"/> — identifies the
    /// plugin whose circuit opened and the cooldown the breaker applied.
    /// </summary>
    public sealed class PluginBreakerTrippedEventArgs : EventArgs
    {
        public PluginBreakerTrippedEventArgs(string pluginId, TimeSpan cooldown)
        {
            PluginId = pluginId;
            Cooldown = cooldown;
        }

        public string PluginId { get; }

        /// <summary>Duration the circuit stays open before it may retry.</summary>
        public TimeSpan Cooldown { get; }
    }

    /// <summary>
    /// Payload for <see cref="PluginCircuitBreakerPolicy.Recovered"/> — identifies the
    /// plugin whose cooldown expired and moved back to half-open.
    /// </summary>
    public sealed class PluginBreakerRecoveredEventArgs : EventArgs
    {
        public PluginBreakerRecoveredEventArgs(string pluginId)
        {
            PluginId = pluginId;
        }

        public string PluginId { get; }
    }

    public class PluginCircuitBreakerPolicy
    {
        private const int MaxFailures = 3;
        private static readonly TimeSpan ResetTimeout = TimeSpan.FromMinutes(1);

        // Sliding-window failure timestamps. ADR-002 defines the breaker as
        // "3 crashes within 1 minute"; storing timestamps makes that window real
        // instead of counting failures indefinitely.
        private readonly ConcurrentDictionary<string, List<DateTime>> _recentFailures = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, DateTime> _brokenCircuits = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<PluginCircuitBreakerPolicy> _logger;

        /// <summary>
        /// Raised when the breaker opens a circuit (3 failures within 1 minute).
        /// The policy is a pure state machine: it only announces the transition.
        /// Side effects (health telemetry, tray notifications) are owned by
        /// <see cref="Pulsar.Services.PluginBreakerNotificationService"/>, which subscribes
        /// to this event (ADR-013).
        /// </summary>
        public event EventHandler<PluginBreakerTrippedEventArgs>? Tripped;

        /// <summary>
        /// Raised when a cooldown expires and the circuit moves to half-open.
        /// See <see cref="Tripped"/> for the observation-seam rationale (ADR-013).
        /// </summary>
        public event EventHandler<PluginBreakerRecoveredEventArgs>? Recovered;

        public PluginCircuitBreakerPolicy(ILogger<PluginCircuitBreakerPolicy>? logger = null)
        {
            _logger = logger ?? NullLogger<PluginCircuitBreakerPolicy>.Instance;
        }

        public PluginBreakerAvailability CheckAvailability(PluginDescriptor descriptor, string pluginId)
        {
            if (descriptor.Tier != PluginTier.Extension)
            {
                return new PluginBreakerAvailability(true);
            }

            if (!_brokenCircuits.TryGetValue(pluginId, out var breakTime))
            {
                return new PluginBreakerAvailability(true);
            }

            var elapsed = DateTime.UtcNow - breakTime;
            if (elapsed < ResetTimeout)
            {
                var remaining = (int)(ResetTimeout - elapsed).TotalSeconds;
                _logger.LogWarning("Circuit Open: {PluginId} is disabled for {Remaining}s", pluginId, remaining);
                return new PluginBreakerAvailability(false, $"Plugin disabled for safety. Try again in {remaining}s.");
            }

            _brokenCircuits.TryRemove(pluginId, out _);
            _logger.LogInformation("Circuit Half-Open: Retrying {PluginId}...", pluginId);
            Recovered?.Invoke(this, new PluginBreakerRecoveredEventArgs(pluginId));
            return new PluginBreakerAvailability(true, recovered: true);
        }

        public void RecordSuccess(PluginDescriptor descriptor, string pluginId)
        {
            if (descriptor.Tier != PluginTier.Extension)
            {
                return;
            }

            _recentFailures.TryRemove(pluginId, out _);
        }

        public void RecordFailure(PluginDescriptor descriptor, string pluginId, Exception ex)
        {
            if (descriptor.Tier != PluginTier.Extension)
            {
                return;
            }

            var failures = _recentFailures.GetOrAdd(pluginId, _ => new List<DateTime>());
            int count;

            lock (failures)
            {
                var cutoff = DateTime.UtcNow - ResetTimeout;
                failures.RemoveAll(timestamp => timestamp < cutoff);
                failures.Add(DateTime.UtcNow);
                count = failures.Count;
            }

            _logger.LogWarning(ex, "Plugin crashed ({Count}/{MaxFailures})", count, MaxFailures);

            if (count < MaxFailures)
            {
                return;
            }

            _brokenCircuits[pluginId] = DateTime.UtcNow;
            _recentFailures.TryRemove(pluginId, out _);
            _logger.LogCritical("Circuit Breaker Tripped! Plugin temporarily disabled for {Timeout}s", ResetTimeout.TotalSeconds);
            Tripped?.Invoke(this, new PluginBreakerTrippedEventArgs(pluginId, ResetTimeout));
        }
    }

    public sealed class PluginExecutionRequest
    {
        public required PluginDescriptor Descriptor { get; init; }

        public required string Action { get; init; }

        public required IReadOnlyDictionary<string, string> Args { get; init; }

        public required PulsarContext Context { get; init; }

        /// <summary>
        /// Permissions granted to the plugin in Profiles.json. The pipeline passes
        /// this to <see cref="IPluginPermissionService"/> before execution.
        /// </summary>
        public IReadOnlyCollection<string> GrantedPermissions { get; init; } = Array.Empty<string>();

        public required Func<bool> IsEnabled { get; init; }

        public required Func<Task<IPulsarPlugin?>> ActivateAsync { get; init; }

        public CancellationToken CancellationToken { get; init; }
    }

    /// <summary>
    /// Decides what happens when a Core plugin (tier = Core) fails. Core plugin
    /// failures are fatal by architecture: the default policy rethrows the
    /// original exception. The application shell may register an alternative
    /// policy that shuts the process down after emergency cleanup.
    /// </summary>
    public interface ICorePluginFailureHandler
    {
        PluginExecutionOutcome Handle(PluginDescriptor descriptor, Exception exception);
    }

    public sealed class RethrowCorePluginFailureHandler : ICorePluginFailureHandler
    {
        public static readonly RethrowCorePluginFailureHandler Instance = new();

        public PluginExecutionOutcome Handle(PluginDescriptor descriptor, Exception exception)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
            throw new InvalidOperationException("Unreachable.", exception);
        }
    }

    public class PluginExecutionPipeline
    {
        private readonly PluginRuntimeStateStore _runtimeStateStore;
        private readonly PluginCircuitBreakerPolicy _breakerPolicy;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly ILogger<PluginExecutionPipeline> _logger;
        private readonly ICorePluginFailureHandler _coreFailureHandler;
        private readonly IPluginPermissionService _permissionService;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _executionLocks = new(StringComparer.OrdinalIgnoreCase);

        public PluginExecutionPipeline(
            PluginRuntimeStateStore runtimeStateStore,
            PluginCircuitBreakerPolicy breakerPolicy,
            ILogger<PluginExecutionPipeline>? logger = null,
            IPluginUsageTracker? usageTracker = null,
            IPluginHealthMonitor? healthMonitor = null,
            ICorePluginFailureHandler? coreFailureHandler = null,
            TimeSpan? executionTimeout = null,
            IPluginPermissionService? permissionService = null)
        {
            _runtimeStateStore = runtimeStateStore;
            _breakerPolicy = breakerPolicy;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logger = logger ?? NullLogger<PluginExecutionPipeline>.Instance;
            _coreFailureHandler = coreFailureHandler ?? RethrowCorePluginFailureHandler.Instance;
            ExecutionTimeout = executionTimeout ?? TimeSpan.FromSeconds(30);
            _permissionService = permissionService ?? new PluginPermissionService();
        }

        /// <summary>
        /// Maximum wall-clock duration of a single plugin action. The value is
        /// exposed as init-only so timeout policy tests can run without waiting
        /// 30 seconds while production keeps the original budget.
        /// </summary>
        public TimeSpan ExecutionTimeout { get; init; }

        public async Task<PluginExecutionOutcome> ExecuteAsync(PluginExecutionRequest request, CancellationToken cancellationToken = default)
        {
            var pluginId = request.Descriptor.Id;

            // Default execution policy: one action per plugin at a time. A plugin
            // that needs reentrancy must be refactored to use a background queue,
            // not concurrent mutation of its internal state.
            var executionLock = _executionLocks.GetOrAdd(pluginId, static _ => new SemaphoreSlim(1, 1));
            if (!await executionLock.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogWarning("Plugin is already executing: {PluginId}", pluginId);
                return new PluginExecutionOutcome(
                    PluginResult.Error($"Plugin is already executing: {pluginId}", PluginErrorSeverity.Recoverable, PluginErrorCode.TemporaryUnavailable),
                    PluginExecutionOutcomeKind.Blocked);
            }

            try
            {
                return await ExecuteCoreAsync(request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                executionLock.Release();
            }
        }

        private async Task<PluginExecutionOutcome> ExecuteCoreAsync(PluginExecutionRequest request, CancellationToken cancellationToken = default)
        {
            var pluginId = request.Descriptor.Id;

            if (request.Descriptor.Tier == PluginTier.Extension && !request.IsEnabled())
            {
                _logger.LogWarning("Plugin is disabled by user: {PluginId}", pluginId);
                return new PluginExecutionOutcome(PluginResult.Error("Plugin is disabled."), PluginExecutionOutcomeKind.Blocked);
            }

            var permissionEvaluation = _permissionService.Evaluate(
                request.Descriptor,
                request.GrantedPermissions);
            if (!permissionEvaluation.Granted)
            {
                var details = permissionEvaluation.MissingPermissions.Count > 0
                    ? string.Join(", ", permissionEvaluation.MissingPermissions)
                    : string.Join(", ", permissionEvaluation.UnknownPermissions);

                _logger.LogWarning(
                    "Plugin execution blocked by permissions: {PluginId}. Missing=[{Missing}] Unknown=[{Unknown}]",
                    pluginId,
                    string.Join(", ", permissionEvaluation.MissingPermissions),
                    string.Join(", ", permissionEvaluation.UnknownPermissions));

                return new PluginExecutionOutcome(
                    PluginResult.Error(
                        $"Plugin execution blocked by permissions: {details}",
                        PluginErrorSeverity.Recoverable,
                        PluginErrorCode.AccessDenied),
                    PluginExecutionOutcomeKind.Blocked);
            }

            var availability = _breakerPolicy.CheckAvailability(request.Descriptor, pluginId);
            if (!availability.Allowed)
            {
                return new PluginExecutionOutcome(PluginResult.Error(availability.Message ?? "Plugin unavailable."), PluginExecutionOutcomeKind.Blocked);
            }

            if (availability.Recovered)
            {
                _runtimeStateStore.Transition(pluginId, PluginLifecycleState.Recovering);
            }

            var plugin = await request.ActivateAsync().ConfigureAwait(false);
            if (plugin == null)
            {
                _logger.LogError("Plugin activation failed or plugin unavailable: {PluginId}", pluginId);
                return new PluginExecutionOutcome(PluginResult.Error($"Plugin unavailable: {pluginId}"), PluginExecutionOutcomeKind.Blocked);
            }

            IPluginPermissionInterceptor permissionInterceptor = request.Descriptor.IsExternal
                ? new GrantedPluginPermissionInterceptor(request.GrantedPermissions)
                : AllowAllPluginPermissionInterceptor.Instance;

            using var executionScope = PluginExecutionContext.BeginScope(
                pluginId,
                request.Action,
                targetProcessName: request.Context.TargetProcessName,
                permissionInterceptor: permissionInterceptor);

            var stopwatch = Stopwatch.StartNew();
            var readyState = PluginLifecycleState.Enabled;
            _runtimeStateStore.Transition(pluginId, PluginLifecycleState.Running);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, request.CancellationToken);
            linkedCts.CancelAfter(ExecutionTimeout);

            try
            {
                var result = await plugin.ExecuteAsync(request.Action, request.Args, request.Context, linkedCts.Token).ConfigureAwait(false);

                if (result.Success)
                {
                    _breakerPolicy.RecordSuccess(request.Descriptor, pluginId);
                    _runtimeStateStore.Transition(pluginId, readyState);
                    return Complete(pluginId, stopwatch, request.Context, request.Action, result, PluginExecutionOutcomeKind.Success);
                }

                _runtimeStateStore.Transition(pluginId, readyState);
                if (request.Descriptor.Tier == PluginTier.Extension && result.Severity == PluginErrorSeverity.Critical)
                {
                    // A returned Critical result is a handled failure signal for the
                    // breaker. Only unhandled exceptions/timeouts are fatal for Core.
                    var criticalException = new InvalidOperationException(result.Message ?? "Critical plugin error");
                    _runtimeStateStore.Transition(pluginId, PluginLifecycleState.Faulted, criticalException);
                    _breakerPolicy.RecordFailure(request.Descriptor, pluginId, criticalException);
                }

                _logger.LogWarning("Plugin execution failed (logic error): {Message}", result.Message ?? "Unknown error");
                return Complete(pluginId, stopwatch, request.Context, request.Action, result, PluginExecutionOutcomeKind.HandledFailure);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !request.CancellationToken.IsCancellationRequested)
            {
                var timeoutException = new TimeoutException($"Plugin execution timed out after {ExecutionTimeout.TotalSeconds:0.#} seconds: {pluginId}");
                _runtimeStateStore.Transition(pluginId, PluginLifecycleState.Faulted, timeoutException);
                _logger.LogError(timeoutException, "Plugin execution timed out");

                if (request.Descriptor.Tier == PluginTier.Core)
                {
                    return _coreFailureHandler.Handle(request.Descriptor, timeoutException);
                }

                _breakerPolicy.RecordFailure(request.Descriptor, pluginId, timeoutException);
                var timeoutResult = PluginResult.Error($"Plugin execution timed out: {pluginId}", PluginErrorSeverity.Critical);
                return Complete(pluginId, stopwatch, request.Context, request.Action, timeoutResult, PluginExecutionOutcomeKind.Blocked, timeoutException);
            }
            catch (OperationCanceledException)
            {
                // Caller cancellation is not a plugin fault and must not trip the breaker.
                _runtimeStateStore.Transition(pluginId, readyState);
                _logger.LogInformation("Plugin execution cancelled by caller: {PluginId}", pluginId);
                var cancelledResult = PluginResult.Error("Plugin execution was cancelled.", PluginErrorSeverity.Recoverable, PluginErrorCode.UserCancelled);
                return Complete(pluginId, stopwatch, request.Context, request.Action, cancelledResult, PluginExecutionOutcomeKind.Blocked);
            }
            catch (Exception ex)
            {
                _runtimeStateStore.Transition(pluginId, PluginLifecycleState.Faulted, ex);
                _logger.LogError(ex, "Plugin execution threw exception");

                if (request.Descriptor.Tier == PluginTier.Core)
                {
                    return _coreFailureHandler.Handle(request.Descriptor, ex);
                }

                _breakerPolicy.RecordFailure(request.Descriptor, pluginId, ex);
                var result = PluginResult.Error($"Plugin execution failed: {ex.Message}");
                return Complete(pluginId, stopwatch, request.Context, request.Action, result, PluginExecutionOutcomeKind.Exception, ex);
            }
        }

        private PluginExecutionOutcome Complete(
            string pluginId,
            Stopwatch stopwatch,
            PulsarContext context,
            string action,
            PluginResult result,
            PluginExecutionOutcomeKind kind,
            Exception? exception = null)
        {
            stopwatch.Stop();
            var outcome = new PluginExecutionOutcome(result, kind);
            _usageTracker?.RecordExecution(pluginId, outcome.IsTelemetrySuccess, stopwatch.ElapsedMilliseconds, context.TargetProcessName);

            switch (outcome.Kind)
            {
                case PluginExecutionOutcomeKind.Success:
                    _healthMonitor?.RecordSuccess(pluginId);
                    break;
                case PluginExecutionOutcomeKind.HandledFailure:
                    _healthMonitor?.RecordError(pluginId, new InvalidOperationException(result.Message ?? "Plugin execution failed."), action);
                    break;
                case PluginExecutionOutcomeKind.Exception:
                    _healthMonitor?.RecordError(pluginId, exception ?? new InvalidOperationException(result.Message ?? "Plugin execution failed."), action);
                    break;
            }

            return outcome;
        }
    }

    /// <summary>
    /// 插件运行时内核 —— 三个窄 seam（注册面 <see cref="IPluginRegistry"/> /
    /// 执行面 <see cref="IPluginExecutor"/> / 运维面 <see cref="IPluginRuntimeOps"/>）
    /// 的唯一实现。持有目录、状态存储、执行管线与 Loader 的编排；
    /// 消费方只依赖与自身角色匹配的 seam，不经过安装/卸载/执行之外的宽接口。
    /// </summary>
    public class PluginRuntimeKernel : IPluginRegistry, IPluginExecutor, IPluginRuntimeOps
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly PluginLoader _loader;
        private readonly PluginCatalog _catalog;
        private readonly PluginRuntimeStateStore _runtimeStateStore;
        private readonly PluginExecutionPipeline _executionPipeline;
        private readonly ILogger<PluginRuntimeKernel> _logger;
        private readonly IConfigService? _configService;
        private readonly Core.Rendering.IRadialRendererRegistry? _rendererRegistry;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _activationLocks = new(StringComparer.OrdinalIgnoreCase);

        public PluginRuntimeKernel(
            IServiceProvider serviceProvider,
            PluginLoader loader,
            PluginCatalog catalog,
            PluginRuntimeStateStore runtimeStateStore,
            PluginExecutionPipeline executionPipeline,
            ILogger<PluginRuntimeKernel>? logger = null,
            IConfigService? configService = null,
            Core.Rendering.IRadialRendererRegistry? rendererRegistry = null)
        {
            _serviceProvider = serviceProvider;
            _loader = loader;
            _catalog = catalog;
            _runtimeStateStore = runtimeStateStore;
            _executionPipeline = executionPipeline;
            _logger = logger ?? NullLogger<PluginRuntimeKernel>.Instance;
            _configService = configService;
            _rendererRegistry = rendererRegistry;
        }

        public async Task LoadCoreAsync()
        {
            _logger.LogInformation("[PluginRuntimeKernel] Discovering startup-critical plugins...");
            _catalog.RegisterDescriptors(_loader.DiscoverDescriptors(includeCore: true, includeExtensions: false, analyzeDependencies: false));
            foreach (var descriptor in _catalog.GetAll().Where(d => d.Tier == PluginTier.Core))
            {
                await GetOrActivatePluginAsync(descriptor.Id);
            }
        }

        public Task DiscoverDeferredAsync()
        {
            _logger.LogInformation("[PluginRuntimeKernel] Discovering deferred extension plugins...");
            _catalog.RegisterDescriptors(_loader.DiscoverDescriptors(includeCore: false, includeExtensions: true, analyzeDependencies: true));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Rescans the external plugin directory after an install/uninstall and
        /// registers any newly discovered descriptors. Discovery otherwise runs
        /// only once at startup, so plugins installed at runtime are invisible
        /// to the catalog (and thus to permission grants) until the next launch.
        /// </summary>
        public Task RefreshDiscoveryAsync()
        {
            _loader.InvalidateDiscoveryCache();
            _logger.LogInformation("[PluginRuntimeKernel] Refreshing plugin discovery after package change...");
            _catalog.RegisterDescriptors(_loader.DiscoverDescriptors(includeCore: false, includeExtensions: true, analyzeDependencies: true));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Fully deactivates a single plugin so its install directory can be
        /// deleted while the app is running. Runs the unload lifecycle hook,
        /// removes the runtime state and catalog entry (dropping all strong
        /// references to the plugin instance and its types), unregisters any
        /// renderer contributions, invalidates the discovery cache, and
        /// unloads the plugin's assembly load context to release file locks.
        /// </summary>
        public async Task DeactivatePluginAsync(string pluginId)
        {
            if (_runtimeStateStore.TryGetPlugin(pluginId, out var plugin))
            {
                if (plugin is IPluginLifecycle lifecycle)
                {
                    try
                    {
                        await lifecycle.OnUnloadAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[PluginRuntimeKernel] OnUnloadAsync failed for {PluginId}", pluginId);
                    }
                }

                _runtimeStateStore.RemovePlugin(pluginId);

                // Drop renderer contributions so the UI falls back to built-ins.
                _rendererRegistry?.UnregisterOwner(pluginId);
            }

            // Sever the implementation type BEFORE dropping the catalog entry.
            // External descriptors carry a Type loaded from the collectible ALC,
            // so any live holder (e.g. the Plugin Manager page's descriptor list)
            // keeps the context alive and the DLL locked. Nulling the Type breaks
            // that pin; the catalog entry is then removed and the GC below can
            // actually collect the context.
            if (_catalog.TryGetDescriptor(pluginId, out var descriptor) && descriptor != null)
            {
                descriptor.ImplementationType = null;
            }

            _catalog.RemoveDescriptor(pluginId);
            _loader.InvalidateDiscoveryCache();

            // TryUnloadExternalContext owns the whole collectible-ALC teardown:
            // Unload() initiation plus the forced GC pump that actually releases
            // the plugin DLL file locks, so the directory is deletable right
            // after this call (candidate E, architecture review 2026-09-04).
            _loader.TryUnloadExternalContext(pluginId);

            _logger.LogInformation("[PluginRuntimeKernel] Deactivated plugin {PluginId}", pluginId);
        }

        public PluginDescriptor? GetDescriptor(string pluginId)
        {
            _catalog.TryGetDescriptor(pluginId, out var descriptor);
            return descriptor;
        }

        public IEnumerable<PluginDescriptor> GetAllPluginDescriptors()
        {
            return _catalog.GetAll();
        }

        public IPulsarPlugin? GetPlugin(string pluginId)
        {
            _runtimeStateStore.TryGetPlugin(pluginId, out var plugin);
            return plugin;
        }

        public IEnumerable<IPulsarPlugin> GetAllPlugins()
        {
            return _runtimeStateStore.Plugins.Values;
        }

        public async Task<IPulsarPlugin?> GetOrActivatePluginAsync(string pluginId)
        {
            if (_runtimeStateStore.TryGetPlugin(pluginId, out var existingPlugin))
            {
                return existingPlugin;
            }

            if (!_catalog.TryGetDescriptor(pluginId, out var descriptor) || descriptor == null)
            {
                return null;
            }

            // Per-plugin activation gate. Two slots may request the same plugin at
            // the same time; without this gate each request creates its own instance
            // and the second one overwrites runtime state, losing lifecycle hooks.
            var activationLock = _activationLocks.GetOrAdd(pluginId, static _ => new SemaphoreSlim(1, 1));
            await activationLock.WaitAsync();
            try
            {
                if (_runtimeStateStore.TryGetPlugin(pluginId, out existingPlugin))
                {
                    return existingPlugin;
                }

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var plugin = _loader.ActivatePlugin(descriptor);
                    _runtimeStateStore.SetPlugin(plugin, PluginLifecycleState.Loaded);
                    await ApplyProfileAsync(descriptor, plugin);
                    stopwatch.Stop();
                    _logger.LogInformation("[PluginRuntimeKernel] Activated plugin {PluginId} in {ElapsedMs}ms", plugin.Id, stopwatch.ElapsedMilliseconds);
                    return plugin;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _runtimeStateStore.Transition(pluginId, PluginLifecycleState.Faulted, ex);
                    _logger.LogError(ex, "[PluginRuntimeKernel] Failed to activate plugin {PluginId} after {ElapsedMs}ms", pluginId, stopwatch.ElapsedMilliseconds);
                    return null;
                }
            }
            finally
            {
                activationLock.Release();
            }
        }

        public async Task<PluginResult> ExecuteAsync(string pluginId, string action, IReadOnlyDictionary<string, string> args, PulsarContext context, CancellationToken cancellationToken = default)
        {
            var descriptor = GetDescriptor(pluginId);
            if (descriptor == null)
            {
                _logger.LogError("Plugin not found: {PluginId}", pluginId);
                return PluginResult.Error($"Plugin not found: {pluginId}");
            }

            var outcome = await _executionPipeline.ExecuteAsync(new PluginExecutionRequest
            {
                Descriptor = descriptor,
                Action = action,
                Args = args,
                Context = context,
                GrantedPermissions = GetGrantedPermissions(pluginId),
                IsEnabled = () => IsPluginEnabled(pluginId),
                ActivateAsync = () => GetOrActivatePluginAsync(pluginId),
                CancellationToken = cancellationToken
            });

            return outcome.Result;
        }

        /// <summary>
        /// Persists user-approved permissions for an external plugin. Unknown
        /// permission tokens are rejected before touching Profiles.json.
        /// </summary>
        public async Task GrantPermissionsAsync(string pluginId, IEnumerable<string> permissions)
        {
            if (_configService == null)
            {
                return;
            }

            var descriptor = GetDescriptor(pluginId);
            if (descriptor == null)
            {
                _logger.LogWarning("[PluginRuntimeKernel] Cannot grant permissions for unknown plugin: {PluginId}", pluginId);
                return;
            }

            var normalized = permissions
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (var permission in normalized)
            {
                if (!PluginPermissions.IsKnown(permission))
                {
                    throw new ArgumentException($"Unknown plugin permission: {permission}", nameof(permissions));
                }
            }

            await ConfigEditSession.RunAsync(_configService, session =>
                session.UpdatePluginProfile(pluginId, profile =>
                    profile.GrantedPermissions = normalized.ToList()));
            _logger.LogInformation(
                "[PluginRuntimeKernel] Granted {Count} permissions for {PluginId}",
                normalized.Length,
                pluginId);
        }

        private IReadOnlyCollection<string> GetGrantedPermissions(string pluginId)
        {
            if (_configService?.GetSnapshot().Plugins.TryGetValue(pluginId, out var profile) == true)
            {
                return profile.GrantedPermissions;
            }

            return Array.Empty<string>();
        }

        public async Task SetPluginStateAsync(string pluginId, bool enabled)
        {
            if (_configService == null)
            {
                return;
            }

            var descriptor = GetDescriptor(pluginId);
            if (descriptor == null)
            {
                return;
            }

            if (descriptor.Tier == PluginTier.Core)
            {
                _logger.LogWarning("[PluginRuntimeKernel] Cannot disable core plugin: {PluginId}", pluginId);
                return;
            }

            var currentProfile = _configService.GetSnapshot().Plugins.TryGetValue(pluginId, out var current)
                ? current
                : new PluginProfile();
            if (currentProfile.Enabled == enabled)
            {
                return;
            }

            await ConfigEditSession.RunAsync(_configService, session =>
                session.UpdatePluginProfile(pluginId, profile => profile.Enabled = enabled));

            if (_runtimeStateStore.TryGetPlugin(pluginId, out var plugin) && plugin is IPluginLifecycle lifecycle)
            {
                if (enabled)
                {
                    await lifecycle.OnEnableAsync();
                    _runtimeStateStore.Transition(pluginId, PluginLifecycleState.Enabled);
                }
                else
                {
                    await lifecycle.OnDisableAsync();
                    _runtimeStateStore.Transition(pluginId, PluginLifecycleState.Disabled);
                }
            }
            else
            {
                _runtimeStateStore.Transition(pluginId, enabled ? PluginLifecycleState.Enabled : PluginLifecycleState.Disabled);
            }

            // [RadialRenderer] Unconditional owner cleanup on disable: a plugin that
            // registered renderers but did not unregister them in OnDisableAsync must
            // never leave dangling contributions behind (resolution falls back to Default).
            if (!enabled)
            {
                var removedRenderers = _rendererRegistry?.UnregisterOwner(pluginId) ?? 0;
                if (removedRenderers > 0)
                {
                    _logger.LogInformation(
                        "[PluginRuntimeKernel] Removed {Count} plugin renderer(s) on disable of {PluginId}",
                        removedRenderers,
                        pluginId);
                }
            }
        }

        public bool IsPluginEnabled(string pluginId)
        {
            var descriptor = GetDescriptor(pluginId);
            if (descriptor != null && descriptor.Tier == PluginTier.Core)
            {
                return true;
            }

            if (_configService?.GetSnapshot()?.Plugins.TryGetValue(pluginId, out var profile) == true)
            {
                return profile.Enabled;
            }

            return true;
        }

        public async Task UnloadAllAsync()
        {
            foreach (var plugin in _runtimeStateStore.Plugins.Values.ToList())
            {
                if (plugin is IPluginLifecycle lifecycle)
                {
                    try
                    {
                        await lifecycle.OnUnloadAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[PluginRuntimeKernel] OnUnloadAsync failed for {PluginId}", plugin.Id);
                    }
                }

                _runtimeStateStore.RemovePlugin(plugin.Id);

                // [RadialRenderer] Unconditional owner cleanup on unload.
                _rendererRegistry?.UnregisterOwner(plugin.Id);
            }
        }

        private async Task ApplyProfileAsync(PluginDescriptor descriptor, IPulsarPlugin plugin)
        {
            if (_configService == null)
            {
                _runtimeStateStore.Transition(plugin.Id, PluginLifecycleState.Enabled);
                return;
            }

            // Read-only apply: the runtime applies the user's persisted profile (or
            // defaults when absent) but never writes back to Profiles.json. Activating
            // a plugin is not a user configuration change.
            var config = _configService.GetSnapshot();
            PluginProfile? profile = null;
            config.Plugins.TryGetValue(plugin.Id, out profile);
            profile ??= new PluginProfile { Enabled = true };

            if (plugin is IPluginConfigurable configurable)
            {
                try
                {
                    var validationResult = configurable.ValidateSettings(profile.Config);
                    if (!validationResult.IsValid)
                    {
                        _logger.LogError("[PluginRuntimeKernel] Invalid settings for {PluginId}: {Errors}", plugin.Id, string.Join(", ", validationResult.Errors));
                        profile = new PluginProfile { Enabled = profile.Enabled, Config = GetDefaultSettings(configurable) };
                    }

                    configurable.UpdateSettings(profile.Config);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PluginRuntimeKernel] Failed to apply settings for {PluginId}", plugin.Id);
                }
            }

            if (descriptor.Tier == PluginTier.Core)
            {
                if (plugin is IPluginLifecycle coreLifecycle)
                {
                    await coreLifecycle.OnEnableAsync();
                }

                _runtimeStateStore.Transition(plugin.Id, PluginLifecycleState.Enabled);
                return;
            }

            if (profile.Enabled)
            {
                if (plugin is IPluginLifecycle lifecycle)
                {
                    await lifecycle.OnEnableAsync();
                }

                _runtimeStateStore.Transition(plugin.Id, PluginLifecycleState.Enabled);
            }
            else
            {
                _runtimeStateStore.Transition(plugin.Id, PluginLifecycleState.Disabled);
            }
        }

        private static Dictionary<string, object> GetDefaultSettings(IPluginConfigurable configurable)
        {
            var defaultSettings = new Dictionary<string, object>();
            foreach (var definition in configurable.GetSettingsDefinition())
            {
                if (definition.DefaultValue != null)
                {
                    defaultSettings[definition.Key] = definition.DefaultValue;
                }
            }

            return defaultSettings;
        }
    }
}


