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
    /// 依赖隔离管理器 - 协调依赖分析、冲突检测和 Shim 生成
    /// </summary>
    public class DependencyIsolationManager
    {
        private readonly ILogger<DependencyIsolationManager>? _logger;
        private readonly DependencyConflictDetector _conflictDetector;
        private readonly NuGetPackageResolver _nugetResolver;
        private readonly ShimAssemblyGenerator _shimGenerator;
        private readonly string _pluginDirectory;
        private readonly string _shimDirectory;

        private List<DependencyConflict> _detectedConflicts = new();
        private Dictionary<string, string> _shimMap = new(); // AssemblyName@Version -> ShimPath

        public DependencyIsolationManager(
            string pluginDirectory,
            ILogger<DependencyIsolationManager>? logger = null)
        {
            _logger = logger;
            _pluginDirectory = pluginDirectory;
            
            // 创建 Shim 输出目录
            _shimDirectory = Path.Combine(pluginDirectory, ".shims");
            if (!Directory.Exists(_shimDirectory))
            {
                Directory.CreateDirectory(_shimDirectory);
            }

            _conflictDetector = new DependencyConflictDetector(null);
            _nugetResolver = new NuGetPackageResolver(null);
            _shimGenerator = new ShimAssemblyGenerator(_shimDirectory, null);
        }

        /// <summary>
        /// 分析并解决依赖冲突
        /// </summary>
        public async Task<DependencyIsolationResult> AnalyzeAndResolveAsync(CancellationToken cancellationToken = default)
        {
            _logger?.LogInformation("[DependencyIsolationManager] Starting dependency analysis for {Directory}", _pluginDirectory);

            var result = new DependencyIsolationResult
            {
                PluginDirectory = _pluginDirectory,
                AnalysisStartTime = DateTime.UtcNow
            };

            try
            {
                // 1. 检测依赖冲突
                _logger?.LogInformation("[DependencyIsolationManager] Step 1: Detecting dependency conflicts");
                _detectedConflicts = _conflictDetector.AnalyzePluginDirectory(_pluginDirectory);
                result.Conflicts = _detectedConflicts;

                _logger?.LogInformation("[DependencyIsolationManager] Found {Count} conflicts", _detectedConflicts.Count);

                // 2. 解析 NuGet 包
                _logger?.LogInformation("[DependencyIsolationManager] Step 2: Resolving NuGet packages");
                var pluginFolders = Directory.GetDirectories(_pluginDirectory);
                foreach (var folder in pluginFolders)
                {
                    try
                    {
                        var packages = await _nugetResolver.ResolvePackagesAsync(folder, cancellationToken);
                        result.ResolvedPackages.AddRange(packages);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[DependencyIsolationManager] Failed to resolve NuGet packages for {Folder}", folder);
                    }
                }

                _logger?.LogInformation("[DependencyIsolationManager] Resolved {Count} NuGet packages", result.ResolvedPackages.Count);

                // 3. 生成 Shim 程序集
                _logger?.LogInformation("[DependencyIsolationManager] Step 3: Generating shim assemblies");
                var criticalConflicts = _detectedConflicts
                    .Where(c => c.Type == ConflictType.VersionMismatch && 
                               (c.Severity == ConflictSeverity.Error || c.Severity == ConflictSeverity.Critical))
                    .ToList();

                if (criticalConflicts.Any())
                {
                    _shimMap = _shimGenerator.GenerateShimsForConflicts(criticalConflicts);
                    result.GeneratedShims = _shimMap.Values.ToList();
                    
                    _logger?.LogInformation("[DependencyIsolationManager] Generated {Count} shim assemblies", _shimMap.Count);
                }
                else
                {
                    _logger?.LogInformation("[DependencyIsolationManager] No critical conflicts found, skipping shim generation");
                }

                // 4. 清理旧的 Shim 程序集
                _shimGenerator.CleanupOldShims(TimeSpan.FromDays(7));

                result.Success = true;
                result.AnalysisEndTime = DateTime.UtcNow;

                _logger?.LogInformation("[DependencyIsolationManager] Dependency analysis completed in {Duration}ms",
                    (result.AnalysisEndTime - result.AnalysisStartTime).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[DependencyIsolationManager] Dependency analysis failed");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// 获取程序集的 Shim 路径（如果存在）
        /// </summary>
        public string? GetShimPath(string assemblyName, Version version)
        {
            var key = $"{assemblyName}@{version}";
            return _shimMap.TryGetValue(key, out var shimPath) ? shimPath : null;
        }

        /// <summary>
        /// 获取所有检测到的冲突
        /// </summary>
        public IReadOnlyList<DependencyConflict> GetConflicts()
        {
            return _detectedConflicts.AsReadOnly();
        }

        /// <summary>
        /// 获取冲突报告
        /// </summary>
        public string GenerateConflictReport()
        {
            if (!_detectedConflicts.Any())
            {
                return "No dependency conflicts detected.";
            }

            var report = new System.Text.StringBuilder();
            report.AppendLine("=== Dependency Conflict Report ===");
            report.AppendLine($"Total Conflicts: {_detectedConflicts.Count}");
            report.AppendLine();

            var groupedByType = _detectedConflicts.GroupBy(c => c.Type);
            foreach (var group in groupedByType)
            {
                report.AppendLine($"## {group.Key} ({group.Count()})");
                foreach (var conflict in group)
                {
                    report.AppendLine($"  - {conflict.AssemblyName} [{conflict.Severity}]");
                    foreach (var version in conflict.ConflictingVersions)
                    {
                        report.AppendLine($"    * v{version.Version}: {string.Join(", ", version.UsedByPlugins)}");
                    }
                    if (!string.IsNullOrEmpty(conflict.Resolution))
                    {
                        report.AppendLine($"    Resolution: {conflict.Resolution}");
                    }
                    report.AppendLine();
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// 检查是否存在严重冲突
        /// </summary>
        public bool HasCriticalConflicts()
        {
            return _detectedConflicts.Any(c => 
                c.Severity == ConflictSeverity.Error || 
                c.Severity == ConflictSeverity.Critical);
        }

        /// <summary>
        /// 获取建议的解决方案
        /// </summary>
        public List<string> GetResolutionSuggestions()
        {
            var suggestions = new List<string>();

            var versionConflicts = _detectedConflicts
                .Where(c => c.Type == ConflictType.VersionMismatch)
                .ToList();

            if (versionConflicts.Any())
            {
                suggestions.Add($"Found {versionConflicts.Count} version conflicts. Consider:");
                suggestions.Add("  1. Update all plugins to use the same dependency versions");
                suggestions.Add("  2. Use binding redirects in app.config");
                suggestions.Add("  3. Enable shim assembly generation (automatic)");
            }

            var missingDeps = _detectedConflicts
                .Where(c => c.Type == ConflictType.MissingDependency)
                .ToList();

            if (missingDeps.Any())
            {
                suggestions.Add($"Found {missingDeps.Count} missing dependencies. Consider:");
                suggestions.Add("  1. Install missing NuGet packages");
                suggestions.Add("  2. Copy required DLLs to plugin folders");
                suggestions.Add("  3. Check plugin documentation for dependencies");
            }

            var duplicates = _detectedConflicts
                .Where(c => c.Type == ConflictType.DuplicateAssembly)
                .ToList();

            if (duplicates.Any())
            {
                suggestions.Add($"Found {duplicates.Count} duplicate assemblies. Consider:");
                suggestions.Add("  1. Remove duplicate DLLs from plugin folders");
                suggestions.Add("  2. Use shared dependency folder");
            }

            return suggestions;
        }
    }

    /// <summary>
    /// 依赖隔离分析结果
    /// </summary>
    public class DependencyIsolationResult
    {
        /// <summary>
        /// 插件目录
        /// </summary>
        public string PluginDirectory { get; set; } = string.Empty;

        /// <summary>
        /// 分析是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误消息（如果失败）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 检测到的冲突
        /// </summary>
        public List<DependencyConflict> Conflicts { get; set; } = new();

        /// <summary>
        /// 解析的 NuGet 包
        /// </summary>
        public List<NuGetPackageInfo> ResolvedPackages { get; set; } = new();

        /// <summary>
        /// 生成的 Shim 程序集路径
        /// </summary>
        public List<string> GeneratedShims { get; set; } = new();

        /// <summary>
        /// 分析开始时间
        /// </summary>
        public DateTime AnalysisStartTime { get; set; }

        /// <summary>
        /// 分析结束时间
        /// </summary>
        public DateTime AnalysisEndTime { get; set; }

        /// <summary>
        /// 分析耗时
        /// </summary>
        public TimeSpan Duration => AnalysisEndTime - AnalysisStartTime;

        /// <summary>
        /// 是否存在严重冲突
        /// </summary>
        public bool HasCriticalConflicts => Conflicts.Any(c => 
            c.Severity == ConflictSeverity.Error || 
            c.Severity == ConflictSeverity.Critical);

        public override string ToString()
        {
            return $"DependencyIsolationResult: {Conflicts.Count} conflicts, {GeneratedShims.Count} shims, Duration: {Duration.TotalMilliseconds}ms";
        }
    }
}
