// [Path]: Pulsar/Pulsar/Services/PluginRegistry.cs

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;

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

        public PluginRegistry(IServiceProvider serviceProvider, ILogger<PluginRegistry> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _trayService = _serviceProvider.GetService(typeof(Services.Interfaces.ITrayService)) as Services.Interfaces.ITrayService;
            
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
        public void LoadAll()
        {
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
            // 1. 检查熔断状态
            if (_brokenCircuits.TryGetValue(pluginId, out var breakTime))
            {
                if (DateTime.UtcNow - breakTime < ResetTimeout)
                {
                    // 熔断中
                    var remaining = (int)(ResetTimeout - (DateTime.UtcNow - breakTime)).TotalSeconds;
                    _logger.LogWarning("[PluginRegistry] 🛡️ Circuit Open: {PluginId} is disabled for {Remaining}s", pluginId, remaining);
                    return PluginResult.Error($"Plugin disabled for safety. Try again in {remaining}s.");
                }
                else
                {
                    // 冷却结束，进入半开状态 (Half-Open)
                    _brokenCircuits.Remove(pluginId);
                    _logger.LogInformation("[PluginRegistry] 🛡️ Circuit Half-Open: Retrying {PluginId}...", pluginId);
                }
            }

            var plugin = GetPlugin(pluginId);
            
            if (plugin == null)
            {
                _logger.LogError("[PluginRegistry] ❌ Plugin not found: {PluginId}", pluginId);
                return PluginResult.Error($"Plugin not found: {pluginId}");
            }

            try
            {
                _logger.LogDebug("[PluginRegistry] Executing: {PluginId}.{Action}", pluginId, action);
                var result = await plugin.ExecuteAsync(action, args, context);
                
                // 2. 执行成功 - 重置计数器
                if (_failureCounts.ContainsKey(pluginId))
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
                    // 注意：逻辑失败通常不计入熔断，只有崩溃才计入。
                    // 但如果插件持续返回逻辑错误，是否应该熔断？目前策略：仅异常熔断。
                }
                
                return result;
            }
            catch (Exception ex)
            {
                // 3. 发生异常 - 增加故障计数
                _logger.LogError(ex, "[PluginRegistry] ❌ Exception in plugin {PluginId}", pluginId);
                HandlePluginCrash(pluginId, ex);
                return PluginResult.Error($"Plugin execution failed: {ex.Message}");
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

            _logger.LogWarning("[PluginRegistry] ⚠️ Plugin {PluginId} crashed ({Count}/{MaxFailures})", pluginId, count, MaxFailures);

            if (count >= MaxFailures)
            {
                // 触发熔断
                _brokenCircuits[pluginId] = DateTime.UtcNow;
                _failureCounts.Remove(pluginId); // 重置计数，等待冷却后重试
                _logger.LogError("[PluginRegistry] 💥 Circuit Breaker Tripped! {PluginId} disabled for {Timeout}s", pluginId, ResetTimeout.TotalSeconds);
                
                // 发送系统通知告知用户插件已禁用
                _trayService?.ShowNotification(
                    "插件已自动禁用", 
                    $"插件 '{pluginId}' 因多次崩溃已被暂时禁用 {ResetTimeout.TotalSeconds} 秒，以保护主程序运行。", 
                    System.Windows.Forms.ToolTipIcon.Error
                );
            }
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
    }
}
