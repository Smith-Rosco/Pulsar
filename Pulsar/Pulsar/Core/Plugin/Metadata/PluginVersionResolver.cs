using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace Pulsar.Core.Plugin.Metadata
{
    /// <summary>
    /// 插件版本解析器 - 处理语义化版本和依赖解析
    /// 
    /// 支持的版本范围格式:
    /// - "1.0.0" - 精确版本
    /// - "^1.0.0" - 兼容版本（>= 1.0.0 且 < 2.0.0）
    /// - "~1.0.0" - 补丁版本（>= 1.0.0 且 < 1.1.0）
    /// - ">= 1.0.0" - 最低版本
    /// - "1.0.0 - 2.0.0" - 版本范围
    /// - "*" - 任意版本
    /// </summary>
    public class PluginVersionResolver
    {
        private readonly Dictionary<string, List<PluginManifest>> _availableVersions = new();
        private readonly ILogger? _logger;

        public PluginVersionResolver(ILogger? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 注册可用的插件版本
        /// </summary>
        public void RegisterVersion(PluginManifest manifest)
        {
            if (string.IsNullOrEmpty(manifest.Id))
            {
                throw new ArgumentException("Manifest must have an Id", nameof(manifest));
            }

            if (!_availableVersions.ContainsKey(manifest.Id))
            {
                _availableVersions[manifest.Id] = new List<PluginManifest>();
            }

            // 检查是否已存在相同版本
            var existing = _availableVersions[manifest.Id]
                .FirstOrDefault(m => m.Version == manifest.Version);

            if (existing != null)
            {
                _logger?.LogWarning("[PluginVersionResolver] Version already registered: {PluginId} v{Version}", 
                    manifest.Id, manifest.Version);
                return;
            }

            _availableVersions[manifest.Id].Add(manifest);
            _logger?.LogDebug("[PluginVersionResolver] Registered version: {PluginId} v{Version}", 
                manifest.Id, manifest.Version);
        }

        /// <summary>
        /// 解析版本范围，返回最佳匹配的版本
        /// </summary>
        /// <param name="pluginId">插件 ID</param>
        /// <param name="versionRange">版本范围（如 "^1.0.0"）</param>
        /// <returns>匹配的清单，如果没有匹配则返回 null</returns>
        public PluginManifest? ResolveVersion(string pluginId, string versionRange)
        {
            if (!_availableVersions.TryGetValue(pluginId, out var versions))
            {
                _logger?.LogWarning("[PluginVersionResolver] No versions available for plugin: {PluginId}", pluginId);
                return null;
            }

            if (versions.Count == 0)
            {
                return null;
            }

            try
            {
                // 解析版本范围
                var range = ParseVersionRange(versionRange);

                // 过滤匹配的版本
                var matchingVersions = versions
                    .Where(m => IsVersionValid(m.Version) && range.Satisfies(NuGetVersion.Parse(m.Version)))
                    .OrderByDescending(m => NuGetVersion.Parse(m.Version))
                    .ToList();

                if (matchingVersions.Count == 0)
                {
                    _logger?.LogWarning("[PluginVersionResolver] No matching version found for {PluginId} {VersionRange}", 
                        pluginId, versionRange);
                    return null;
                }

                var best = matchingVersions.First();
                _logger?.LogDebug("[PluginVersionResolver] Resolved {PluginId} {VersionRange} -> v{Version}", 
                    pluginId, versionRange, best.Version);

                return best;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginVersionResolver] Failed to resolve version: {PluginId} {VersionRange}", 
                    pluginId, versionRange);
                return null;
            }
        }

        /// <summary>
        /// 检查插件是否与指定的 Pulsar 版本兼容
        /// </summary>
        public bool CheckCompatibility(PluginManifest manifest, string pulsarVersion)
        {
            try
            {
                var version = NuGetVersion.Parse(pulsarVersion);
                var minVersion = NuGetVersion.Parse(manifest.MinPulsarVersion);

                // 检查最低版本
                if (version < minVersion)
                {
                    _logger?.LogWarning("[PluginVersionResolver] Plugin {PluginId} requires Pulsar >= {MinVersion}, current: {CurrentVersion}",
                        manifest.Id, manifest.MinPulsarVersion, pulsarVersion);
                    return false;
                }

                // 检查最高版本（如果指定）
                if (!string.IsNullOrEmpty(manifest.MaxPulsarVersion))
                {
                    var maxVersion = NuGetVersion.Parse(manifest.MaxPulsarVersion);
                    if (version > maxVersion)
                    {
                        _logger?.LogWarning("[PluginVersionResolver] Plugin {PluginId} requires Pulsar <= {MaxVersion}, current: {CurrentVersion}",
                            manifest.Id, manifest.MaxPulsarVersion, pulsarVersion);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginVersionResolver] Failed to check compatibility for {PluginId}", manifest.Id);
                return false;
            }
        }

        /// <summary>
        /// 获取插件的所有可用版本
        /// </summary>
        public List<PluginManifest> GetAvailableVersions(string pluginId)
        {
            if (_availableVersions.TryGetValue(pluginId, out var versions))
            {
                return versions.OrderByDescending(m => NuGetVersion.Parse(m.Version)).ToList();
            }
            return new List<PluginManifest>();
        }

        /// <summary>
        /// 获取所有已注册的插件 ID
        /// </summary>
        public IEnumerable<string> GetRegisteredPluginIds()
        {
            return _availableVersions.Keys;
        }

        /// <summary>
        /// 解析版本范围字符串
        /// </summary>
        private VersionRange ParseVersionRange(string versionRange)
        {
            // 处理特殊格式
            if (versionRange == "*")
            {
                return VersionRange.All;
            }

            // 处理 npm 风格的版本范围
            if (versionRange.StartsWith("^"))
            {
                // ^1.2.3 -> >= 1.2.3 且 < 2.0.0
                var version = NuGetVersion.Parse(versionRange.Substring(1));
                var maxVersion = new NuGetVersion(version.Major + 1, 0, 0);
                return new VersionRange(version, true, maxVersion, false);
            }

            if (versionRange.StartsWith("~"))
            {
                // ~1.2.3 -> >= 1.2.3 且 < 1.3.0
                var version = NuGetVersion.Parse(versionRange.Substring(1));
                var maxVersion = new NuGetVersion(version.Major, version.Minor + 1, 0);
                return new VersionRange(version, true, maxVersion, false);
            }

            // 使用 NuGet 标准解析
            return VersionRange.Parse(versionRange);
        }

        /// <summary>
        /// 验证版本字符串是否有效
        /// </summary>
        private bool IsVersionValid(string version)
        {
            return NuGetVersion.TryParse(version, out _);
        }

        /// <summary>
        /// 解析依赖树（递归解析所有依赖）
        /// </summary>
        public List<PluginManifest> ResolveDependencyTree(PluginManifest rootManifest)
        {
            var resolved = new List<PluginManifest>();
            var visited = new HashSet<string>();

            ResolveDependencyTreeRecursive(rootManifest, resolved, visited);

            return resolved;
        }

        private void ResolveDependencyTreeRecursive(
            PluginManifest manifest, 
            List<PluginManifest> resolved, 
            HashSet<string> visited)
        {
            // 检测循环依赖
            if (visited.Contains(manifest.Id))
            {
                _logger?.LogWarning("[PluginVersionResolver] Circular dependency detected: {PluginId}", manifest.Id);
                return;
            }

            visited.Add(manifest.Id);

            // 解析依赖
            foreach (var dep in manifest.Dependencies)
            {
                var depManifest = ResolveVersion(dep.Key, dep.Value);
                if (depManifest == null)
                {
                    _logger?.LogError("[PluginVersionResolver] Failed to resolve dependency: {PluginId} requires {DependencyId} {VersionRange}",
                        manifest.Id, dep.Key, dep.Value);
                    throw new InvalidOperationException($"Missing dependency: {dep.Key} {dep.Value}");
                }

                // 递归解析依赖的依赖
                ResolveDependencyTreeRecursive(depManifest, resolved, visited);
            }

            // 添加到结果（依赖优先）
            if (!resolved.Any(m => m.Id == manifest.Id))
            {
                resolved.Add(manifest);
            }
        }

        /// <summary>
        /// 清除所有已注册的版本
        /// </summary>
        public void Clear()
        {
            _availableVersions.Clear();
            _logger?.LogDebug("[PluginVersionResolver] Cleared all registered versions");
        }
    }
}
