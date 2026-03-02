using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Versioning;
using Pulsar.Models;

namespace Pulsar.Services
{
    /// <summary>
    /// 增强的插件注册中心 - 支持热插拔、版本管理、运行时卸载
    /// 
    /// 新特性 (v2.0):
    /// - 运行时加载/卸载插件
    /// - 版本管理和依赖解析
    /// - 插件宿主隔离（PluginHost）
    /// - 内存安全（WeakReference + GC）
    /// - 热重载支持
    /// </summary>
    public class PluginRegistryV2
    {
        private readonly Dictionary<string, PluginHost> _hosts = new();
        private readonly Dictionary<string, int> _failureCounts = new();
        private readonly Dictionary<string, DateTime> _brokenCircuits = new();
        
        private const int MaxFailures = 3;
        private readonly TimeSpan ResetTimeout = TimeSpan.FromMinutes(1);

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PluginRegistryV2> _logger;
        private readonly Services.Interfaces.ITrayService? _trayService;
        private Services.Interfaces.IConfigService? _configService;
        private readonly Services.Interfaces.IPluginUsageTracker? _usageTracker;
        private readonly Services.Interfaces.IPluginHealthMonitor? _healthMonitor;
        
        private readonly PluginVersionResolver _versionResolver;
        private readonly PluginManifestLoader _manifestLoader;

        public PluginRegistryV2(IServiceProvider serviceProvider, ILogger<PluginRegistryV2> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _trayService = _serviceProvider.GetService(typeof(Services.Interfaces.ITrayService)) as Services.Interfaces.ITrayService;
            _usageTracker = _serviceProvider.GetService(typeof(Services.Interfaces.IPluginUsageTracker)) as Services.Interfaces.IPluginUsageTracker;
            _healthMonitor = _serviceProvider.GetService(typeof(Services.Interfaces.IPluginHealthMonitor)) as Services.Interfaces.IPluginHealthMonitor;
            
            _versionResolver = new PluginVersionResolver(logger);
            _manifestLoader = new PluginManifestLoader(logger);
        }

        /// <summary>
        /// 加载单个插件
        /// </summary>
        public async Task<bool> LoadPluginAsync(string pluginPath)
        {
            try
            {
                _logger.LogInformation("[PluginRegistryV2] Loading plugin from {PluginPath}", pluginPath);

                // 1. 创建插件宿主
                var host = new PluginHost(pluginPath, _serviceProvider, _logger);
                await host.LoadAsync();

                // 2. 检查是否已存在
                if (_hosts.ContainsKey(host.PluginId))
                {
                    _logger.LogWarning("[PluginRegistryV2] Plugin already loaded: {PluginId}", host.PluginId);
                    await host.UnloadAsync();
                    return false;
                }

                // 3. 加载清单（如果存在）
                var manifest = await _manifestLoader.LoadFromPluginPathAsync(pluginPath);
                if (manifest != null)
                {
                    _versionResolver.RegisterVersion(manifest);
                    _logger.LogDebug("[PluginRegistryV2] Loaded manifest for {PluginId}", host.PluginId);
                }

                // 4. 注册插件
                _hosts[host.PluginId] = host;

                // 5. 确保配置中有插件条目
                if (_configService != null)
                {
                    var config = _configService.Current;
                    if (config != null && !config.Plugins.ContainsKey(host.PluginId))
                    {
                        config.Plugins[host.PluginId] = new Models.PluginProfile { Enabled = true };
                    }
                }

                _logger.LogInformation("[PluginRegistryV2] ✓ Successfully loaded plugin: {PluginId} v{Version}", 
                    host.PluginId, host.Version);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginRegistryV2] Failed to load plugin: {PluginPath}", pluginPath);
                return false;
            }
        }

        /// <summary>
        /// 卸载插件
        /// </summary>
        public async Task<bool> UnloadPluginAsync(string pluginId)
        {
            if (!_hosts.TryGetValue(pluginId, out var host))
            {
                _logger.LogWarning("[PluginRegistryV2] Plugin not found: {PluginId}", pluginId);
                return false;
            }

            try
            {
                _logger.LogInformation("[PluginRegistryV2] Unloading plugin: {PluginId}", pluginId);
                
                await host.UnloadAsync();
                _hosts.Remove(pluginId);
                
                _logger.LogInformation("[PluginRegistryV2] ✓ Successfully unloaded plugin: {PluginId}", pluginId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginRegistryV2] Failed to unload plugin: {PluginId}", pluginId);
                return false;
            }
        }

