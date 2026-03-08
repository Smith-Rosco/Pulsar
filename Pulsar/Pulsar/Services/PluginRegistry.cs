// [Path]: Pulsar/Pulsar/Services/PluginRegistry.cs

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;
using Pulsar.Models;

namespace Pulsar.Services
{
    /// <summary>
    /// 插件注册中心 - 管理所有插件的生命周期和调度
    /// </summary>
    public class PluginRegistry
    {
        private readonly Dictionary<string, IPulsarPlugin> _plugins = new();
        
        // [Safety] Circuit Breaker State
        private readonly Dictionary<string, int> _failureCounts = new();
        private readonly Dictionary<string, DateTime> _brokenCircuits = new();
        private const int MaxFailures = 3;
        private readonly TimeSpan ResetTimeout = TimeSpan.FromMinutes(1);

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PluginRegistry> _logger;
        private readonly PluginLoader _loader;
        private readonly Services.Interfaces.ITrayService? _trayService;
        private Services.Interfaces.IConfigService? _configService; // [New]
        private readonly Services.Interfaces.IPluginUsageTracker? _usageTracker; // [New]
        private readonly Services.Interfaces.IPluginHealthMonitor? _healthMonitor; // [New]
        private readonly Services.Interfaces.IPluginLogService? _logService; // [New]

        public PluginRegistry(IServiceProvider serviceProvider, ILogger<PluginRegistry> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _trayService = _serviceProvider.GetService(typeof(Services.Interfaces.ITrayService)) as Services.Interfaces.ITrayService;
            _configService = _serviceProvider.GetService(typeof(Services.Interfaces.IConfigService)) as Services.Interfaces.IConfigService;
            _usageTracker = _serviceProvider.GetService(typeof(Services.Interfaces.IPluginUsageTracker)) as Services.Interfaces.IPluginUsageTracker;
            _healthMonitor = _serviceProvider.GetService(typeof(Services.Interfaces.IPluginHealthMonitor)) as Services.Interfaces.IPluginHealthMonitor;
            _logService = _serviceProvider.GetService(typeof(Services.Interfaces.IPluginLogService)) as Services.Interfaces.IPluginLogService;
            
            // 初始化插件加载器
            string pluginDir = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "Plugins"
            );
            _loader = new PluginLoader(serviceProvider, pluginDir);
        }

