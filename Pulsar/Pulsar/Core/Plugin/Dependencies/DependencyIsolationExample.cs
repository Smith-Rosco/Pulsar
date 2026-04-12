using System;
using System.IO;
using System.Threading.Tasks;
using Pulsar.Core.Plugin.Dependencies;

namespace Pulsar.Core.Plugin.Examples
{
    /// <summary>
    /// 依赖隔离系统使用示例
    /// </summary>
    public class DependencyIsolationExample
    {
        /// <summary>
        /// 示例 1: 基本依赖分析
        /// </summary>
        public static async Task Example1_BasicAnalysis()
        {
            Console.WriteLine("=== Example 1: Basic Dependency Analysis ===\n");

            var pluginDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar", "Plugins");

            var manager = new DependencyIsolationManager(pluginDirectory);
            var result = await manager.AnalyzeAndResolveAsync();

            if (result.Success)
            {
                Console.WriteLine($"✓ Analysis completed in {result.Duration.TotalMilliseconds}ms");
                Console.WriteLine($"  - Conflicts found: {result.Conflicts.Count}");
                Console.WriteLine($"  - NuGet packages resolved: {result.ResolvedPackages.Count}");
                Console.WriteLine($"  - Shim assemblies generated: {result.GeneratedShims.Count}");

                if (result.HasCriticalConflicts)
                {
                    Console.WriteLine("\n⚠ Critical conflicts detected!");
                }
            }
            else
            {
                Console.WriteLine($"✗ Analysis failed: {result.ErrorMessage}");
            }
        }

        /// <summary>
        /// 示例 2: 生成冲突报告
        /// </summary>
        public static async Task Example2_ConflictReport()
        {
            Console.WriteLine("\n=== Example 2: Generate Conflict Report ===\n");

            var pluginDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar", "Plugins");

            var manager = new DependencyIsolationManager(pluginDirectory);
            await manager.AnalyzeAndResolveAsync();

            var report = manager.GenerateConflictReport();
            Console.WriteLine(report);

            // 保存报告到文件
            var reportPath = Path.Combine(pluginDirectory, "conflict-report.txt");
            File.WriteAllText(reportPath, report);
            Console.WriteLine($"\n✓ Report saved to: {reportPath}");
        }

        /// <summary>
        /// 示例 3: 获取解决方案建议
        /// </summary>
        public static async Task Example3_ResolutionSuggestions()
        {
            Console.WriteLine("\n=== Example 3: Resolution Suggestions ===\n");

            var pluginDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar", "Plugins");

            var manager = new DependencyIsolationManager(pluginDirectory);
            await manager.AnalyzeAndResolveAsync();

            var suggestions = manager.GetResolutionSuggestions();

            if (suggestions.Count > 0)
            {
                Console.WriteLine("Suggested actions to resolve conflicts:\n");
                foreach (var suggestion in suggestions)
                {
                    Console.WriteLine($"  {suggestion}");
                }
            }
            else
            {
                Console.WriteLine("✓ No conflicts detected. All dependencies are compatible.");
            }
        }

        /// <summary>
        /// 示例 4: 检测特定类型的冲突
        /// </summary>
        public static async Task Example4_ConflictTypes()
        {
            Console.WriteLine("\n=== Example 4: Conflict Types ===\n");

            var pluginDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar", "Plugins");

            var manager = new DependencyIsolationManager(pluginDirectory);
            var result = await manager.AnalyzeAndResolveAsync();

            var conflicts = result.Conflicts;

            // 按类型分组
            var versionConflicts = conflicts.FindAll(c => c.Type == ConflictType.VersionMismatch);
            var missingDeps = conflicts.FindAll(c => c.Type == ConflictType.MissingDependency);
            var duplicates = conflicts.FindAll(c => c.Type == ConflictType.DuplicateAssembly);

            Console.WriteLine($"Version Conflicts: {versionConflicts.Count}");
            foreach (var conflict in versionConflicts)
            {
                Console.WriteLine($"  - {conflict.AssemblyName}");
                foreach (var version in conflict.ConflictingVersions)
                {
                    Console.WriteLine($"    * v{version.Version}: {string.Join(", ", version.UsedByPlugins)}");
                }
            }

            Console.WriteLine($"\nMissing Dependencies: {missingDeps.Count}");
            foreach (var conflict in missingDeps)
            {
                Console.WriteLine($"  - {conflict.AssemblyName}");
            }

            Console.WriteLine($"\nDuplicate Assemblies: {duplicates.Count}");
            foreach (var conflict in duplicates)
            {
                Console.WriteLine($"  - {conflict.AssemblyName}");
            }
        }

