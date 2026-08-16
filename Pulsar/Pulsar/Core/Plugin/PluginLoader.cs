using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
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
        private readonly PluginFactory _pluginFactory;
        private readonly object _discoveryLock = new();
        private DiscoveryCache? _fullDiscoveryCache;
        private DiscoveryCache? _coreDiscoveryCache;

        public PluginLoader(IServiceProvider services, string pluginDir)
        {
            _services = services;
            _pluginDirectory = pluginDir;
            _logger = services.GetService(typeof(ILogger<PluginLoader>)) as ILogger<PluginLoader>;
            _metadataRegistry = services.GetService(typeof(IPluginMetadataRegistry)) as IPluginMetadataRegistry;
            _pluginFactory = new PluginFactory(services);
        }

        public virtual List<PluginDescriptor> DiscoverDescriptors(bool includeCore, bool includeExtensions, bool analyzeDependencies)
        {
            var cachedDescriptors = TryGetCachedDescriptors(includeCore, includeExtensions, analyzeDependencies);
            if (cachedDescriptors != null)
            {
                return cachedDescriptors;
            }

            var descriptors = new List<PluginDescriptor>();

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

            CacheDescriptors(includeCore, includeExtensions, analyzeDependencies, descriptors);
            return descriptors;
        }

        public virtual IPulsarPlugin ActivatePlugin(PluginDescriptor descriptor)
        {
            var plugin = _pluginFactory.CreatePlugin(descriptor.ImplementationType);
            plugin.Initialize(_services);

            // External descriptors intentionally defer metadata discovery until
            // activation (constructors must not run before permission consent).
            if (descriptor.IsExternal && plugin is IPluginMetadataProvider metadataProvider)
            {
                _metadataRegistry?.Register(metadataProvider.GetMetadata());
            }

            _logger?.LogInformation("[PluginLoader] Activated plugin: {PluginId} ({DisplayName})", plugin.Id, plugin.DisplayName);
            return plugin;
        }

        public void InvalidateDiscoveryCache()
        {
            lock (_discoveryLock)
            {
                _coreDiscoveryCache = null;
                _fullDiscoveryCache = null;
            }
        }

        protected virtual void DiscoverBuiltinDescriptors(List<PluginDescriptor> descriptors, bool includeCore, bool includeExtensions)
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
                        var manifest = TryReadExternalManifest(folder);
                        if (manifest == null)
                        {
                            _logger?.LogWarning("[PluginLoader] External plugin folder has no valid manifest and was skipped: {Folder}", folder);
                            continue;
                        }

                        if (!IsManifestVersionCompatible(manifest, out var versionReason))
                        {
                            _logger?.LogWarning("[PluginLoader] Skipped external plugin {PluginId} from {Folder}: {Reason}", manifest.Id, folder, versionReason);
                            continue;
                        }

                        var dllFiles = Directory.GetFiles(folder, "*.dll");
                        if (dllFiles.Length == 0)
                        {
                            continue;
                        }

                        var pluginName = Path.GetFileName(folder);
                        var anchorDll = dllFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Equals(pluginName, StringComparison.OrdinalIgnoreCase))
                            ?? dllFiles.First();

                        var context = new PluginLoadContext(anchorDll, shimMap: null);

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
                                        if (!IsManifestEntryPointMatch(manifest, pluginType))
                                        {
                                            continue;
                                        }

                                        var descriptor = CreateExternalDescriptor(pluginType, manifest);
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

        internal static PluginManifest? TryReadExternalManifest(string folder)
        {
            var manifestPath = Path.Combine(folder, "plugin.manifest.json");
            if (!File.Exists(manifestPath))
            {
                manifestPath = Path.Combine(folder, "manifest.json");
            }

            if (!File.Exists(manifestPath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return string.IsNullOrWhiteSpace(manifest?.Id) ? null : manifest;
            }
            catch (Exception ex) when (ex is IOException or JsonException or NotSupportedException)
            {
                return null;
            }
        }

        internal static bool IsManifestVersionCompatible(PluginManifest manifest, out string reason)
        {
            reason = string.Empty;

            var hostVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
            if (!TryParseManifestVersion(manifest.MinPulsarVersion, out var minVersion))
            {
                reason = $"unsupported minPulsarVersion '{manifest.MinPulsarVersion}'.";
                return false;
            }

            if (hostVersion < minVersion)
            {
                reason = $"requires Pulsar >= {manifest.MinPulsarVersion}, host is {hostVersion}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(manifest.MaxPulsarVersion)
                && TryParseManifestVersion(manifest.MaxPulsarVersion, out var maxVersion)
                && hostVersion > maxVersion)
            {
                reason = $"requires Pulsar <= {manifest.MaxPulsarVersion}, host is {hostVersion}.";
                return false;
            }

            return true;
        }

        private static bool TryParseManifestVersion(
            string value,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Version? version)
        {
            if (Version.TryParse(value, out version))
            {
                return true;
            }

            // Semantic-version prerelease/build suffixes ("1.0.0-beta", "1.0.0+build")
            // are accepted by stripping the suffix for the compatibility check.
            var core = value.Split('+', 2)[0].Split('-', 2)[0];
            return Version.TryParse(core, out version);
        }

        private static bool IsManifestEntryPointMatch(PluginManifest manifest, Type pluginType)
        {
            if (string.IsNullOrWhiteSpace(manifest.EntryPoint))
            {
                return true;
            }

            return string.Equals(pluginType.FullName, manifest.EntryPoint, StringComparison.Ordinal);
        }

        /// <summary>
        /// Builds an external descriptor without instantiating the plugin type.
        /// Discovery-time instantiation would execute untrusted constructors
        /// before the user approves the package permissions; manifest data is
        /// authoritative until first activation.
        /// </summary>
        internal static PluginDescriptor CreateExternalDescriptor(Type pluginType, PluginManifest manifest)
        {
            if (manifest.IsCore || manifest.Tier == PluginTier.Core)
            {
                throw new PluginInstantiationException(
                    "External plugin packages cannot declare the Core tier.",
                    pluginType);
            }

            var metadata = new PluginMetadata
            {
                Id = manifest.Id,
                Display = new DisplayInfo
                {
                    Name = string.IsNullOrWhiteSpace(manifest.DisplayName) ? pluginType.Name : manifest.DisplayName,
                    Description = manifest.Description,
                    IconKey = string.IsNullOrWhiteSpace(manifest.Icon) ? "📦" : manifest.Icon,
                    Category = manifest.Tags.FirstOrDefault() ?? "External",
                    Version = manifest.Version,
                    Author = string.IsNullOrWhiteSpace(manifest.Author) ? "Unknown" : manifest.Author,
                    License = manifest.License,
                    DocumentationUrl = manifest.DocumentationUrl
                },
                Schema = null,
                UI = new UIHints
                {
                    Badge = "External",
                    AccentColor = "#7C5CFF",
                    ShowInQuickAccess = true,
                    SortOrder = 200
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string>(),
                    RequiresForegroundWindow = false,
                    Dependencies = manifest.Dependencies.Keys.ToList(),
                    CanDisable = true,
                    Tier = PluginTier.Extension,
                    MinPulsarVersion = manifest.MinPulsarVersion
                },
                Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
            };

            return new PluginDescriptor
            {
                Id = manifest.Id,
                DisplayName = string.IsNullOrWhiteSpace(manifest.DisplayName) ? pluginType.Name : manifest.DisplayName,
                Version = manifest.Version,
                Author = string.IsNullOrWhiteSpace(manifest.Author) ? "Unknown" : manifest.Author,
                Description = manifest.Description,
                Icon = string.IsNullOrWhiteSpace(manifest.Icon) ? "📦" : manifest.Icon,
                CanDisable = true,
                Tier = PluginTier.Extension,
                IsExternal = true,
                Permissions = manifest.Permissions,
                ImplementationType = pluginType,
                Dependencies = manifest.Dependencies.Keys.ToList(),
                Metadata = metadata,
                IsConfigurable = typeof(IPluginConfigurable).IsAssignableFrom(pluginType)
            };
        }

        private PluginDescriptor CreateDescriptor(
            Type pluginType,
            bool isExternal = false,
            IReadOnlyList<string>? permissions = null)
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
                IsExternal = isExternal,
                Permissions = permissions ?? Array.Empty<string>(),
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

        private List<PluginDescriptor>? TryGetCachedDescriptors(bool includeCore, bool includeExtensions, bool analyzeDependencies)
        {
            lock (_discoveryLock)
            {
                DiscoveryCache? cache = includeCore switch
                {
                    true when !includeExtensions && !analyzeDependencies => _coreDiscoveryCache,
                    true when includeExtensions && analyzeDependencies => _fullDiscoveryCache,
                    false when includeExtensions && analyzeDependencies => _fullDiscoveryCache,
                    _ => null
                };

                if (cache == null)
                {
                    return null;
                }

                _logger?.LogDebug("[PluginLoader] Reusing cached plugin descriptors ({Count})", cache.Descriptors.Count);
                return FilterDescriptors(cache.Descriptors, includeCore, includeExtensions);
            }
        }

        private void CacheDescriptors(bool includeCore, bool includeExtensions, bool analyzeDependencies, List<PluginDescriptor> descriptors)
        {
            lock (_discoveryLock)
            {
                var snapshot = descriptors.ToList();
                if (includeCore && !includeExtensions && !analyzeDependencies)
                {
                    _coreDiscoveryCache = new DiscoveryCache(snapshot);
                }

                if (includeExtensions && analyzeDependencies)
                {
                    _fullDiscoveryCache = new DiscoveryCache(snapshot);
                }
            }
        }

        private static List<PluginDescriptor> FilterDescriptors(IEnumerable<PluginDescriptor> descriptors, bool includeCore, bool includeExtensions)
        {
            return descriptors
                .Where(descriptor => ShouldInclude(descriptor, includeCore, includeExtensions))
                .ToList();
        }

        private sealed class DiscoveryCache
        {
            public DiscoveryCache(List<PluginDescriptor> descriptors)
            {
                Descriptors = descriptors;
            }

            public List<PluginDescriptor> Descriptors { get; }
        }
    }
}
