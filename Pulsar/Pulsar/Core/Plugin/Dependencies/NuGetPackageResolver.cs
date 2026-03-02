using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Pulsar.Core.Plugin.Dependencies
{
    /// <summary>
    /// NuGet 包解析器 - 解析插件的 NuGet 依赖
    /// </summary>
    public class NuGetPackageResolver
    {
        private readonly ILogger<NuGetPackageResolver>? _logger;
        private readonly string _nugetCacheDir;

        public NuGetPackageResolver(ILogger<NuGetPackageResolver>? logger = null)
        {
            _logger = logger;
            
            // 使用标准 NuGet 缓存目录
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _nugetCacheDir = Path.Combine(userProfile, ".nuget", "packages");
        }

        /// <summary>
        /// 解析插件的 NuGet 依赖
        /// </summary>
        public async Task<List<NuGetPackageInfo>> ResolvePackagesAsync(string pluginFolder, CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("[NuGetPackageResolver] Resolving NuGet packages for: {Folder}", pluginFolder);

            var packages = new List<NuGetPackageInfo>();

            // 1. 查找 .deps.json 文件
            var depsJsonFiles = Directory.GetFiles(pluginFolder, "*.deps.json");
            if (depsJsonFiles.Length == 0)
            {
                _logger?.LogDebug("[NuGetPackageResolver] No .deps.json file found in {Folder}", pluginFolder);
                return packages;
            }

            // 2. 解析 .deps.json 文件
            foreach (var depsJsonFile in depsJsonFiles)
            {
                try
                {
                    var parsedPackages = await ParseDepsJsonAsync(depsJsonFile, cancellationToken);
                    packages.AddRange(parsedPackages);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[NuGetPackageResolver] Failed to parse {File}", depsJsonFile);
                }
            }

            // 3. 解析 NuGet 缓存中的包
            foreach (var package in packages)
            {
                try
                {
                    ResolvePackageFromCache(package);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[NuGetPackageResolver] Failed to resolve package {Package} v{Version}",
                        package.PackageId, package.Version);
                }
            }

            _logger?.LogInformation("[NuGetPackageResolver] Resolved {Count} NuGet packages", packages.Count);

            return packages;
        }

        /// <summary>
        /// 解析 .deps.json 文件
        /// </summary>
        private async Task<List<NuGetPackageInfo>> ParseDepsJsonAsync(string depsJsonFile, CancellationToken cancellationToken)
        {
            var packages = new List<NuGetPackageInfo>();

            try
            {
                var json = await File.ReadAllTextAsync(depsJsonFile, cancellationToken);
                
                // 简化的 JSON 解析 - 实际应使用 System.Text.Json 或 Newtonsoft.Json
                // 这里仅作为示例，实际实现需要完整的 JSON 解析
                _logger?.LogDebug("[NuGetPackageResolver] Parsed {File}", depsJsonFile);
                
                // TODO: 实现完整的 .deps.json 解析
                // 格式参考: https://github.com/dotnet/runtime/blob/main/docs/design/features/host-runtime-information.md
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[NuGetPackageResolver] Failed to parse {File}", depsJsonFile);
            }

            return packages;
        }

        /// <summary>
        /// 从 NuGet 缓存解析包
        /// </summary>
        private void ResolvePackageFromCache(NuGetPackageInfo package)
        {
            if (!Directory.Exists(_nugetCacheDir))
            {
                _logger?.LogWarning("[NuGetPackageResolver] NuGet cache directory not found: {Directory}", _nugetCacheDir);
                return;
            }

            // NuGet 缓存结构: ~/.nuget/packages/{packageId}/{version}/
            var packageDir = Path.Combine(_nugetCacheDir, package.PackageId.ToLowerInvariant(), package.Version);
            
            if (Directory.Exists(packageDir))
            {
                package.CachePath = packageDir;
                
                // 查找 lib 目录中的程序集
                var libDir = Path.Combine(packageDir, "lib");
                if (Directory.Exists(libDir))
                {
                    var assemblies = Directory.GetFiles(libDir, "*.dll", SearchOption.AllDirectories);
                    package.Assemblies.AddRange(assemblies);
                    
                    _logger?.LogDebug("[NuGetPackageResolver] Found {Count} assemblies in {Package} v{Version}",
                        assemblies.Length, package.PackageId, package.Version);
                }
            }
            else
            {
                _logger?.LogWarning("[NuGetPackageResolver] Package not found in cache: {Package} v{Version}",
                    package.PackageId, package.Version);
            }
        }

        /// <summary>
        /// 获取包的所有依赖（递归）
        /// </summary>
        public List<NuGetPackageInfo> GetAllDependencies(NuGetPackageInfo package)
        {
            var allDependencies = new List<NuGetPackageInfo>();
            var visited = new HashSet<string>();

            void CollectDependencies(NuGetPackageInfo pkg)
            {
                var key = $"{pkg.PackageId}@{pkg.Version}";
                if (visited.Contains(key))
                {
                    return;
                }

                visited.Add(key);
                allDependencies.Add(pkg);

                foreach (var dependency in pkg.Dependencies)
                {
                    CollectDependencies(dependency);
                }
            }

            CollectDependencies(package);

            return allDependencies;
        }
    }

    /// <summary>
    /// NuGet 包信息
    /// </summary>
    public class NuGetPackageInfo
    {
        /// <summary>
        /// 包 ID
        /// </summary>
        public string PackageId { get; set; } = string.Empty;

        /// <summary>
        /// 包版本
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 包在 NuGet 缓存中的路径
        /// </summary>
        public string? CachePath { get; set; }

        /// <summary>
        /// 包中的程序集列表
        /// </summary>
        public List<string> Assemblies { get; set; } = new();

        /// <summary>
        /// 包的依赖项
        /// </summary>
        public List<NuGetPackageInfo> Dependencies { get; set; } = new();

        /// <summary>
        /// 目标框架
        /// </summary>
        public string? TargetFramework { get; set; }

        public override string ToString()
        {
            return $"{PackageId} v{Version}";
        }
    }
}