        /// <summary>
        /// 示例 5: 与 PluginLoader 集成
        /// </summary>
        public static void Example5_PluginLoaderIntegration(IServiceProvider services)
        {
            Console.WriteLine("\n=== Example 5: PluginLoader Integration ===\n");

            var pluginDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar", "Plugins");

            // 创建 PluginLoader (自动启用依赖分析)
            var pluginLoader = new PluginLoader(services, pluginDirectory);

            // 发现插件描述符 (自动分析依赖)
            var plugins = pluginLoader.DiscoverDescriptors(includeCore: true, includeExtensions: true, analyzeDependencies: true);

            Console.WriteLine($"✓ Discovered {plugins.Count} plugins");

            // 检查是否存在严重冲突
            if (pluginLoader.HasCriticalDependencyConflicts())
            {
                Console.WriteLine("\n⚠ Critical dependency conflicts detected!");
                Console.WriteLine("\nConflict Report:");
                Console.WriteLine(pluginLoader.GetDependencyConflictReport());
            }
            else
            {
                Console.WriteLine("\n✓ No critical conflicts detected");
            }

            // 获取详细分析结果
            var analysisResult = pluginLoader.GetDependencyAnalysisResult();
            if (analysisResult != null)
            {
                Console.WriteLine($"\nAnalysis Details:");
                Console.WriteLine($"  - Duration: {analysisResult.Duration.TotalMilliseconds}ms");
                Console.WriteLine($"  - Conflicts: {analysisResult.Conflicts.Count}");
                Console.WriteLine($"  - Shims: {analysisResult.GeneratedShims.Count}");
            }
        }

        /// <summary>
        /// 示例 6: 手动冲突检测
        /// </summary>
        public static void Example6_ManualConflictDetection()
        {
            Console.WriteLine("\n=== Example 6: Manual Conflict Detection ===\n");

            var pluginDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar", "Plugins");

            var detector = new DependencyConflictDetector();
            var conflicts = detector.AnalyzePluginDirectory(pluginDirectory);

            Console.WriteLine($"Found {conflicts.Count} conflicts:\n");

            foreach (var conflict in conflicts)
            {
                Console.WriteLine($"[{conflict.Severity}] {conflict.Type}: {conflict.AssemblyName}");
                
                if (!string.IsNullOrEmpty(conflict.Resolution))
                {
                    Console.WriteLine($"  Resolution: {conflict.Resolution}");
                }
                
                Console.WriteLine();
            }
        }

        /// <summary>
        /// 示例 7: NuGet 包解析
        /// </summary>
        public static async Task Example7_NuGetResolution()
        {
            Console.WriteLine("\n=== Example 7: NuGet Package Resolution ===\n");

            var pluginDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar", "Plugins");

            var resolver = new NuGetPackageResolver();

            // 解析所有插件文件夹的 NuGet 包
            var allPackages = new System.Collections.Generic.List<NuGetPackageInfo>();

            if (Directory.Exists(pluginDirectory))
            {
                var pluginFolders = Directory.GetDirectories(pluginDirectory);
                
                foreach (var folder in pluginFolders)
                {
                    var packages = await resolver.ResolvePackagesAsync(folder);
                    allPackages.AddRange(packages);
                    
                    if (packages.Count > 0)
                    {
                        Console.WriteLine($"Plugin: {Path.GetFileName(folder)}");
                        foreach (var package in packages)
                        {
                            Console.WriteLine($"  - {package.PackageId} v{package.Version}");
                            Console.WriteLine($"    Assemblies: {package.Assemblies.Count}");
                        }
                        Console.WriteLine();
                    }
                }
            }

            Console.WriteLine($"Total NuGet packages resolved: {allPackages.Count}");
        }

        /// <summary>
        /// 运行所有示例
        /// </summary>
        public static async Task RunAllExamples(IServiceProvider services)
        {
            try
            {
                await Example1_BasicAnalysis();
                await Example2_ConflictReport();
                await Example3_ResolutionSuggestions();
                await Example4_ConflictTypes();
                Example5_PluginLoaderIntegration(services);
                Example6_ManualConflictDetection();
                await Example7_NuGetResolution();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error running examples: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
