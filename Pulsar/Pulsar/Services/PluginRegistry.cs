// [Path]: Pulsar/Pulsar/Services/PluginRegistry.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly IServiceProvider _serviceProvider;
        private readonly PluginLoader _loader;

        public PluginRegistry(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            
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
            Debug.WriteLine("[PluginRegistry] Loading plugins...");

            var plugins = _loader.LoadAll();
            
            foreach (var plugin in plugins)
            {
                if (_plugins.ContainsKey(plugin.Id))
                {
                    Debug.WriteLine($"[PluginRegistry] ⚠️ Duplicate plugin ID: {plugin.Id}");
                    continue;
                }

                _plugins[plugin.Id] = plugin;
                Debug.WriteLine($"[PluginRegistry] ✓ Registered plugin: {plugin.DisplayName} ({plugin.Id})");
            }

            Debug.WriteLine($"[PluginRegistry] Loaded {_plugins.Count} plugins");
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
        /// 执行插件动作
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="action">动作名称</param>
        /// <param name="args">参数</param>
        /// <param name="context">上下文</param>
        /// <returns>插件执行结果</returns>
        public async Task<PluginResult> ExecuteAsync(
            string pluginId,
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            var plugin = GetPlugin(pluginId);
            
            if (plugin == null)
            {
                Debug.WriteLine($"[PluginRegistry] ❌ Plugin not found: {pluginId}");
                return PluginResult.Error($"Plugin not found: {pluginId}");
            }

            try
            {
                Debug.WriteLine($"[PluginRegistry] Executing: {pluginId}.{action}");
                var result = await plugin.ExecuteAsync(action, args, context);
                
                if (result.Success)
                {
                    Debug.WriteLine($"[PluginRegistry] ✓ Success: {result.Message ?? "OK"}");
                }
                else
                {
                    Debug.WriteLine($"[PluginRegistry] ❌ Failed: {result.Message ?? "Unknown error"}");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginRegistry] ❌ Exception: {ex.Message}");
                return PluginResult.Error($"Plugin execution failed: {ex.Message}");
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
