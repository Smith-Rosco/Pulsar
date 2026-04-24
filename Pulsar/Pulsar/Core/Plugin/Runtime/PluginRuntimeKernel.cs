using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

    public interface IPluginCatalog
    {
        IDictionary<string, PluginDescriptor> Descriptors { get; }

        void RegisterDescriptors(IEnumerable<PluginDescriptor> descriptors);

        bool TryGetDescriptor(string pluginId, out PluginDescriptor? descriptor);

        IEnumerable<PluginDescriptor> GetAll();
    }

    public interface IPluginRuntimeStateStore
    {
        IDictionary<string, IPulsarPlugin> Plugins { get; }

        PluginLifecycleState GetState(string pluginId);

        PluginRuntimeSnapshot GetSnapshot(string pluginId);

        void SetPlugin(IPulsarPlugin plugin, PluginLifecycleState state);

        void Transition(string pluginId, PluginLifecycleState state, Exception? error = null);

        bool TryGetPlugin(string pluginId, out IPulsarPlugin? plugin);

        void RemovePlugin(string pluginId);
    }

    public interface IPluginBreakerPolicy
    {
        PluginBreakerAvailability CheckAvailability(PluginDescriptor descriptor, string pluginId);

        void RecordSuccess(PluginDescriptor descriptor, string pluginId);

        void RecordFailure(PluginDescriptor descriptor, string pluginId, Exception ex);
    }

    public interface IPluginExecutionPipeline
    {
        Task<PluginExecutionOutcome> ExecuteAsync(PluginExecutionRequest request);
    }

    public interface IPluginRuntimeKernel
    {
        Task LoadCoreAsync();

        Task DiscoverDeferredAsync();

        PluginDescriptor? GetDescriptor(string pluginId);

        IEnumerable<PluginDescriptor> GetAllPluginDescriptors();

        IPulsarPlugin? GetPlugin(string pluginId);

        IEnumerable<IPulsarPlugin> GetAllPlugins();

        Task<IPulsarPlugin?> GetOrActivatePluginAsync(string pluginId);

        Task<PluginResult> ExecuteAsync(string pluginId, string action, IReadOnlyDictionary<string, string> args, PulsarContext context);

        Task SetPluginStateAsync(string pluginId, bool enabled);

        bool IsPluginEnabled(string pluginId);

        Task UnloadAllAsync();
    }

    public sealed class PluginCatalog : IPluginCatalog
    {
        private readonly Dictionary<string, PluginDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, PluginDescriptor> Descriptors => _descriptors;

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
    }

    public sealed class PluginRuntimeStateStore : IPluginRuntimeStateStore
    {
        private readonly Dictionary<string, IPulsarPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PluginRuntimeSnapshot> _snapshots = new(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, IPulsarPlugin> Plugins => _plugins;

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

            _snapshots[pluginId] = snapshot;
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
            _snapshots[pluginId] = new PluginRuntimeSnapshot
            {
                PluginId = pluginId,
                State = state,
                LastError = error,
                LoadedAtUtc = snapshot.LoadedAtUtc ?? (state is PluginLifecycleState.Loaded or PluginLifecycleState.Enabled or PluginLifecycleState.Disabled or PluginLifecycleState.Running or PluginLifecycleState.Recovering or PluginLifecycleState.Faulted ? DateTime.UtcNow : null),
                UnloadedAtUtc = state == PluginLifecycleState.Unloaded ? DateTime.UtcNow : snapshot.UnloadedAtUtc
            };
        }

        public bool TryGetPlugin(string pluginId, out IPulsarPlugin? plugin)
        {
            return _plugins.TryGetValue(pluginId, out plugin);
        }

        public void RemovePlugin(string pluginId)
        {
            _plugins.Remove(pluginId);
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

    public sealed class PluginCircuitBreakerPolicy : IPluginBreakerPolicy
    {
        private const int MaxFailures = 3;
        private static readonly TimeSpan ResetTimeout = TimeSpan.FromMinutes(1);

        private readonly Dictionary<string, int> _failureCounts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _brokenCircuits = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger<PluginCircuitBreakerPolicy> _logger;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly ITrayService? _trayService;

        public PluginCircuitBreakerPolicy(
            ILogger<PluginCircuitBreakerPolicy>? logger = null,
            IPluginHealthMonitor? healthMonitor = null,
            ITrayService? trayService = null)
        {
            _logger = logger ?? NullLogger<PluginCircuitBreakerPolicy>.Instance;
            _healthMonitor = healthMonitor;
            _trayService = trayService;
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

            _brokenCircuits.Remove(pluginId);
            _healthMonitor?.RecordCircuitBreakerRecovery(pluginId);
            _logger.LogInformation("Circuit Half-Open: Retrying {PluginId}...", pluginId);
            return new PluginBreakerAvailability(true, recovered: true);
        }

        public void RecordSuccess(PluginDescriptor descriptor, string pluginId)
        {
            if (descriptor.Tier != PluginTier.Extension)
            {
                return;
            }

            if (_failureCounts.Remove(pluginId))
            {
                _logger.LogDebug("Reset failure count for {PluginId} after successful execution", pluginId);
            }
        }

        public void RecordFailure(PluginDescriptor descriptor, string pluginId, Exception ex)
        {
            if (descriptor.Tier != PluginTier.Extension)
            {
                return;
            }

            if (!_failureCounts.ContainsKey(pluginId))
            {
                _failureCounts[pluginId] = 0;
            }

            _failureCounts[pluginId]++;
            var count = _failureCounts[pluginId];
            _logger.LogWarning(ex, "Plugin crashed ({Count}/{MaxFailures})", count, MaxFailures);

            if (count < MaxFailures)
            {
                return;
            }

            _brokenCircuits[pluginId] = DateTime.UtcNow;
            _failureCounts.Remove(pluginId);
            _logger.LogCritical("Circuit Breaker Tripped! Plugin temporarily disabled for {Timeout}s", ResetTimeout.TotalSeconds);
            _healthMonitor?.RecordCircuitBreakerTrip(pluginId);
            _trayService?.ShowNotification(
                "插件已自动禁用",
                $"插件 '{pluginId}' 因多次崩溃已被暂时禁用 {ResetTimeout.TotalSeconds} 秒，以保护主程序运行。",
                System.Windows.Forms.ToolTipIcon.Error);
        }
    }

    public sealed class PluginExecutionRequest
    {
        public required PluginDescriptor Descriptor { get; init; }

        public required string Action { get; init; }

        public required IReadOnlyDictionary<string, string> Args { get; init; }

        public required PulsarContext Context { get; init; }

        public required Func<bool> IsEnabled { get; init; }

        public required Func<Task<IPulsarPlugin?>> ActivateAsync { get; init; }
    }

    public sealed class PluginExecutionPipeline : IPluginExecutionPipeline
    {
        private readonly IPluginRuntimeStateStore _runtimeStateStore;
        private readonly IPluginBreakerPolicy _breakerPolicy;
        private readonly IPluginUsageTracker? _usageTracker;
        private readonly IPluginHealthMonitor? _healthMonitor;
        private readonly ILogger<PluginExecutionPipeline> _logger;

        public PluginExecutionPipeline(
            IPluginRuntimeStateStore runtimeStateStore,
            IPluginBreakerPolicy breakerPolicy,
            ILogger<PluginExecutionPipeline>? logger = null,
            IPluginUsageTracker? usageTracker = null,
            IPluginHealthMonitor? healthMonitor = null)
        {
            _runtimeStateStore = runtimeStateStore;
            _breakerPolicy = breakerPolicy;
            _usageTracker = usageTracker;
            _healthMonitor = healthMonitor;
            _logger = logger ?? NullLogger<PluginExecutionPipeline>.Instance;
        }

        public async Task<PluginExecutionOutcome> ExecuteAsync(PluginExecutionRequest request)
        {
            var pluginId = request.Descriptor.Id;

            if (request.Descriptor.Tier == PluginTier.Extension && !request.IsEnabled())
            {
                _logger.LogWarning("Plugin is disabled by user: {PluginId}", pluginId);
                return new PluginExecutionOutcome(PluginResult.Error("Plugin is disabled."), PluginExecutionOutcomeKind.Blocked);
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

            var plugin = await request.ActivateAsync();
            if (plugin == null)
            {
                _logger.LogError("Plugin activation failed or plugin unavailable: {PluginId}", pluginId);
                return new PluginExecutionOutcome(PluginResult.Error($"Plugin unavailable: {pluginId}"), PluginExecutionOutcomeKind.Blocked);
            }

            using var executionScope = PluginExecutionContext.BeginScope(
                pluginId,
                request.Action,
                targetProcessName: request.Context.TargetProcessName);

            var stopwatch = Stopwatch.StartNew();
            var readyState = request.Descriptor.CanDisable ? PluginLifecycleState.Enabled : PluginLifecycleState.Enabled;
            _runtimeStateStore.Transition(pluginId, PluginLifecycleState.Running);

            try
            {
                var result = await plugin.ExecuteAsync(request.Action, request.Args, request.Context);

                if (result.Success)
                {
                    _breakerPolicy.RecordSuccess(request.Descriptor, pluginId);
                    _runtimeStateStore.Transition(pluginId, readyState);
                    return Complete(pluginId, stopwatch, request.Context, request.Action, result, PluginExecutionOutcomeKind.Success);
                }

                _runtimeStateStore.Transition(pluginId, readyState);
                if (request.Descriptor.Tier == PluginTier.Extension && result.Severity == PluginErrorSeverity.Critical)
                {
                    var criticalException = new InvalidOperationException(result.Message ?? "Critical plugin error");
                    _runtimeStateStore.Transition(pluginId, PluginLifecycleState.Faulted, criticalException);
                    _breakerPolicy.RecordFailure(request.Descriptor, pluginId, criticalException);
                }

                _logger.LogWarning("Plugin execution failed (logic error): {Message}", result.Message ?? "Unknown error");
                return Complete(pluginId, stopwatch, request.Context, request.Action, result, PluginExecutionOutcomeKind.HandledFailure);
            }
            catch (Exception ex)
            {
                _runtimeStateStore.Transition(pluginId, PluginLifecycleState.Faulted, ex);
                _breakerPolicy.RecordFailure(request.Descriptor, pluginId, ex);
                _logger.LogError(ex, "Plugin execution threw exception");
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

    public sealed class PluginRuntimeKernel : IPluginRuntimeKernel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly PluginLoader _loader;
        private readonly IPluginCatalog _catalog;
        private readonly IPluginRuntimeStateStore _runtimeStateStore;
        private readonly IPluginExecutionPipeline _executionPipeline;
        private readonly ILogger<PluginRuntimeKernel> _logger;
        private readonly IConfigService? _configService;

        public PluginRuntimeKernel(
            IServiceProvider serviceProvider,
            PluginLoader loader,
            IPluginCatalog catalog,
            IPluginRuntimeStateStore runtimeStateStore,
            IPluginExecutionPipeline executionPipeline,
            ILogger<PluginRuntimeKernel>? logger = null,
            IConfigService? configService = null)
        {
            _serviceProvider = serviceProvider;
            _loader = loader;
            _catalog = catalog;
            _runtimeStateStore = runtimeStateStore;
            _executionPipeline = executionPipeline;
            _logger = logger ?? NullLogger<PluginRuntimeKernel>.Instance;
            _configService = configService;
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

        public async Task<PluginResult> ExecuteAsync(string pluginId, string action, IReadOnlyDictionary<string, string> args, PulsarContext context)
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
                IsEnabled = () => IsPluginEnabled(pluginId),
                ActivateAsync = () => GetOrActivatePluginAsync(pluginId)
            });

            return outcome.Result;
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

            if (!descriptor.CanDisable)
            {
                _logger.LogWarning("[PluginRuntimeKernel] Cannot disable core plugin: {PluginId}", pluginId);
                return;
            }

            var config = _configService.Current;
            if (!config.Plugins.TryGetValue(pluginId, out var profile))
            {
                profile = new PluginProfile();
                config.Plugins[pluginId] = profile;
            }

            if (profile.Enabled == enabled)
            {
                return;
            }

            profile.Enabled = enabled;
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

            await _configService.SaveAsync(config);
        }

        public bool IsPluginEnabled(string pluginId)
        {
            var descriptor = GetDescriptor(pluginId);
            if (descriptor != null && !descriptor.CanDisable)
            {
                return true;
            }

            if (_configService?.Current?.Plugins.TryGetValue(pluginId, out var profile) == true)
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
            }
        }

        private async Task ApplyProfileAsync(PluginDescriptor descriptor, IPulsarPlugin plugin)
        {
            if (_configService == null)
            {
                _runtimeStateStore.Transition(plugin.Id, descriptor.CanDisable ? PluginLifecycleState.Enabled : PluginLifecycleState.Enabled);
                return;
            }

            var config = _configService.Current;
            if (!config.Plugins.TryGetValue(plugin.Id, out var profile))
            {
                profile = new PluginProfile { Enabled = true };
                config.Plugins[plugin.Id] = profile;
            }

            if (plugin is IPluginConfigurable configurable)
            {
                try
                {
                    var validationResult = configurable.ValidateSettings(profile.Config);
                    if (!validationResult.IsValid)
                    {
                        _logger.LogError("[PluginRuntimeKernel] Invalid settings for {PluginId}: {Errors}", plugin.Id, string.Join(", ", validationResult.Errors));
                        profile.Config = GetDefaultSettings(configurable);
                    }

                    configurable.UpdateSettings(profile.Config);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PluginRuntimeKernel] Failed to apply settings for {PluginId}", plugin.Id);
                }
            }

            if (!descriptor.CanDisable)
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
