using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Pulsar.Core.Plugin.Metadata
{
    /// <summary>
    /// 插件清单加载器 - 解析 plugin.manifest.json
    /// </summary>
    public class PluginManifestLoader
    {
        private readonly ILogger? _logger;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true
        };

        public PluginManifestLoader(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 从插件目录加载清单
        /// </summary>
        /// <param name="pluginPath">插件 DLL 路径</param>
        /// <returns>插件清单，如果不存在则返回 null</returns>
        public async Task<PluginManifest?> LoadFromPluginPathAsync(string pluginPath)
        {
            if (string.IsNullOrEmpty(pluginPath))
            {
                throw new ArgumentNullException(nameof(pluginPath));
            }

            // 1. 确定清单文件路径
            var pluginDir = Path.GetDirectoryName(pluginPath);
            if (string.IsNullOrEmpty(pluginDir))
            {
                _logger?.LogWarning("[PluginManifestLoader] Cannot determine plugin directory: {PluginPath}", pluginPath);
                return null;
            }

            var manifestPath = Path.Combine(pluginDir, "plugin.manifest.json");

            // 2. 检查文件是否存在
            if (!File.Exists(manifestPath))
            {
                _logger?.LogDebug("[PluginManifestLoader] Manifest not found: {ManifestPath}", manifestPath);
                return null;
            }

            // 3. 加载并解析
            return await LoadFromFileAsync(manifestPath);
        }

        /// <summary>
        /// 从文件加载清单
        /// </summary>
        public async Task<PluginManifest?> LoadFromFileAsync(string manifestPath)
        {
            try
            {
                _logger?.LogDebug("[PluginManifestLoader] Loading manifest from {ManifestPath}", manifestPath);

                var json = await File.ReadAllTextAsync(manifestPath);
                var manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);

                if (manifest == null)
                {
                    _logger?.LogWarning("[PluginManifestLoader] Failed to deserialize manifest: {ManifestPath}", manifestPath);
                    return null;
                }

                // 验证必填字段
                if (string.IsNullOrEmpty(manifest.Id))
                {
                    _logger?.LogError("[PluginManifestLoader] Manifest missing required field 'id': {ManifestPath}", manifestPath);
                    return null;
                }

                _logger?.LogInformation("[PluginManifestLoader] ✓ Loaded manifest: {PluginId} v{Version}", 
                    manifest.Id, manifest.Version);

                return manifest;
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, "[PluginManifestLoader] Invalid JSON in manifest: {ManifestPath}", manifestPath);
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginManifestLoader] Failed to load manifest: {ManifestPath}", manifestPath);
                return null;
            }
        }

        /// <summary>
        /// 保存清单到文件
        /// </summary>
        public async Task SaveToFileAsync(PluginManifest manifest, string manifestPath)
        {
            try
            {
                var json = JsonSerializer.Serialize(manifest, JsonOptions);
                await File.WriteAllTextAsync(manifestPath, json);
                
                _logger?.LogInformation("[PluginManifestLoader] ✓ Saved manifest: {ManifestPath}", manifestPath);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginManifestLoader] Failed to save manifest: {ManifestPath}", manifestPath);
                throw;
            }
        }

        /// <summary>
        /// 从插件实例生成默认清单（用于向后兼容）
        /// </summary>
        public PluginManifest CreateDefaultManifest(IPulsarPlugin plugin)
        {
            var tier = PluginTier.Extension;
            if (plugin is IPluginTiered tiered)
            {
                tier = tiered.Tier;
            }

            return new PluginManifest
            {
                Id = plugin.Id,
                Version = plugin.Version,
                DisplayName = plugin.DisplayName,
                Description = plugin.Description,
                Author = plugin.Author,
                License = plugin.License,
                Icon = plugin.Icon,
                MinPulsarVersion = plugin.MinPulsarVersion,
                DocumentationUrl = plugin.DocumentationUrl,
                IsCore = !plugin.CanDisable,
                Tier = tier,
                Tags = plugin.Tags.ToList(),
                Dependencies = plugin.Dependencies.ToDictionary(d => d, d => "*") // 任意版本
            };
        }
    }
}
