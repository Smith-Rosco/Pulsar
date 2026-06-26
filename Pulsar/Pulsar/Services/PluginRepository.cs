using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Models;

namespace Pulsar.Services
{
    /// <summary>
    /// 本地插件仓库 - 管理插件索引和版本存储
    /// </summary>
    [Obsolete("PluginRepository is deprecated. Use LocalPluginScanner for scanning installed plugins and PluginPackageManager for installation. Online plugin repository is not implemented.")]
    public class PluginRepository
    {
        private readonly string _repositoryPath;
        private readonly string _indexFilePath;
        private readonly ILogger<PluginRepository>? _logger;
        private Dictionary<string, List<PluginPackageInfo>> _packageIndex = new();
        private readonly SemaphoreSlim _indexLock = new(1, 1);

        public PluginRepository(string repositoryPath, ILogger<PluginRepository>? logger = null)
        {
            _repositoryPath = repositoryPath;
            _indexFilePath = Path.Combine(_repositoryPath, "index.json");
            _logger = logger;

            // 确保仓库目录存在
            if (!Directory.Exists(_repositoryPath))
            {
                Directory.CreateDirectory(_repositoryPath);
                _logger?.LogInformation("[PluginRepository] Created repository directory: {Path}", _repositoryPath);
            }
        }

        /// <summary>
        /// 初始化仓库（加载索引）
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await _indexLock.WaitAsync(cancellationToken);
            try
            {
                if (File.Exists(_indexFilePath))
                {
                    var json = await File.ReadAllTextAsync(_indexFilePath, cancellationToken);
                    var packages = JsonSerializer.Deserialize<List<PluginPackageInfo>>(json);

                    if (packages != null)
                    {
                        _packageIndex = packages
                            .GroupBy(p => p.Id)
                            .ToDictionary(g => g.Key, g => g.ToList());

                        _logger?.LogInformation("[PluginRepository] Loaded {Count} packages from index", packages.Count);
                    }
                }
                else
                {
                    _logger?.LogInformation("[PluginRepository] No index file found, starting with empty repository");
                    await SaveIndexAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginRepository] Failed to initialize repository");
                throw;
            }
            finally
            {
                _indexLock.Release();
            }
        }

        /// <summary>
        /// 获取所有插件包
        /// </summary>
        public List<PluginPackageInfo> GetAllPackages()
        {
            return _packageIndex.Values.SelectMany(v => v).ToList();
        }

        /// <summary>
        /// 获取指定插件的所有版本
        /// </summary>
        public List<PluginPackageInfo> GetPackageVersions(string pluginId)
        {
            if (_packageIndex.TryGetValue(pluginId, out var versions))
            {
                return versions.OrderByDescending(v => Version.Parse(v.Version)).ToList();
            }
            return new List<PluginPackageInfo>();
        }

        /// <summary>
        /// 获取指定插件的最新版本
        /// </summary>
        public PluginPackageInfo? GetLatestVersion(string pluginId)
        {
            var versions = GetPackageVersions(pluginId);
            return versions.FirstOrDefault();
        }

        /// <summary>
        /// 获取指定插件的特定版本
        /// </summary>
        public PluginPackageInfo? GetPackage(string pluginId, string version)
        {
            if (_packageIndex.TryGetValue(pluginId, out var versions))
            {
                return versions.FirstOrDefault(v => v.Version == version);
            }
            return null;
        }

        /// <summary>
        /// 添加或更新插件包
        /// </summary>
        public async Task AddOrUpdatePackageAsync(PluginPackageInfo package, CancellationToken cancellationToken = default)
        {
            await _indexLock.WaitAsync(cancellationToken);
            try
            {
                if (!_packageIndex.ContainsKey(package.Id))
                {
                    _packageIndex[package.Id] = new List<PluginPackageInfo>();
                }

                var existingPackage = _packageIndex[package.Id]
                    .FirstOrDefault(p => p.Version == package.Version);

                if (existingPackage != null)
                {
                    // 更新现有包
                    _packageIndex[package.Id].Remove(existingPackage);
                    _logger?.LogInformation("[PluginRepository] Updated package: {Id} v{Version}", package.Id, package.Version);
                }
                else
                {
                    _logger?.LogInformation("[PluginRepository] Added new package: {Id} v{Version}", package.Id, package.Version);
                }

                _packageIndex[package.Id].Add(package);
                await SaveIndexAsync(cancellationToken);
            }
            finally
            {
                _indexLock.Release();
            }
        }

