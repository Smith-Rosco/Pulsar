using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin.Dependencies;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Services.Interfaces;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// Discovers plugin descriptors and activates plugin instances on demand.
    /// </summary>
    public class PluginLoader
    {
        private readonly string _pluginDirectory;
        private readonly IServiceProvider _services;
        private readonly ILogger<PluginLoader>? _logger;
        private readonly IPluginMetadataRegistry? _metadataRegistry;
        private readonly DependencyIsolationManager? _dependencyManager;
        private readonly PluginFactory _pluginFactory;
        private DependencyIsolationResult? _dependencyAnalysisResult;

        public PluginLoader(IServiceProvider services, string pluginDir)
        {
            _services = services;
            _pluginDirectory = pluginDir;
            _logger = services.GetService(typeof(ILogger<PluginLoader>)) as ILogger<PluginLoader>;
            _metadataRegistry = services.GetService(typeof(IPluginMetadataRegistry)) as IPluginMetadataRegistry;
            _pluginFactory = new PluginFactory(services);

            if (Directory.Exists(pluginDir))
            {
                _dependencyManager = new DependencyIsolationManager(pluginDir, null);
            }
        }

        public List<PluginDescriptor> DiscoverDescriptors(bool includeCore, bool includeExtensions, bool analyzeDependencies)
        {
            var descriptors = new List<PluginDescriptor>();

            if (includeExtensions && analyzeDependencies && _dependencyManager != null)
            {
                try
                {
                    _logger?.LogInformation("[PluginLoader] Analyzing plugin dependencies...");
                    _dependencyAnalysisResult = _dependencyManager.AnalyzeAndResolveAsync().GetAwaiter().GetResult();

                    if (_dependencyAnalysisResult.Success)
                    {
                        _logger?.LogInformation(
                            "[PluginLoader] Dependency analysis completed: {Conflicts} conflicts, {Shims} shims generated",
                            _dependencyAnalysisResult.Conflicts.Count,
                            _dependencyAnalysisResult.GeneratedShims.Count);

                        if (_dependencyAnalysisResult.HasCriticalConflicts)
                        {
                            _logger?.LogWarning("[PluginLoader] Critical dependency conflicts detected. Some plugins may fail to load.");
                            _logger?.LogWarning("[PluginLoader] Conflict report:\n{Report}", _dependencyManager.GenerateConflictReport());
                        }
                    }
                    else
                    {
                        _logger?.LogError("[PluginLoader] Dependency analysis failed: {Error}", _dependencyAnalysisResult.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[PluginLoader] Failed to analyze plugin dependencies");
                }
            }

            DiscoverBuiltinDescriptors(descriptors, includeCore, includeExtensions);

            if (includeExtensions)
            {
                DiscoverExternalDescriptors(descriptors, includeCore, includeExtensions);
            }

            try
            {
                descriptors = TopologicalSort(descriptors);
                _logger?.LogInformation("[PluginLoader] Sorted {Count} plugins by dependencies", descriptors.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginLoader] Failed to sort plugins by dependencies");
            }

            return descriptors;
        }

        public IPulsarPlugin ActivatePlugin(PluginDescriptor descriptor)
        {
            var plugin = _pluginFactory.CreatePlugin(descriptor.ImplementationType);
            plugin.Initialize(_services);
            _logger?.LogInformation("[PluginLoader] Activated plugin: {PluginId} ({DisplayName})", plugin.Id, plugin.DisplayName);
            return plugin;
        }

        private void DiscoverBuiltinDescriptors(List<PluginDescriptor> descriptors, bool includeCore, bool includeExtensions)
        {
            try
            {
                var pluginTypes = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(t => typeof(IPulsarPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();

                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        var descriptor = CreateDescriptor(pluginType);
                        if (!ShouldInclude(descriptor, includeCore, includeExtensions))
                        {
                            continue;
                        }

                        descriptors.Add(descriptor);
                        RegisterMetadata(descriptor);
                        _logger?.LogDebug("[PluginLoader] Discovered builtin plugin: {PluginType}", pluginType.Name);
                    }
                    catch (PluginInstantiationException ex)
                    {
                        _logger?.LogError(ex, "[PluginLoader] Failed to inspect builtin plugin: {PluginType}", pluginType.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginLoader] Error discovering builtin plugins");
            }
        }

        private void DiscoverExternalDescriptors(List<PluginDescriptor> descriptors, bool includeCore, bool includeExtensions)
        {
            if (!Directory.Exists(_pluginDirectory))
            {
                _logger?.LogInformation("[PluginLoader] Plugin directory not found: {PluginDirectory}", _pluginDirectory);
                return;
            }

            try
            {
                var pluginFolders = Directory.GetDirectories(_pluginDirectory);

                foreach (var folder in pluginFolders)
                {
                    try
                    {
                        var dllFiles = Directory.GetFiles(folder, "*.dll");
                        if (dllFiles.Length == 0)
                        {
                            continue;
                        }

                        var pluginName = Path.GetFileName(folder);
                        var anchorDll = dllFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                            ?? dllFiles.First();

                        Dictionary<string, string>? shimMap = null;
                        if (_dependencyAnalysisResult != null && _dependencyAnalysisResult.GeneratedShims.Any())
                        {
                            shimMap = new Dictionary<string, string>();
                            foreach (var shimPath in _dependencyAnalysisResult.GeneratedShims)
                            {
                                try
                                {
                                    var shimName = Path.GetFileNameWithoutExtension(shimPath);
                                    shimMap[shimName] = shimPath;
                                }
                                catch
                                {
                                }
                            }
                        }

                        var context = new PluginLoadContext(anchorDll, shimMap);

                        foreach (var dllPath in dllFiles)
                        {
                            try
                            {
                                var assembly = context.LoadFromAssemblyPath(dllPath);
                                var pluginTypes = assembly.GetTypes()
                                    .Where(t => typeof(IPulsarPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                                    .ToList();

                                var discoveredCount = 0;
                                foreach (var pluginType in pluginTypes)
                                {
                                    try
                                    {
                                        var descriptor = CreateDescriptor(pluginType);
                                        if (!ShouldInclude(descriptor, includeCore, includeExtensions))
                                        {
                                            continue;
                                        }

                                        descriptors.Add(descriptor);
                                        RegisterMetadata(descriptor);
                                        discoveredCount++;
                                        _logger?.LogDebug("[PluginLoader] Discovered external plugin: {PluginType}", pluginType.Name);
                                    }
                                    catch (PluginInstantiationException ex)
                                    {
                                        _logger?.LogError(ex, "[PluginLoader] Failed to inspect external plugin: {PluginType}", pluginType.Name);
                                    }
                                }

                                if (discoveredCount > 0)
                                {
                                    _logger?.LogInformation(
                                        "[PluginLoader] Discovered {Count} plugins from {Assembly} (Context: {Folder})",
                                        discoveredCount,
                                        Path.GetFileName(dllPath),
                                        folder);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "[PluginLoader] Failed to load assembly {AssemblyPath} in context {Folder}", dllPath, folder);
                            }
                        }
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

        private PluginDescriptor CreateDescriptor(Type pluginType)
        {
            var plugin = _pluginFactory.CreatePlugin(pluginType);
            var tier = plugin is IPluginTiered tiered ? tiered.Tier : (plugin.CanDisable ? PluginTier.Extension : PluginTier.Core);

            var metadata = plugin is IPluginMetadataProvider metadataProvider
                ? metadataProvider.GetMetadata()
                : CreateDefaultMetadata(plugin, tier);

            return new PluginDescriptor
            {
                Id = plugin.Id,
                DisplayName = plugin.DisplayName,
                Version = plugin.Version,
                Author = plugin.Author,
                Description = plugin.Description,
                Icon = plugin.Icon,
                CanDisable = plugin.CanDisable,
                Tier = tier,
                ImplementationType = pluginType,
                Dependencies = plugin.Dependencies.ToList(),
                Metadata = metadata,
                IsConfigurable = plugin is IPluginConfigurable
            };
        }

        private void RegisterMetadata(PluginDescriptor descriptor)
        {
            try
            {
                _metadataRegistry?.Register(descriptor.Metadata);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[PluginLoader] Failed to register metadata for plugin {PluginId}", descriptor.Id);
            }
        }

        private static bool ShouldInclude(PluginDescriptor descriptor, bool includeCore, bool includeExtensions)
        {
            return descriptor.Tier switch
            {
                PluginTier.Core => includeCore,
                PluginTier.Extension => includeExtensions,
                _ => includeExtensions
            };
        }

        private List<PluginDescriptor> TopologicalSort(List<PluginDescriptor> plugins)
        {
            var sorted = new List<PluginDescriptor>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();
            var pluginMap = plugins.ToDictionary(p => p.Id);

            void Visit(PluginDescriptor plugin)
            {
                if (visited.Contains(plugin.Id))
                {
                    return;
                }

                if (visiting.Contains(plugin.Id))
                {
                    _logger?.LogWarning("[PluginLoader] Circular dependency detected for plugin: {PluginId}", plugin.Id);
                    throw new InvalidOperationException($"Circular dependency detected for plugin: {plugin.Id}");
                }

                visiting.Add(plugin.Id);

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

        private PluginMetadata CreateDefaultMetadata(IPulsarPlugin plugin, PluginTier tier)
        {
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
                Schema = null,
                UI = new UIHints
                {
                    Badge = tier == PluginTier.Core ? "Core" : "Plugin",
                    AccentColor = tier == PluginTier.Core ? "#FF6B35" : "#4A90E2",
                    ShowInQuickAccess = true,
                    SortOrder = 100
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string>(),
                    RequiresForegroundWindow = false,
                    Dependencies = plugin.Dependencies.ToList(),
                    CanDisable = plugin.CanDisable,
                    Tier = tier,
                    MinPulsarVersion = plugin.MinPulsarVersion
                },
                Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
            };
        }

        public DependencyIsolationResult? GetDependencyAnalysisResult()
        {
            return _dependencyAnalysisResult;
        }

        public string GetDependencyConflictReport()
        {
            return _dependencyManager?.GenerateConflictReport() ?? "Dependency analysis not available.";
        }

        public bool HasCriticalDependencyConflicts()
        {
            return _dependencyManager?.HasCriticalConflicts() ?? false;
        }
    }
}
