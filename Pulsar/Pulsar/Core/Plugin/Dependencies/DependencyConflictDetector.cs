using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Pulsar.Core.Plugin.Dependencies
{
    /// <summary>
    /// 依赖冲突检测器 - 分析插件依赖并检测冲突
    /// </summary>
    public class DependencyConflictDetector
    {
        private readonly ILogger<DependencyConflictDetector>? _logger;
        private readonly Dictionary<string, List<AssemblyDependencyInfo>> _assemblyVersions = new();

        public DependencyConflictDetector(ILogger<DependencyConflictDetector>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// 分析插件目录并检测依赖冲突
        /// </summary>
        public List<DependencyConflict> AnalyzePluginDirectory(string pluginDirectory)
        {
            _logger?.LogInformation("[DependencyConflictDetector] Analyzing plugin directory: {Directory}", pluginDirectory);

            var conflicts = new List<DependencyConflict>();
            _assemblyVersions.Clear();

            if (!Directory.Exists(pluginDirectory))
            {
                _logger?.LogWarning("[DependencyConflictDetector] Plugin directory not found: {Directory}", pluginDirectory);
                return conflicts;
            }

            // 1. 扫描所有插件文件夹
            var pluginFolders = Directory.GetDirectories(pluginDirectory);
            foreach (var folder in pluginFolders)
            {
                AnalyzePluginFolder(folder);
            }

            // 2. 检测版本冲突
            conflicts.AddRange(DetectVersionConflicts());

            // 3. 检测缺失依赖
            conflicts.AddRange(DetectMissingDependencies());

            // 4. 检测重复程序集
            conflicts.AddRange(DetectDuplicateAssemblies());

            _logger?.LogInformation("[DependencyConflictDetector] Found {Count} conflicts", conflicts.Count);

            return conflicts;
        }

        /// <summary>
        /// 分析单个插件文件夹
        /// </summary>
        private void AnalyzePluginFolder(string folder)
        {
            var pluginName = Path.GetFileName(folder);
            var dllFiles = Directory.GetFiles(folder, "*.dll");

            foreach (var dllPath in dllFiles)
            {
                try
                {
                    var assemblyName = AssemblyName.GetAssemblyName(dllPath);
                    var info = new AssemblyDependencyInfo
                    {
                        Name = assemblyName.Name ?? "Unknown",
                        Version = assemblyName.Version ?? new Version(1, 0, 0, 0),
                        FilePath = dllPath,
                        PublicKeyToken = assemblyName.GetPublicKeyToken(),
                        Source = AssemblySource.Plugin,
                        IsSystemAssembly = IsSystemAssembly(assemblyName.Name ?? ""),
                        IsPulsarContract = IsPulsarContract(assemblyName.Name ?? "")
                    };

                    // 加载程序集以获取依赖信息
                    try
                    {
                        var assembly = Assembly.LoadFrom(dllPath);
                        info.Dependencies = assembly.GetReferencedAssemblies().ToList();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[DependencyConflictDetector] Failed to load assembly for dependency analysis: {Path}", dllPath);
                    }

                    // 记录程序集版本
                    if (!_assemblyVersions.ContainsKey(info.Name))
                    {
                        _assemblyVersions[info.Name] = new List<AssemblyDependencyInfo>();
                    }
                    _assemblyVersions[info.Name].Add(info);

                    _logger?.LogDebug("[DependencyConflictDetector] Analyzed {Assembly} v{Version} from {Plugin}",
                        info.Name, info.Version, pluginName);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[DependencyConflictDetector] Failed to analyze assembly: {Path}", dllPath);
                }
            }
        }

        /// <summary>
        /// 检测版本冲突
        /// </summary>
        private List<DependencyConflict> DetectVersionConflicts()
        {
            var conflicts = new List<DependencyConflict>();

            foreach (var kvp in _assemblyVersions)
            {
                var assemblyName = kvp.Key;
                var versions = kvp.Value;

                // 跳过系统程序集和 Pulsar 契约程序集
                if (versions.Any(v => v.IsSystemAssembly || v.IsPulsarContract))
                {
                    continue;
                }

                // 检查是否存在多个版本
                var distinctVersions = versions.Select(v => v.Version).Distinct().ToList();
                if (distinctVersions.Count > 1)
                {
                    var conflict = new DependencyConflict
                    {
                        AssemblyName = assemblyName,
                        Type = ConflictType.VersionMismatch,
                        Severity = DetermineSeverity(distinctVersions),
                        ConflictingVersions = versions.GroupBy(v => v.Version)
                            .Select(g => new ConflictingVersion
                            {
                                Version = g.Key,
                                UsedByPlugins = g.Select(v => Path.GetFileName(Path.GetDirectoryName(v.FilePath) ?? "Unknown")).ToList(),
                                FilePath = g.First().FilePath
                            }).ToList(),
                        Resolution = $"Consider using binding redirects or shim assemblies to resolve version conflicts for {assemblyName}"
                    };

                    conflicts.Add(conflict);
                    _logger?.LogWarning("[DependencyConflictDetector] Version conflict detected: {Assembly} has {Count} versions",
                        assemblyName, distinctVersions.Count);
                }
            }

            return conflicts;
        }

        /// <summary>
        /// 检测缺失依赖
        /// </summary>
        private List<DependencyConflict> DetectMissingDependencies()
        {
            var conflicts = new List<DependencyConflict>();
            var allAssemblies = _assemblyVersions.Keys.ToHashSet();

            foreach (var kvp in _assemblyVersions)
            {
                foreach (var info in kvp.Value)
                {
                    foreach (var dependency in info.Dependencies)
                    {
                        var depName = dependency.Name ?? "Unknown";

                        // 跳过系统程序集
                        if (IsSystemAssembly(depName))
                        {
                            continue;
                        }

                        // 检查依赖是否存在
                        if (!allAssemblies.Contains(depName))
                        {
                            var conflict = new DependencyConflict
                            {
                                AssemblyName = depName,
                                Type = ConflictType.MissingDependency,
                                Severity = ConflictSeverity.Error,
                                ConflictingVersions = new List<ConflictingVersion>
                                {
                                    new ConflictingVersion
                                    {
                                        Version = dependency.Version ?? new Version(1, 0, 0, 0),
                                        UsedByPlugins = new List<string> { info.Name }
                                    }
                                },
                                Resolution = $"Install {depName} v{dependency.Version} or use NuGet package resolution"
                            };

                            conflicts.Add(conflict);
                            _logger?.LogError("[DependencyConflictDetector] Missing dependency: {Assembly} v{Version} required by {Plugin}",
                                depName, dependency.Version, info.Name);
                        }
                    }
                }
            }

            return conflicts;
        }

        /// <summary>
        /// 检测重复程序集
        /// </summary>
        private List<DependencyConflict> DetectDuplicateAssemblies()
        {
            var conflicts = new List<DependencyConflict>();

            foreach (var kvp in _assemblyVersions)
            {
                var assemblyName = kvp.Key;
                var versions = kvp.Value;

                // 检查同一版本是否在多个位置存在
                var versionGroups = versions.GroupBy(v => v.Version);
                foreach (var group in versionGroups)
                {
                    if (group.Count() > 1)
                    {
                        var conflict = new DependencyConflict
                        {
                            AssemblyName = assemblyName,
                            Type = ConflictType.DuplicateAssembly,
                            Severity = ConflictSeverity.Warning,
                            ConflictingVersions = new List<ConflictingVersion>
                            {
                                new ConflictingVersion
                                {
                                    Version = group.Key,
                                    UsedByPlugins = group.Select(v => Path.GetFileName(Path.GetDirectoryName(v.FilePath) ?? "Unknown")).ToList(),
                                    FilePath = group.First().FilePath
                                }
                            },
                            Resolution = $"Remove duplicate copies of {assemblyName} v{group.Key}"
                        };

                        conflicts.Add(conflict);
                        _logger?.LogWarning("[DependencyConflictDetector] Duplicate assembly detected: {Assembly} v{Version} found in {Count} locations",
                            assemblyName, group.Key, group.Count());
                    }
                }
            }

            return conflicts;
        }

        /// <summary>
        /// 判断冲突严重程度
        /// </summary>
        private ConflictSeverity DetermineSeverity(List<Version> versions)
        {
            // 如果主版本号不同，则为严重冲突
            if (versions.Select(v => v.Major).Distinct().Count() > 1)
            {
                return ConflictSeverity.Error;
            }

            // 如果次版本号不同，则为警告
            if (versions.Select(v => v.Minor).Distinct().Count() > 1)
            {
                return ConflictSeverity.Warning;
            }

            // 仅修订号不同，则为信息
            return ConflictSeverity.Info;
        }

        /// <summary>
        /// 判断是否为系统程序集
        /// </summary>
        private bool IsSystemAssembly(string assemblyName)
        {
            return assemblyName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.Equals("netstandard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断是否为 Pulsar 契约程序集
        /// </summary>
        private bool IsPulsarContract(string assemblyName)
        {
            return assemblyName.Equals("Pulsar", StringComparison.OrdinalIgnoreCase) ||
                   assemblyName.StartsWith("Pulsar.Contracts", StringComparison.OrdinalIgnoreCase);
        }
    }
}
