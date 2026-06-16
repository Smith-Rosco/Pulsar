using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Core.Plugin.Metadata;

namespace Pulsar.Services
{
    /// <summary>
    /// 扫描本地已安装的外部插件
    /// </summary>
    public class LocalPluginScanner
    {
        private readonly string _pluginDirectory;
        private readonly ILogger<LocalPluginScanner>? _logger;

        public LocalPluginScanner(string pluginDirectory, ILogger<LocalPluginScanner>? logger = null)
        {
            _pluginDirectory = pluginDirectory;
            _logger = logger;
        }

        /// <summary>
        /// 扫描已安装的外部插件
        /// </summary>
        public List<PluginPackageInfo> ScanInstalledPlugins()
        {
            var installedPlugins = new List<PluginPackageInfo>();

            try
            {
                if (!Directory.Exists(_pluginDirectory))
                {
                    _logger?.LogDebug("[LocalPluginScanner] Plugin directory does not exist: {Directory}", _pluginDirectory);
                    return installedPlugins;
                }

                var pluginFolders = Directory.GetDirectories(_pluginDirectory);
                _logger?.LogDebug("[LocalPluginScanner] Found {Count} plugin folders", pluginFolders.Length);

                foreach (var folder in pluginFolders)
                {
                    try
                    {
                        // Try plugin.manifest.json first (new format), then manifest.json (legacy)
                        var manifestPath = Path.Combine(folder, "plugin.manifest.json");
                        if (!File.Exists(manifestPath))
                        {
                            manifestPath = Path.Combine(folder, "manifest.json");
                        }

                        if (!File.Exists(manifestPath))
                        {
                            _logger?.LogWarning("[LocalPluginScanner] No manifest file found in {Folder}", folder);
                            continue;
                        }

                        var manifestJson = File.ReadAllText(manifestPath);
                        var manifest = JsonSerializer.Deserialize<PluginManifest>(manifestJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (manifest == null)
                        {
                            _logger?.LogWarning("[LocalPluginScanner] Failed to parse manifest in {Folder}", folder);
                            continue;
                        }

                        var pluginInfo = new PluginPackageInfo
                        {
                            Id = manifest.Id ?? Path.GetFileName(folder),
                            Name = manifest.DisplayName ?? "Unknown Plugin",
                            Version = manifest.Version ?? "1.0.0",
                            Description = manifest.Description ?? string.Empty,
                            Author = manifest.Author ?? "Unknown",
                            Icon = manifest.Icon ?? "📦",
                            IsInstalled = true,
                            InstalledVersion = manifest.Version,
                            LocalPath = folder,
                            Tags = manifest.Tags ?? new List<string>()
                        };

                        installedPlugins.Add(pluginInfo);
                        _logger?.LogDebug("[LocalPluginScanner] Found plugin: {Name} v{Version}", pluginInfo.Name, pluginInfo.Version);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "[LocalPluginScanner] Failed to scan plugin folder: {Folder}", folder);
                    }
                }

                _logger?.LogInformation("[LocalPluginScanner] Scanned {Count} installed plugins", installedPlugins.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[LocalPluginScanner] Failed to scan plugin directory: {Directory}", _pluginDirectory);
            }

            return installedPlugins;
        }
    }
}
