// [Path]: Pulsar/Pulsar/Core/Plugin/PluginLoader.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 插件加载器 - 负责从内置程序集和外部 DLL 加载插件
    /// </summary>
    public class PluginLoader
    {
        private readonly string _pluginDirectory;
        private readonly IServiceProvider _services;

        public PluginLoader(IServiceProvider services, string pluginDir)
        {
            _services = services;
            _pluginDirectory = pluginDir;
        }

        /// <summary>
        /// 加载所有插件 (内置 + 外部)
        /// </summary>
        public List<IPulsarPlugin> LoadAll()
        {
            var plugins = new List<IPulsarPlugin>();

            // 1. 加载内置插件 (当前程序集)
            try
            {
                var builtinPlugins = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(t => typeof(IPulsarPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .Select(t => (IPulsarPlugin)Activator.CreateInstance(t)!)
                    .ToList();
                
                plugins.AddRange(builtinPlugins);
                Debug.WriteLine($"[PluginLoader] Loaded {builtinPlugins.Count} builtin plugins");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginLoader] Error loading builtin plugins: {ex.Message}");
            }

            // 2. 加载外部插件 (Plugins/ 目录)
            if (Directory.Exists(_pluginDirectory))
            {
                try
                {
                    var dllFiles = Directory.GetFiles(_pluginDirectory, "*.dll", SearchOption.AllDirectories);
                    
                    foreach (var dllPath in dllFiles)
                    {
                        try
                        {
                            var assembly = Assembly.LoadFrom(dllPath);
                            var externalPlugins = assembly.GetTypes()
                                .Where(t => typeof(IPulsarPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                                .Select(t => (IPulsarPlugin)Activator.CreateInstance(t)!)
                                .ToList();
                            
                            plugins.AddRange(externalPlugins);
                            Debug.WriteLine($"[PluginLoader] Loaded {externalPlugins.Count} plugins from {Path.GetFileName(dllPath)}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PluginLoader] Failed to load {dllPath}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PluginLoader] Error scanning plugin directory: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"[PluginLoader] Plugin directory not found: {_pluginDirectory}");
            }

            // 3. 初始化所有插件
            foreach (var plugin in plugins)
            {
                try
                {
                    plugin.Initialize(_services);
                    Debug.WriteLine($"[PluginLoader] Initialized plugin: {plugin.Id} ({plugin.DisplayName})");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PluginLoader] Failed to initialize plugin {plugin.Id}: {ex.Message}");
                }
            }

            return plugins;
        }
    }
}