        /// <summary>
        /// 热重载插件
        /// </summary>
        public async Task<bool> ReloadPluginAsync(string pluginId, string? newPluginPath = null)
        {
            if (!_hosts.TryGetValue(pluginId, out var host))
            {
                _logger.LogWarning("[PluginRegistryV2] Plugin not found for reload: {PluginId}", pluginId);
                return false;
            }

            try
            {
                _logger.LogInformation("[PluginRegistryV2] Reloading plugin: {PluginId}", pluginId);

                // 1. 获取原始路径（如果没有提供新路径）
                var pluginPath = newPluginPath ?? GetPluginPath(pluginId);
                if (string.IsNullOrEmpty(pluginPath))
                {
                    _logger.LogError("[PluginRegistryV2] Cannot determine plugin path for: {PluginId}", pluginId);
                    return false;
                }

                // 2. 卸载旧版本
                await UnloadPluginAsync(pluginId);

                // 3. 等待 GC 回收
                await Task.Delay(500);

                // 4. 加载新版本
                return await LoadPluginAsync(pluginPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PluginRegistryV2] Failed to reload plugin: {PluginId}", pluginId);
                return false;
            }
        }

        /// <summary>
        /// 执行插件动作（含熔断保护）
        /// </summary>
        public async Task<PluginResult> ExecuteAsync(
            string pluginId,
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (!_hosts.TryGetValue(pluginId, out var host))
            {
                _logger.LogError("[PluginRegistryV2] Plugin not found: {PluginId}", pluginId);
                return PluginResult.Error($"Plugin not found: {pluginId}");
            }

            // 检查插件是否存活
            if (!host.IsAlive)
            {
                _logger.LogError("[PluginRegistryV2] Plugin has been garbage collected: {PluginId}", pluginId);
                return PluginResult.Error("Plugin has been unloaded");
            }

            var tier = GetTier(host);

            // Extension 插件检查
            if (tier == PluginTier.Extension)
            {
                // 检查是否被用户禁用
                if (_configService?.Current?.Plugins.TryGetValue(pluginId, out var profile) == true)
                {
                    if (!profile.Enabled)
                    {
                        _logger.LogWarning("[PluginRegistryV2] Plugin is disabled: {PluginId}", pluginId);
                        return PluginResult.Error("Plugin is disabled.");
                    }
                }

                // 熔断器检查
                if (_brokenCircuits.TryGetValue(pluginId, out var breakTime))
                {
                    if (DateTime.UtcNow - breakTime < ResetTimeout)
                    {
                        var remaining = (int)(ResetTimeout - (DateTime.UtcNow - breakTime)).TotalSeconds;
                        _logger.LogWarning("[PluginRegistryV2] Circuit Open: {PluginId} disabled for {Remaining}s", 
                            pluginId, remaining);
                        return PluginResult.Error($"Plugin disabled for safety. Try again in {remaining}s.");
                    }

                    _brokenCircuits.Remove(pluginId);
                    _logger.LogInformation("[PluginRegistryV2] Circuit Half-Open: Retrying {PluginId}...", pluginId);
                }
            }

            // 创建执行上下文
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
                _logger.LogDebug("[PluginRegistryV2] Executing plugin action");
                var result = await host.ExecuteAsync(action, args, context);
                
                success = result.Success;

                // 成功执行 - 重置失败计数
                if (tier == PluginTier.Extension && _failureCounts.ContainsKey(pluginId))
                {
                    _failureCounts.Remove(pluginId);
                }

                if (result.Success)
                {
                    _logger.LogInformation("[PluginRegistryV2] Plugin execution succeeded: {Message}", 
                        result.Message ?? "OK");
                }
                else
                {
                    _logger.LogWarning("[PluginRegistryV2] Plugin execution failed: {Message}", 
                        result.Message ?? "Unknown error");
                    
                    // 处理 Critical 错误
                    if (tier == PluginTier.Extension && result.Severity == PluginErrorSeverity.Critical)
                    {
                        _logger.LogWarning("[PluginRegistryV2] Critical error detected");
                        HandlePluginCrash(pluginId, new InvalidOperationException(result.Message ?? "Critical error"));
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                exception = ex;
                success = false;
                _logger.LogError(ex, "[PluginRegistryV2] Plugin execution threw exception");

                // Extension 插件触发熔断器
                if (tier == PluginTier.Extension)
                {
                    HandlePluginCrash(pluginId, ex);
                }

                return PluginResult.Error($"Plugin execution failed: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();

                // 记录统计
                _usageTracker?.RecordExecution(pluginId, success, stopwatch.ElapsedMilliseconds, context.TargetProcessName);

                // 记录健康监控
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

        /// <summary>
        /// 获取插件宿主
        /// </summary>
        public PluginHost? GetPluginHost(string pluginId)
        {
            _hosts.TryGetValue(pluginId, out var host);
            return host;
        }

        /// <summary>
        /// 获取所有已加载的插件
        /// </summary>
        public IEnumerable<PluginHost> GetAllPluginHosts()
        {
            return _hosts.Values;
        }

        /// <summary>
        /// 获取插件数量
        /// </summary>
        public int Count => _hosts.Count;

        /// <summary>
        /// 检查插件是否已加载
        /// </summary>
        public bool IsPluginLoaded(string pluginId)
        {
            return _hosts.ContainsKey(pluginId) && _hosts[pluginId].IsAlive;
        }

        /// <summary>
        /// 卸载所有插件
        /// </summary>
        public async Task UnloadAllAsync()
        {
            _logger.LogInformation("[PluginRegistryV2] Unloading all plugins...");
            
            var tasks = _hosts.Values.Select(host => host.UnloadAsync()).ToList();
            await Task.WhenAll(tasks);
            
            _hosts.Clear();
            
            _logger.LogInformation("[PluginRegistryV2] All plugins unloaded");
        }

        /// <summary>
        /// 初始化配置服务引用
        /// </summary>
        public void InitializeConfigService()
        {
            _configService = _serviceProvider.GetService(typeof(Services.Interfaces.IConfigService)) 
                as Services.Interfaces.IConfigService;
        }

        private PluginTier GetTier(PluginHost host)
        {
            var plugin = host.GetPluginInstance();
            if (plugin is IPluginTiered tiered)
            {
                return tiered.Tier;
            }

            return plugin?.CanDisable == false ? PluginTier.Core : PluginTier.Extension;
        }

        private void HandlePluginCrash(string pluginId, Exception ex)
        {
            if (!_failureCounts.ContainsKey(pluginId))
            {
                _failureCounts[pluginId] = 0;
            }
            
            _failureCounts[pluginId]++;
            int count = _failureCounts[pluginId];

            _logger.LogWarning("[PluginRegistryV2] Plugin crashed ({Count}/{MaxFailures})", count, MaxFailures);

            if (count >= MaxFailures)
            {
                _brokenCircuits[pluginId] = DateTime.UtcNow;
                _failureCounts.Remove(pluginId);
                _logger.LogCritical("[PluginRegistryV2] Circuit Breaker Tripped! Plugin disabled for {Timeout}s", 
                    ResetTimeout.TotalSeconds);
                
                _healthMonitor?.RecordCircuitBreakerTrip(pluginId);

                _trayService?.ShowNotification(
                    "插件已自动禁用", 
                    $"插件 '{pluginId}' 因多次崩溃已被暂时禁用 {ResetTimeout.TotalSeconds} 秒。", 
                    System.Windows.Forms.ToolTipIcon.Error
                );
            }
        }

        private string? GetPluginPath(string pluginId)
        {
            // TODO: 从配置或缓存中获取插件路径
            // 临时实现：假设插件在标准目录
            var pluginDir = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, 
                "Plugins", 
                pluginId
            );
            
            var dllPath = System.IO.Path.Combine(pluginDir, $"{pluginId}.dll");
            return System.IO.File.Exists(dllPath) ? dllPath : null;
        }
    }
}