        /// <summary>
        /// 删除插件包
        /// </summary>
        public async Task RemovePackageAsync(string pluginId, string version, CancellationToken cancellationToken = default)
        {
            await _indexLock.WaitAsync(cancellationToken);
            try
            {
                if (_packageIndex.TryGetValue(pluginId, out var versions))
                {
                    var package = versions.FirstOrDefault(p => p.Version == version);
                    if (package != null)
                    {
                        versions.Remove(package);

                        // 如果没有其他版本，删除整个条目
                        if (versions.Count == 0)
                        {
                            _packageIndex.Remove(pluginId);
                        }

                        // 删除本地文件
                        if (!string.IsNullOrEmpty(package.LocalPath) && File.Exists(package.LocalPath))
                        {
                            File.Delete(package.LocalPath);
                        }

                        await SaveIndexAsync(cancellationToken);
                        _logger?.LogInformation("[PluginRepository] Removed package: {Id} v{Version}", pluginId, version);
                    }
                }
            }
            finally
            {
                _indexLock.Release();
            }
        }

        /// <summary>
        /// 搜索插件包
        /// </summary>
        public List<PluginPackageInfo> SearchPackages(string query, string? tag = null)
        {
            var allPackages = GetAllPackages();

            // 每个插件只返回最新版本
            var latestPackages = allPackages
                .GroupBy(p => p.Id)
                .Select(g => g.OrderByDescending(p => Version.Parse(p.Version)).First())
                .ToList();

            if (!string.IsNullOrWhiteSpace(query))
            {
                query = query.ToLowerInvariant();
                latestPackages = latestPackages
                    .Where(p =>
                        p.Name.ToLowerInvariant().Contains(query) ||
                        p.Description.ToLowerInvariant().Contains(query) ||
                        p.Tags.Any(t => t.ToLowerInvariant().Contains(query)))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                latestPackages = latestPackages
                    .Where(p => p.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }

            return latestPackages;
        }

        /// <summary>
        /// 获取插件包的本地路径
        /// </summary>
        public string GetPackagePath(string pluginId, string version)
        {
            return Path.Combine(_repositoryPath, pluginId, version);
        }

        /// <summary>
        /// 获取插件包文件路径
        /// </summary>
        public string GetPackageFilePath(string pluginId, string version)
        {
            return Path.Combine(GetPackagePath(pluginId, version), $"{pluginId}.zip");
        }

        /// <summary>
        /// 检查插件包是否已下载
        /// </summary>
        public bool IsPackageDownloaded(string pluginId, string version)
        {
            var packagePath = GetPackageFilePath(pluginId, version);
            return File.Exists(packagePath);
        }

        /// <summary>
        /// 保存索引到文件
        /// </summary>
        private async Task SaveIndexAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var allPackages = _packageIndex.Values.SelectMany(v => v).ToList();
                var json = JsonSerializer.Serialize(allPackages, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_indexFilePath, json, cancellationToken);
                _logger?.LogDebug("[PluginRepository] Saved index with {Count} packages", allPackages.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginRepository] Failed to save index");
                throw;
            }
        }

        /// <summary>
        /// 获取所有标签
        /// </summary>
        public List<string> GetAllTags()
        {
            return GetAllPackages()
                .SelectMany(p => p.Tags)
                .Distinct()
                .OrderBy(t => t)
                .ToList();
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public RepositoryStatistics GetStatistics()
        {
            var allPackages = GetAllPackages();
            var uniquePlugins = _packageIndex.Keys.Count;

            return new RepositoryStatistics
            {
                TotalPackages = allPackages.Count,
                UniquePlugins = uniquePlugins,
                TotalDownloads = allPackages.Sum(p => p.DownloadCount),
                AverageRating = allPackages.Any() ? allPackages.Average(p => p.Rating) : 0,
                LastUpdated = allPackages.Any() ? allPackages.Max(p => p.LastUpdated) : DateTime.MinValue
            };
        }

        /// <summary>
        /// 清理未使用的包文件
        /// </summary>
        public Task CleanupUnusedPackagesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var allPackages = GetAllPackages();
                var validPaths = allPackages
                    .Where(p => !string.IsNullOrEmpty(p.LocalPath))
                    .Select(p => p.LocalPath!)
                    .ToHashSet();

                // 扫描仓库目录
                var allFiles = Directory.GetFiles(_repositoryPath, "*.zip", SearchOption.AllDirectories);

                foreach (var file in allFiles)
                {
                    if (!validPaths.Contains(file))
                    {
                        File.Delete(file);
                        _logger?.LogInformation("[PluginRepository] Deleted unused package file: {File}", file);
                    }
                }

                // 删除空目录
                var allDirs = Directory.GetDirectories(_repositoryPath, "*", SearchOption.AllDirectories);
                foreach (var dir in allDirs.OrderByDescending(d => d.Length)) // 从最深的目录开始
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir);
                        _logger?.LogDebug("[PluginRepository] Deleted empty directory: {Dir}", dir);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[PluginRepository] Failed to cleanup unused packages");
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// 仓库统计信息
    /// </summary>
    public class RepositoryStatistics
    {
        public int TotalPackages { get; set; }
        public int UniquePlugins { get; set; }
        public int InstalledCount { get; set; }
        public int TotalDownloads { get; set; }
        public double AverageRating { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
