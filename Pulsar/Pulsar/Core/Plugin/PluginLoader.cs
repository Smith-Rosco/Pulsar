using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 插件加载器 - 负责从内置程序集和外部 DLL 加载插件 (支持隔离加载)
    /// </summary>
    public class PluginLoader
    {
        private readonly string _pluginDirectory;
        private readonly IServiceProvider _services;
        private readonly ILogger<PluginLoader>? _logger;

        public PluginLoader(IServiceProvider services, string pluginDir)
        {
            _services = services;
            _pluginDirectory = pluginDir;
            _logger = services.GetService(typeof(ILogger<PluginLoader>)) as ILogger<PluginLoader>;
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
                _logger?.LogInformation("[PluginLoader] Loaded {Count} builtin plugins", builtinPlugins.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginLoader] Error loading builtin plugins");
            }

            // 2. 加载外部插件 (Plugins/ 子目录)
            // 采用 "One Folder = One Context" 策略进行隔离
            if (Directory.Exists(_pluginDirectory))
            {
                try
                {
                    var pluginFolders = Directory.GetDirectories(_pluginDirectory);
                    
                    foreach (var folder in pluginFolders)
                    {
                        try
                        {
                            var dllFiles = Directory.GetFiles(folder, "*.dll");
                            if (dllFiles.Length == 0) continue;

                            // 策略: 优先使用与文件夹同名的 DLL 作为上下文锚点 (用于解析依赖)
                            // 如果没有同名 DLL，则使用找到的第一个 DLL
                            var pluginName = Path.GetFileName(folder);
                            var anchorDll = dllFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(pluginName, StringComparison.OrdinalIgnoreCase));
                            
                            if (string.IsNullOrEmpty(anchorDll))
                            {
                                anchorDll = dllFiles.First();
                            }

                            // 创建隔离上下文
                            var context = new PluginLoadContext(anchorDll);

                            foreach (var dllPath in dllFiles)
                            {
                                try
                                {
                                    // 通过上下文加载程序集
                                    var assembly = context.LoadFromAssemblyPath(dllPath);

                                    // 扫描实现了 IPulsarPlugin 的类型
                                    var foundPlugins = assembly.GetTypes()
                                        .Where(t => typeof(IPulsarPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                                        .Select(t => (IPulsarPlugin)Activator.CreateInstance(t)!)
                                        .ToList();

                                    if (foundPlugins.Any())
                                    {
                                        plugins.AddRange(foundPlugins);
                                        _logger?.LogInformation(
                                            "[PluginLoader] Loaded {Count} plugins from {Assembly} (Context: {Folder})",
                                            foundPlugins.Count,
                                            Path.GetFileName(dllPath),
                                            folder);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogWarning(ex, "[PluginLoader] Failed to load assembly {AssemblyPath} in context {Folder}", dllPath, folder);
                                }
                            }
                            
                            // 如果整个文件夹没找到插件，且支持卸载，上下文会被 GC 回收 (需配合 WeakRef，此处暂略)
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "[PluginLoader] Error processing plugin folder {Folder}", folder);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[PluginLoader] Error scanning plugin directory");
                }
            }
            else
            {
                _logger?.LogInformation("[PluginLoader] Plugin directory not found: {PluginDirectory}", _pluginDirectory);
            }

            // 3. 初始化所有插件
            foreach (var plugin in plugins)
            {
                try
                {
                    plugin.Initialize(_services);
                    _logger?.LogInformation("[PluginLoader] Initialized plugin: {PluginId} ({DisplayName})", plugin.Id, plugin.DisplayName);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[PluginLoader] Failed to initialize plugin {PluginId}", plugin.Id);
                }
            }

            return plugins;
        }
    }
}