        /// <summary>
        /// 加载并初始化所有插件
        /// </summary>
        public async Task LoadAllAsync()
        {
            // [Logging] Single startup log instead of per-plugin logs
            _logger.LogInformation("[PluginRegistry] Loading plugins...");

            var plugins = _loader.LoadAll();
            var loadedPluginNames = new List<string>();
            var failedPlugins = new List<string>();
            
            foreach (var plugin in plugins)
            {
                if (_plugins.ContainsKey(plugin.Id))
                {
                    _logger.LogWarning("[PluginRegistry] ⚠️ Duplicate plugin ID: {PluginId}", plugin.Id);
                    continue;
                }

                _plugins[plugin.Id] = plugin;
                bool loadSuccess = true;
                
                // [New] Ensure plugin has a profile entry
                if (_configService != null)
                {
                    var config = _configService.Current;
                    if (config != null)
                    {
                        if (!config.Plugins.TryGetValue(plugin.Id, out var profile))
                        {
                            profile = new Models.PluginProfile { Enabled = true };
                            config.Plugins[plugin.Id] = profile;
                        }
                        
                        // [Fix] Apply saved settings on startup with validation
                        if (plugin is IPluginConfigurable configurable)
                        {
                            try
                            {
                                // Validate settings before applying
                                var validationResult = configurable.ValidateSettings(profile.Config);
                                if (!validationResult.IsValid)
                                {
                                    _logger.LogWarning("[PluginRegistry] Invalid settings for {PluginId}: {Errors}", 
                                        plugin.Id, string.Join(", ", validationResult.Errors));
                                }
                                
                                configurable.UpdateSettings(profile.Config);
                                // [Logging] Downgraded to Debug - happens for every plugin
                                _logger.LogDebug("[PluginRegistry] Applied settings for {PluginId}", plugin.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[PluginRegistry] Failed to apply settings for {PluginId}", plugin.Id);
                                loadSuccess = false;
                            }
                        }
                        
                        // [New] Call OnEnableAsync for lifecycle-aware plugins
                        if (plugin is IPluginLifecycle lifecycle && profile.Enabled)
                        {
                            try
                            {
                                await lifecycle.OnEnableAsync();
                                // [Logging] Downgraded to Debug - happens for every plugin
                                _logger.LogDebug("[PluginRegistry] Called OnEnableAsync for {PluginId}", plugin.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[PluginRegistry] OnEnableAsync failed for {PluginId}", plugin.Id);
                                loadSuccess = false;
                            }
                        }
                    }
                }

                // [Logging] Collect plugin names for summary instead of logging each one
                if (loadSuccess)
                {
                    loadedPluginNames.Add(plugin.DisplayName);
                }
                else
                {
                    failedPlugins.Add(plugin.DisplayName);
                }
            }

            // [Logging] Single summary log with all loaded plugins
            _logger.LogInformation("[PluginRegistry] Loaded {Count} plugins: {PluginList}", 
                _plugins.Count, string.Join(", ", loadedPluginNames));
            
            if (failedPlugins.Count > 0)
            {
                _logger.LogWarning("[PluginRegistry] {Count} plugins failed to initialize: {FailedList}", 
                    failedPlugins.Count, string.Join(", ", failedPlugins));
            }
        }

        /// <summary>
        /// 根据插件 ID 获取插件实例
        /// </summary>
        public IPulsarPlugin? GetPlugin(string pluginId)
        {
            _plugins.TryGetValue(pluginId, out var plugin);
            return plugin;
        }

        /// <summary>
        /// 执行插件动作 (含熔断保护 + 统一日志上下文)
        /// </summary>
        public async Task<PluginResult> ExecuteAsync(
            string pluginId,
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            var plugin = GetPlugin(pluginId);
            
            if (plugin == null)
            {
                _logger.LogError("Plugin not found: {PluginId}", pluginId);
                return PluginResult.Error($"Plugin not found: {pluginId}");
            }

            var tier = GetTier(plugin);

            // [Rule] Core plugins cannot be disabled by user config.
            if (tier == PluginTier.Extension)
            {
                // Check Enabled State from Config
                if (_configService != null && _configService.Current != null)
                {
                    if (_configService.Current.Plugins.TryGetValue(pluginId, out var profile))
                    {
                        if (!profile.Enabled)
                        {
                            _logger.LogWarning("Plugin is disabled by user: {PluginId}", pluginId);
                            return PluginResult.Error("Plugin is disabled.");
                        }
                    }
                }

                // Circuit Breaker only applies to Extension plugins.
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

            // [Unified Logging] 创建插件执行上下文作用域
            using var executionScope = PluginExecutionContext.BeginScope(
                pluginId, 
                action, 
                targetProcessName: context.TargetProcessName
            );

            var stopwatch = Stopwatch.StartNew();
            bool success = false;
            Exception? exception = null;

            try
            {
                // [Logging] Removed debug log - too frequent, low value
                var result = await plugin.ExecuteAsync(action, args, context);
                
                success = result.Success;

                if (result.Success)
                {
                    // [Logging] Downgraded to Debug - happens frequently for every plugin execution
                    _logger.LogDebug("Plugin execution succeeded: {Message}", result.Message ?? "OK");
                    
                    // Extension plugin succeeded - reset crash counters.
                    if (tier == PluginTier.Extension && _failureCounts.ContainsKey(pluginId))
                    {
                        _failureCounts.Remove(pluginId);
                        // [Logging] Keep Debug - useful for troubleshooting circuit breaker
                        _logger.LogDebug("Reset failure count for {PluginId} after successful execution", pluginId);
                    }
                }
                else
                {
                    // [Logging] Keep Warning - indicates problem
                    _logger.LogWarning("Plugin execution failed (logic error): {Message}", result.Message ?? "Unknown error");
                    
                    // [New] Handle Critical severity errors for Extension plugins
                    if (tier == PluginTier.Extension && result.Severity == PluginErrorSeverity.Critical)
                    {
                        // [Logging] Keep Warning - important for circuit breaker
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

                // Only Extension plugins are isolated with Circuit Breaker.
                if (tier == PluginTier.Extension)
                {
                    HandlePluginCrash(pluginId, ex);
                }

                return PluginResult.Error($"Plugin execution failed: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();

                // [New] 记录统计数据
                _usageTracker?.RecordExecution(pluginId, success, stopwatch.ElapsedMilliseconds, context.TargetProcessName);

                // [New] 记录健康监控
                if (success)
                {
                    _healthMonitor?.RecordSuccess(pluginId);
                }
                else if (exception != null)
                {
                    _healthMonitor?.RecordError(pluginId, exception, action);
                }

                // [Deprecated] PluginLogService 不再写入日志，仅作为查询层
                // 所有日志已通过 Serilog + PluginContextEnricher 自动记录
            }
        }

        private static PluginTier GetTier(IPulsarPlugin plugin)
        {
            if (plugin is IPluginTiered tiered)
            {
                return tiered.Tier;
            }

            // Backwards-compatible default:
            // - If a plugin cannot be disabled, treat it as Core.
            // - Otherwise treat it as Extension.
            return plugin.CanDisable ? PluginTier.Extension : PluginTier.Core;
        }

        private void HandlePluginCrash(string pluginId, Exception ex)
        {
            if (!_failureCounts.ContainsKey(pluginId))
            {
                _failureCounts[pluginId] = 0;
            }
            
            _failureCounts[pluginId]++;
            int count = _failureCounts[pluginId];

            _logger.LogWarning("Plugin crashed ({Count}/{MaxFailures})", count, MaxFailures);

            if (count >= MaxFailures)
            {
                // 触发熔断
                _brokenCircuits[pluginId] = DateTime.UtcNow;
                _failureCounts.Remove(pluginId); // 重置计数，等待冷却后重试
                _logger.LogCritical("Circuit Breaker Tripped! Plugin temporarily disabled for {Timeout}s", ResetTimeout.TotalSeconds);
                
                // [New] 记录 Circuit Breaker 触发
                _healthMonitor?.RecordCircuitBreakerTrip(pluginId);

                // 发送系统通知告知用户插件已禁用
                _trayService?.ShowNotification(
                    "插件已自动禁用", 
                    $"插件 '{pluginId}' 因多次崩溃已被暂时禁用 {ResetTimeout.TotalSeconds} 秒，以保护主程序运行。", 
                    System.Windows.Forms.ToolTipIcon.Error
                );
            }
        }

        /// <summary>
        /// Manually enable or disable a plugin.
        /// </summary>
        public async Task SetPluginStateAsync(string pluginId, bool enabled)
        {
            if (_configService == null) return;

            // Core plugins cannot be disabled.
            var plugin = GetPlugin(pluginId);
            if (plugin == null) return;
            
            if (!plugin.CanDisable)
            {
                _logger.LogWarning("[PluginRegistry] Cannot disable core plugin: {PluginId}", pluginId);
                return;
            }
            
            var config = _configService.Current;
            if (config == null) return;

            if (!config.Plugins.TryGetValue(pluginId, out var profile))
            {
                profile = new Models.PluginProfile();
                config.Plugins[pluginId] = profile;
            }

            if (profile.Enabled != enabled)
            {
                profile.Enabled = enabled;
                // [Logging] Keep Information - important user action
                _logger.LogInformation("[PluginRegistry] Plugin {PluginId} state changed to {State}", pluginId, enabled ? "Enabled" : "Disabled");
                
                // [New] Call lifecycle hooks
                if (plugin is IPluginLifecycle lifecycle)
                {
                    try
                    {
                        if (enabled)
                        {
                            await lifecycle.OnEnableAsync();
                            // [Logging] Downgraded to Debug - internal operation
                            _logger.LogDebug("[PluginRegistry] Called OnEnableAsync for {PluginId}", pluginId);
                        }
                        else
                        {
                            await lifecycle.OnDisableAsync();
                            // [Logging] Downgraded to Debug - internal operation
                            _logger.LogDebug("[PluginRegistry] Called OnDisableAsync for {PluginId}", pluginId);
                        }
                    }
                    catch (Exception ex)
                    {
                        // [Logging] Keep Error - indicates problem
                        _logger.LogError(ex, "[PluginRegistry] Lifecycle hook failed for {PluginId}", pluginId);
                    }
                }
                
                await _configService.SaveAsync(config);
            }
        }

        /// <summary>
        /// Check if a plugin is enabled.
        /// </summary>
        public bool IsPluginEnabled(string pluginId)
        {
            var plugin = GetPlugin(pluginId);
            if (plugin != null && !plugin.CanDisable)
            {
                return true;
            }

            if (_configService?.Current?.Plugins.TryGetValue(pluginId, out var profile) == true)
            {
                return profile.Enabled;
            }
            return true; // Default to enabled if not configured
        }

        /// <summary>
        /// 获取所有已注册的插件
        /// </summary>
        public IEnumerable<IPulsarPlugin> GetAllPlugins()
        {
            return _plugins.Values;
        }

        /// <summary>
        /// 获取插件数量
        /// </summary>
        public int Count => _plugins.Count;

        /// <summary>
        /// Unload all plugins (called on application exit)
        /// </summary>
        public async Task UnloadAllAsync()
        {
            // [Logging] Keep Information - important shutdown event
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
                        // [Logging] Downgraded to Debug - happens for every plugin
                        _logger.LogDebug("[PluginRegistry] Called OnUnloadAsync for {PluginId}", plugin.Id);
                        unloadedCount++;
                    }
                    catch (Exception ex)
                    {
                        // [Logging] Keep Error - indicates problem during shutdown
                        _logger.LogError(ex, "[PluginRegistry] OnUnloadAsync failed for {PluginId}", plugin.Id);
                        failedCount++;
                    }
                }
            }
            
            // [Logging] Single summary log
            if (failedCount > 0)
            {
                _logger.LogWarning("[PluginRegistry] Unloaded {Unloaded} plugins ({Failed} failed)", unloadedCount, failedCount);
            }
            else
            {
                _logger.LogInformation("[PluginRegistry] All {Count} plugins unloaded successfully", unloadedCount);
            }
        }
    }
}
