using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Services.Interfaces;

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
        private readonly IPluginMetadataRegistry? _metadataRegistry;

        public PluginLoader(IServiceProvider services, string pluginDir)
        {
            _services = services;
            _pluginDirectory = pluginDir;
            _logger = services.GetService(typeof(ILogger<PluginLoader>)) as ILogger<PluginLoader>;
            _metadataRegistry = services.GetService(typeof(IPluginMetadataRegistry)) as IPluginMetadataRegistry;
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

            // 3. Sort plugins by dependencies (topological sort)
            try
            {
                plugins = TopologicalSort(plugins);
                _logger?.LogInformation("[PluginLoader] Sorted {Count} plugins by dependencies", plugins.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginLoader] Failed to sort plugins by dependencies");
            }

            // 4. Collect metadata from plugins
            if (_metadataRegistry != null)
            {
                foreach (var plugin in plugins)
                {
                    try
                    {
                        if (plugin is IPluginMetadataProvider metadataProvider)
                        {
                            var metadata = metadataProvider.GetMetadata();
                            _metadataRegistry.Register(metadata);
                            _logger?.LogDebug("[PluginLoader] Registered metadata for plugin: {PluginId}", plugin.Id);
                        }
                        else
                        {
                            // Create default metadata from IPulsarPlugin properties
                            var defaultMetadata = CreateDefaultMetadata(plugin);
                            _metadataRegistry.Register(defaultMetadata);
                            _logger?.LogDebug("[PluginLoader] Created default metadata for plugin: {PluginId}", plugin.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[PluginLoader] Failed to collect metadata for plugin {PluginId}", plugin.Id);
                    }
                }
            }

            // 5. Initialize all plugins
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

        /// <summary>
        /// Topological sort plugins by dependencies
        /// </summary>
        private List<IPulsarPlugin> TopologicalSort(List<IPulsarPlugin> plugins)
        {
            var sorted = new List<IPulsarPlugin>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();
            var pluginMap = plugins.ToDictionary(p => p.Id);

            void Visit(IPulsarPlugin plugin)
            {
                if (visited.Contains(plugin.Id))
                    return;

                if (visiting.Contains(plugin.Id))
                {
                    _logger?.LogWarning("[PluginLoader] Circular dependency detected for plugin: {PluginId}", plugin.Id);
                    throw new InvalidOperationException($"Circular dependency detected for plugin: {plugin.Id}");
                }

                visiting.Add(plugin.Id);

                // Visit dependencies first
                foreach (var depId in plugin.Dependencies)
                {
                    if (pluginMap.TryGetValue(depId, out var dependency))
                    {
                        Visit(dependency);
                    }
                    else
                    {
                        _logger?.LogWarning("[PluginLoader] Missing dependency '{DependencyId}' for plugin '{PluginId}'", depId, plugin.Id);
                        throw new InvalidOperationException($"Missing dependency '{depId}' for plugin '{plugin.Id}'");
                    }
                }

                visiting.Remove(plugin.Id);
                visited.Add(plugin.Id);
                sorted.Add(plugin);
            }

            foreach (var plugin in plugins)
            {
                Visit(plugin);
            }

            return sorted;
        }

        /// <summary>
        /// Create default metadata from IPulsarPlugin properties (for plugins that don't implement IPluginMetadataProvider)
        /// </summary>
        private PluginMetadata CreateDefaultMetadata(IPulsarPlugin plugin)
        {
            var tier = PluginTier.Extension;
            if (plugin is IPluginTiered tiered)
            {
                tier = tiered.Tier;
            }

            return new PluginMetadata
            {
                Id = plugin.Id,
                Display = new DisplayInfo
                {
                    Name = plugin.DisplayName,
                    Description = plugin.Description,
                    IconKey = plugin.Icon,
                    Category = plugin.Tags.FirstOrDefault() ?? "General",
                    Version = plugin.Version,
                    Author = plugin.Author,
                    DocumentationUrl = plugin.DocumentationUrl,
                    License = plugin.License
                },
                Schema = null, // No schema for non-configurable plugins
                UI = new UIHints
                {
                    Badge = tier == PluginTier.Core ? "Core" : "Plugin",
                    AccentColor = tier == PluginTier.Core ? "#FF6B35" : "#4A90E2",
                    ShowInQuickAccess = true,
                    SortOrder = 100
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string>(), // Unknown without metadata
                    RequiresForegroundWindow = false,
                    Dependencies = plugin.Dependencies.ToList(),
                    CanDisable = plugin.CanDisable,
                    Tier = tier,
                    MinPulsarVersion = plugin.MinPulsarVersion
                }
            };
        }
    }
}
