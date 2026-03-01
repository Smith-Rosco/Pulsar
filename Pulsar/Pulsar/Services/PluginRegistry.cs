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
            // [New] Resolve ConfigService
            _configService = _serviceProvider.GetService(typeof(Services.Interfaces.IConfigService)) as Services.Interfaces.IConfigService;

            _logger.LogInformation("[PluginRegistry] Loading plugins...");

            var plugins = _loader.LoadAll();
            
            foreach (var plugin in plugins)
            {
                if (_plugins.ContainsKey(plugin.Id))
                {
                    _logger.LogWarning("[PluginRegistry] ⚠️ Duplicate plugin ID: {PluginId}", plugin.Id);
                    continue;
                }

                _plugins[plugin.Id] = plugin;
                
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
                                _logger.LogInformation("[PluginRegistry] Applied settings for {PluginId}", plugin.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[PluginRegistry] Failed to apply settings for {PluginId}", plugin.Id);
                            }
                        }
                        
                        // [New] Call OnEnableAsync for lifecycle-aware plugins
                        if (plugin is IPluginLifecycle lifecycle && profile.Enabled)
                        {
                            try
                            {
                                await lifecycle.OnEnableAsync();
                                _logger.LogInformation("[PluginRegistry] Called OnEnableAsync for {PluginId}", plugin.Id);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "[PluginRegistry] OnEnableAsync failed for {PluginId}", plugin.Id);
                            }
                        }
                    }
                }

                _logger.LogInformation("[PluginRegistry] ✓ Registered plugin: {PluginName} ({PluginId})", plugin.DisplayName, plugin.Id);
            }

            _logger.LogInformation("[PluginRegistry] Loaded {Count} plugins", _plugins.Count);
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
        /// 执行插件动作 (含熔断保护)
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
                _logger.LogError("[PluginRegistry] ❌ Plugin not found: {PluginId}", pluginId);
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
                            _logger.LogWarning("[PluginRegistry] 🛑 Plugin is disabled by user: {PluginId}", pluginId);
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
                        _logger.LogWarning("[PluginRegistry] 🛡️ Circuit Open: {PluginId} is disabled for {Remaining}s", pluginId, remaining);
                        return PluginResult.Error($"Plugin disabled for safety. Try again in {remaining}s.");
                    }

                    _brokenCircuits.Remove(pluginId);
                    _logger.LogInformation("[PluginRegistry] 🛡️ Circuit Half-Open: Retrying {PluginId}...", pluginId);
                }
            }

            // [New] 开始计时
            var stopwatch = Stopwatch.StartNew();
            bool success = false;
            Exception? exception = null;

            try
            {
                _logger.LogDebug("[PluginRegistry] Executing: {PluginId}.{Action}", pluginId, action);
                var result = await plugin.ExecuteAsync(action, args, context);
                
                success = result.Success;

                // Extension plugin succeeded - reset crash counters.
                if (tier == PluginTier.Extension && _failureCounts.ContainsKey(pluginId))
                {
                    _failureCounts.Remove(pluginId);
                }

                if (result.Success)
                {
                    _logger.LogInformation("[PluginRegistry] ✓ Success: {Message}", result.Message ?? "OK");
                }
                else
                {
                    _logger.LogWarning("[PluginRegistry] ❌ Failed (Logic): {Message}", result.Message ?? "Unknown error");
                    
                    // [New] Handle Critical severity errors for Extension plugins
                    if (tier == PluginTier.Extension && result.Severity == PluginErrorSeverity.Critical)
                    {
                        _logger.LogWarning("[PluginRegistry] Critical error detected, counting towards circuit breaker");
                        HandlePluginCrash(pluginId, new InvalidOperationException(result.Message ?? "Critical plugin error"));
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                exception = ex;
                success = false;
                _logger.LogError(ex, "[PluginRegistry] ❌ Exception in plugin {PluginId}", pluginId);

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

                // [New] 记录日志
                if (success)
                {
                    _logService?.Log(pluginId, PluginLogLevel.Info,
                        $"Executed action '{action}' successfully",
                        null, action, args as Dictionary<string, string>, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    _logService?.Log(pluginId, PluginLogLevel.Error,
                        $"Failed to execute action '{action}'",
                        exception, action, args as Dictionary<string, string>, stopwatch.ElapsedMilliseconds);
                }
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

            _logger.LogWarning("[PluginRegistry] ⚠️ Plugin {PluginId} crashed ({Count}/{MaxFailures})", pluginId, count, MaxFailures);

            if (count >= MaxFailures)
            {
                // 触发熔断
                _brokenCircuits[pluginId] = DateTime.UtcNow;
                _failureCounts.Remove(pluginId); // 重置计数，等待冷却后重试
                _logger.LogError("[PluginRegistry] 💥 Circuit Breaker Tripped! {PluginId} disabled for {Timeout}s", pluginId, ResetTimeout.TotalSeconds);
                
                // [New] 记录 Circuit Breaker 触发
                _healthMonitor?.RecordCircuitBreakerTrip(pluginId);
                _logService?.Log(pluginId, PluginLogLevel.Critical,
                    $"Circuit Breaker triggered - plugin temporarily disabled for {ResetTimeout.TotalSeconds}s");

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
                _logger.LogInformation("[PluginRegistry] Plugin {PluginId} state changed to {State}", pluginId, enabled ? "Enabled" : "Disabled");
                
                // [New] Call lifecycle hooks
                if (plugin is IPluginLifecycle lifecycle)
                {
                    try
                    {
                        if (enabled)
                        {
                            await lifecycle.OnEnableAsync();
                            _logger.LogInformation("[PluginRegistry] Called OnEnableAsync for {PluginId}", pluginId);
                        }
                        else
                        {
                            await lifecycle.OnDisableAsync();
                            _logger.LogInformation("[PluginRegistry] Called OnDisableAsync for {PluginId}", pluginId);
                        }
                    }
                    catch (Exception ex)
                    {
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
            _logger.LogInformation("[PluginRegistry] Unloading all plugins...");
            
            foreach (var plugin in _plugins.Values)
            {
                if (plugin is IPluginLifecycle lifecycle)
                {
                    try
                    {
                        await lifecycle.OnUnloadAsync();
                        _logger.LogInformation("[PluginRegistry] Called OnUnloadAsync for {PluginId}", plugin.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[PluginRegistry] OnUnloadAsync failed for {PluginId}", plugin.Id);
                    }
                }
            }
            
            _logger.LogInformation("[PluginRegistry] All plugins unloaded");
        }
    }
}
