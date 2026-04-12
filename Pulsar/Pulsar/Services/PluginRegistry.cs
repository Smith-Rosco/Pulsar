// [Path]: Pulsar/Pulsar/Services/PluginRegistry.cs

using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Pulsar.Services
{
    /// <summary>
    /// 插件注册中心 - 管理插件发现、按需激活和执行调度。
    /// </summary>
    public class PluginRegistry
    {
        private readonly Dictionary<string, PluginDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IPulsarPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, int> _failureCounts = new();
        private readonly Dictionary<string, DateTime> _brokenCircuits = new();
        private const int MaxFailures = 3;
        private readonly TimeSpan ResetTimeout = TimeSpan.FromMinutes(1);

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PluginRegistry> _logger;
        private readonly PluginLoader _loader;
        private readonly Services.Interfaces.ITrayService? _trayService;
        private readonly Services.Interfaces.IConfigService? _configService;
        private readonly Services.Interfaces.IPluginUsageTracker? _usageTracker;
        private readonly Services.Interfaces.IPluginHealthMonitor? _healthMonitor;
        private readonly Services.Interfaces.IPluginLogService? _logService;

        public PluginRegistry(IServiceProvider serviceProvider, ILogger<PluginRegistry> logger)
            : this(serviceProvider, logger, null)
        {
        }

        public PluginRegistry(IServiceProvider serviceProvider, ILogger<PluginRegistry> logger, PluginLoader? loader)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _trayService = _serviceProvider.GetService(typeof(Services.Interfaces.ITrayService)) as Services.Interfaces.ITrayService;
            _configService = _serviceProvider.GetService(typeof(Services.Interfaces.IConfigService)) as Services.Interfaces.IConfigService;
            _usageTracker = _serviceProvider.GetService(typeof(Services.Interfaces.IPluginUsageTracker)) as Services.Interfaces.IPluginUsageTracker;
            _healthMonitor = _serviceProvider.GetService(typeof(Services.Interfaces.IPluginHealthMonitor)) as Services.Interfaces.IPluginHealthMonitor;
            _logService = _serviceProvider.GetService(typeof(Services.Interfaces.IPluginLogService)) as Services.Interfaces.IPluginLogService;

            string pluginDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            _loader = loader ?? new PluginLoader(serviceProvider, pluginDir);
        }

        public async Task LoadCoreAsync()
        {
            _logger.LogInformation("[PluginRegistry] Discovering startup-critical plugins...");
            RegisterDescriptors(_loader.DiscoverDescriptors(includeCore: true, includeExtensions: false, analyzeDependencies: false));
            await ActivateDescriptorsAsync(_descriptors.Values.Where(descriptor => descriptor.Tier == PluginTier.Core));
        }

        public Task DiscoverDeferredAsync()
        {
            _logger.LogInformation("[PluginRegistry] Discovering deferred extension plugins...");
            RegisterDescriptors(_loader.DiscoverDescriptors(includeCore: false, includeExtensions: true, analyzeDependencies: true));
            return Task.CompletedTask;
        }

        public Task LoadAllAsync()
        {
            return LoadCoreAsync();
        }

        public PluginDescriptor? GetDescriptor(string pluginId)
        {
            _descriptors.TryGetValue(pluginId, out var descriptor);
            return descriptor;
        }

        public IEnumerable<PluginDescriptor> GetAllPluginDescriptors()
        {
            return _descriptors.Values;
        }

        public IPulsarPlugin? GetPlugin(string pluginId)
        {
            _plugins.TryGetValue(pluginId, out var plugin);
            return plugin;
        }

        public async Task<IPulsarPlugin?> GetOrActivatePluginAsync(string pluginId)
        {
            if (_plugins.TryGetValue(pluginId, out var existingPlugin))
            {
                return existingPlugin;
            }

            if (!_descriptors.TryGetValue(pluginId, out var descriptor))
            {
                return null;
            }

            if (descriptor.Tier == PluginTier.Extension && !IsPluginEnabled(pluginId))
            {
                return null;
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                var plugin = _loader.ActivatePlugin(descriptor);
                _plugins[plugin.Id] = plugin;
                await ApplyProfileAsync(plugin);
                stopwatch.Stop();
                _logger.LogInformation("[PluginRegistry] Activated plugin {PluginId} in {ElapsedMs}ms", plugin.Id, stopwatch.ElapsedMilliseconds);
                return plugin;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "[PluginRegistry] Failed to activate plugin {PluginId} after {ElapsedMs}ms", pluginId, stopwatch.ElapsedMilliseconds);

                if (descriptor.Tier == PluginTier.Extension)
                {
                    HandlePluginCrash(pluginId, ex);
                }

                return null;
            }
        }

        public async Task<PluginResult> ExecuteAsync(
            string pluginId,
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            var descriptor = GetDescriptor(pluginId);
            if (descriptor == null)
            {
                _logger.LogError("Plugin not found: {PluginId}", pluginId);
                return PluginResult.Error($"Plugin not found: {pluginId}");
            }

            if (descriptor.Tier == PluginTier.Extension)
            {
                if (_configService != null && _configService.Current != null
                    && _configService.Current.Plugins.TryGetValue(pluginId, out var profile)
                    && !profile.Enabled)
                {
                    _logger.LogWarning("Plugin is disabled by user: {PluginId}", pluginId);
                    return PluginResult.Error("Plugin is disabled.");
                }

                if (_brokenCircuits.TryGetValue(pluginId, out var breakTime))
                {
                    if (DateTime.UtcNow - breakTime < ResetTimeout)
                    {
                        var remaining = (int)(ResetTimeout - (DateTime.UtcNow - breakTime)).TotalSeconds;
                        _logger.LogWarning("Circuit Open: {PluginId} is disabled for {Remaining}s", pluginId, remaining);
                        return PluginResult.Error($"Plugin disabled for safety. Try again in {remaining}s.");
                    }

                    _brokenCircuits.Remove(pluginId);
                    _logger.LogInformation("Circuit Half-Open: Retrying {PluginId}...", pluginId);
                }
            }

            var plugin = await GetOrActivatePluginAsync(pluginId);
            if (plugin == null)
            {
                _logger.LogError("Plugin activation failed or plugin unavailable: {PluginId}", pluginId);
                return PluginResult.Error($"Plugin unavailable: {pluginId}");
            }

            using var executionScope = PluginExecutionContext.BeginScope(
                pluginId,
                action,
                targetProcessName: context.TargetProcessName);

            var stopwatch = Stopwatch.StartNew();
            bool success = false;
            Exception? exception = null;

            try
            {
                var result = await plugin.ExecuteAsync(action, args, context);
                success = result.Success;

                if (result.Success)
                {
                    _logger.LogDebug("Plugin execution succeeded: {Message}", result.Message ?? "OK");

                    if (descriptor.Tier == PluginTier.Extension && _failureCounts.ContainsKey(pluginId))
                    {
                        _failureCounts.Remove(pluginId);
                        _logger.LogDebug("Reset failure count for {PluginId} after successful execution", pluginId);
                    }
                }
                else
                {
                    _logger.LogWarning("Plugin execution failed (logic error): {Message}", result.Message ?? "Unknown error");

                    if (descriptor.Tier == PluginTier.Extension && result.Severity == PluginErrorSeverity.Critical)
                    {
                        _logger.LogWarning("Critical error detected, counting towards circuit breaker");
                        HandlePluginCrash(pluginId, new InvalidOperationException(result.Message ?? "Critical plugin error"));
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                exception = ex;
                success = false;
                _logger.LogError(ex, "Plugin execution threw exception");

                if (descriptor.Tier == PluginTier.Extension)
                {
                    HandlePluginCrash(pluginId, ex);
                }

                return PluginResult.Error($"Plugin execution failed: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                _usageTracker?.RecordExecution(pluginId, success, stopwatch.ElapsedMilliseconds, context.TargetProcessName);

                if (success)
                {
                    _healthMonitor?.RecordSuccess(pluginId);
                }
                else if (exception != null)
                {
                    _healthMonitor?.RecordError(pluginId, exception, action);
                }
            }
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
                _logger.LogWarning("[PluginRegistry] Cannot disable core plugin: {PluginId}", pluginId);
                return;
            }

            var config = _configService.Current;
            if (!config.Plugins.TryGetValue(pluginId, out var profile))
            {
                profile = new Models.PluginProfile();
                config.Plugins[pluginId] = profile;
            }

            if (profile.Enabled == enabled)
            {
                return;
            }

            profile.Enabled = enabled;
            _logger.LogInformation("[PluginRegistry] Plugin {PluginId} state changed to {State}", pluginId, enabled ? "Enabled" : "Disabled");

            if (_plugins.TryGetValue(pluginId, out var plugin) && plugin is IPluginLifecycle lifecycle)
            {
                try
                {
                    if (enabled)
                    {
                        await lifecycle.OnEnableAsync();
                        _logger.LogDebug("[PluginRegistry] Called OnEnableAsync for {PluginId}", pluginId);
                    }
                    else
                    {
                        await lifecycle.OnDisableAsync();
                        _logger.LogDebug("[PluginRegistry] Called OnDisableAsync for {PluginId}", pluginId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PluginRegistry] Lifecycle hook failed for {PluginId}", pluginId);
                }
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

        public IEnumerable<IPulsarPlugin> GetAllPlugins()
        {
            return _plugins.Values;
        }

        public int Count => _plugins.Count;

        public async Task UnloadAllAsync()
        {
            _logger.LogInformation("[PluginRegistry] Unloading {Count} plugins...", _plugins.Count);

            var unloadedCount = 0;
            var failedCount = 0;

            foreach (var plugin in _plugins.Values)
            {
                if (plugin is IPluginLifecycle lifecycle)
                {
                    try
                    {
                        await lifecycle.OnUnloadAsync();
                        _logger.LogDebug("[PluginRegistry] Called OnUnloadAsync for {PluginId}", plugin.Id);
                        unloadedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[PluginRegistry] OnUnloadAsync failed for {PluginId}", plugin.Id);
                        failedCount++;
                    }
                }
            }

            if (failedCount > 0)
            {
                _logger.LogWarning("[PluginRegistry] Unloaded {Unloaded} plugins ({Failed} failed)", unloadedCount, failedCount);
            }
            else
            {
                _logger.LogInformation("[PluginRegistry] All {Count} plugins unloaded successfully", unloadedCount);
            }
        }

        private void RegisterDescriptors(IEnumerable<PluginDescriptor> descriptors)
        {
            foreach (var descriptor in descriptors)
            {
                if (_descriptors.ContainsKey(descriptor.Id))
                {
                    continue;
                }

                _descriptors[descriptor.Id] = descriptor;
            }
        }

        private async Task ActivateDescriptorsAsync(IEnumerable<PluginDescriptor> descriptors)
        {
            var loadedPluginNames = new List<string>();
            var failedPlugins = new List<string>();

            foreach (var descriptor in descriptors)
            {
                if (_plugins.ContainsKey(descriptor.Id))
                {
                    continue;
                }

                try
                {
                    var plugin = _loader.ActivatePlugin(descriptor);
                    _plugins[plugin.Id] = plugin;
                    await ApplyProfileAsync(plugin);
                    loadedPluginNames.Add(plugin.DisplayName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PluginRegistry] Failed to activate plugin {PluginId}", descriptor.Id);
                    failedPlugins.Add(descriptor.DisplayName);
                }
            }

            _logger.LogInformation("[PluginRegistry] Activated {Count} plugins: {PluginList}", loadedPluginNames.Count, string.Join(", ", loadedPluginNames));

            if (failedPlugins.Count > 0)
            {
                _logger.LogWarning("[PluginRegistry] {Count} plugins failed to initialize: {FailedList}", failedPlugins.Count, string.Join(", ", failedPlugins));
            }
        }

        private async Task ApplyProfileAsync(IPulsarPlugin plugin)
        {
            if (_configService == null)
            {
                return;
            }

            var config = _configService.Current;
            if (!config.Plugins.TryGetValue(plugin.Id, out var profile))
            {
                profile = new Models.PluginProfile { Enabled = true };
                config.Plugins[plugin.Id] = profile;
            }

            if (plugin is IPluginConfigurable configurable)
            {
                try
                {
                    var validationResult = configurable.ValidateSettings(profile.Config);
                    if (!validationResult.IsValid)
                    {
                        _logger.LogError("[PluginRegistry] Invalid settings for {PluginId}: {Errors}", plugin.Id, string.Join(", ", validationResult.Errors));
                        var defaultSettings = GetDefaultSettings(configurable);
                        configurable.UpdateSettings(defaultSettings);
                        profile.Config = defaultSettings;
                    }
                    else
                    {
                        configurable.UpdateSettings(profile.Config);
                        _logger.LogDebug("[PluginRegistry] Applied settings for {PluginId}", plugin.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PluginRegistry] Failed to apply settings for {PluginId}", plugin.Id);
                }
            }

            if (profile.Enabled && plugin is IPluginLifecycle lifecycle)
            {
                try
                {
                    await lifecycle.OnEnableAsync();
                    _logger.LogDebug("[PluginRegistry] Called OnEnableAsync for {PluginId}", plugin.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[PluginRegistry] OnEnableAsync failed for {PluginId}", plugin.Id);
                }
            }
        }

        private void HandlePluginCrash(string pluginId, Exception ex)
        {
            if (!_failureCounts.ContainsKey(pluginId))
            {
                _failureCounts[pluginId] = 0;
            }

            _failureCounts[pluginId]++;
            int count = _failureCounts[pluginId];

            _logger.LogWarning(ex, "Plugin crashed ({Count}/{MaxFailures})", count, MaxFailures);

            if (count >= MaxFailures)
            {
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

        private Dictionary<string, object> GetDefaultSettings(IPluginConfigurable configurable)
        {
            var defaultSettings = new Dictionary<string, object>();

            try
            {
                var definitions = configurable.GetSettingsDefinition();
                foreach (var def in definitions)
                {
                    if (def.DefaultValue != null)
                    {
                        defaultSettings[def.Key] = def.DefaultValue;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginRegistry] Failed to get default settings");
            }

            return defaultSettings;
        }
    }
}
