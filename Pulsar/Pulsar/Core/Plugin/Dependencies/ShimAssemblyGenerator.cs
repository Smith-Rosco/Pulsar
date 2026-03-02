using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Extensions.Logging;

namespace Pulsar.Core.Plugin.Dependencies
{
    /// <summary>
    /// Shim Assembly 生成器 - 生成类型转发程序集以解决版本冲突
    /// 
    /// 工作原理:
    /// 1. 分析目标程序集的公共类型
    /// 2. 生成一个新的程序集，包含 TypeForwardedTo 属性
    /// 3. 将类型转发到实际的程序集版本
    /// 4. 插件加载时使用 Shim 程序集，运行时自动转发到正确版本
    /// </summary>
    public class ShimAssemblyGenerator
    {
        private readonly ILogger<ShimAssemblyGenerator>? _logger;
        private readonly string _shimOutputDirectory;

        public ShimAssemblyGenerator(string shimOutputDirectory, ILogger<ShimAssemblyGenerator>? logger = null)
        {
            _logger = logger;
            _shimOutputDirectory = shimOutputDirectory;

            if (!Directory.Exists(_shimOutputDirectory))
            {
                Directory.CreateDirectory(_shimOutputDirectory);
            }
        }

        /// <summary>
        /// 为指定程序集生成 Shim
        /// </summary>
        public string GenerateShim(string sourceAssemblyPath, Version targetVersion)
        {
            _logger?.LogInformation("[ShimAssemblyGenerator] Generating shim for {Assembly} targeting v{Version}",
                Path.GetFileName(sourceAssemblyPath), targetVersion);

            try
            {
                // 1. 加载源程序集
                var sourceAssembly = Assembly.LoadFrom(sourceAssemblyPath);
                var sourceAssemblyName = sourceAssembly.GetName();

                // 2. 创建 Shim 程序集名称
                var shimAssemblyName = new AssemblyName
                {
                    Name = $"{sourceAssemblyName.Name}.Shim.v{targetVersion.Major}_{targetVersion.Minor}",
                    Version = targetVersion,
                    CultureInfo = sourceAssemblyName.CultureInfo
                };

                // 3. 定义 Shim 程序集
                var shimAssemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                    shimAssemblyName,
                    AssemblyBuilderAccess.RunAndCollect);

                var moduleBuilder = shimAssemblyBuilder.DefineDynamicModule(shimAssemblyName.Name!);

                // 4. 获取所有公共类型
                var publicTypes = sourceAssembly.GetExportedTypes();
                _logger?.LogDebug("[ShimAssemblyGenerator] Found {Count} public types in {Assembly}",
                    publicTypes.Length, sourceAssemblyName.Name);

                // 5. 为每个公共类型添加 TypeForwardedTo 属性
                foreach (var type in publicTypes)
                {
                    try
                    {
                        AddTypeForwarder(moduleBuilder, type, sourceAssembly);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "[ShimAssemblyGenerator] Failed to forward type {Type}", type.FullName);
                    }
                }

                // 6. 保存 Shim 程序集
                var shimPath = Path.Combine(_shimOutputDirectory, $"{shimAssemblyName.Name}.dll");
                
                // 注意: AssemblyBuilder.Save() 在 .NET Core/5+ 中不可用
                // 需要使用 System.Reflection.Metadata 和 System.Reflection.PortableExecutable
                SaveShimAssembly(shimAssemblyBuilder, shimPath, sourceAssembly);

                _logger?.LogInformation("[ShimAssemblyGenerator] Generated shim assembly: {Path}", shimPath);

                return shimPath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ShimAssemblyGenerator] Failed to generate shim for {Assembly}",
                    Path.GetFileName(sourceAssemblyPath));
                throw;
            }
        }

        /// <summary>
        /// 添加类型转发器
        /// </summary>
        private void AddTypeForwarder(ModuleBuilder moduleBuilder, Type type, Assembly targetAssembly)
        {
            // 在 .NET Core/5+ 中，TypeForwardedTo 属性通过 CustomAttributeBuilder 添加
            var typeForwardedToConstructor = typeof(System.Runtime.CompilerServices.TypeForwardedToAttribute)
                .GetConstructor(new[] { typeof(Type) });

            if (typeForwardedToConstructor != null)
            {
                var attributeBuilder = new CustomAttributeBuilder(
                    typeForwardedToConstructor,
                    new object[] { type });

                moduleBuilder.SetCustomAttribute(attributeBuilder);
            }
        }

        /// <summary>
        /// 保存 Shim 程序集 (使用 System.Reflection.Metadata)
        /// </summary>
        private void SaveShimAssembly(AssemblyBuilder assemblyBuilder, string outputPath, Assembly sourceAssembly)
        {
            // 注意: 这是一个简化的实现
            // 完整实现需要使用 System.Reflection.Metadata.Ecma335 来构建 PE 文件
            
            _logger?.LogWarning("[ShimAssemblyGenerator] Shim assembly saving is not fully implemented in .NET 8");
            _logger?.LogWarning("[ShimAssemblyGenerator] Consider using ILRepack or similar tools for production scenarios");

            // 临时方案: 复制源程序集并修改版本信息
            // 这不是真正的 Shim，但可以作为占位符
            File.Copy(sourceAssembly.Location, outputPath, overwrite: true);
        }

        /// <summary>
        /// 批量生成 Shim 程序集
        /// </summary>
        public Dictionary<string, string> GenerateShimsForConflicts(List<DependencyConflict> conflicts)
        {
            var shimMap = new Dictionary<string, string>(); // AssemblyName -> ShimPath

            foreach (var conflict in conflicts)
            {
                if (conflict.Type != ConflictType.VersionMismatch)
                {
                    continue;
                }

                try
                {
                    // 选择最高版本作为目标版本
                    var targetVersion = conflict.ConflictingVersions
                        .Select(v => v.Version)
                        .OrderByDescending(v => v)
                        .First();

                    // 为每个冲突版本生成 Shim
                    foreach (var conflictingVersion in conflict.ConflictingVersions)
                    {
                        if (conflictingVersion.Version == targetVersion)
                        {
                            continue; // 跳过目标版本
                        }

                        if (string.IsNullOrEmpty(conflictingVersion.FilePath))
                        {
                            continue;
                        }

                        var shimPath = GenerateShim(conflictingVersion.FilePath, targetVersion);
                        shimMap[$"{conflict.AssemblyName}@{conflictingVersion.Version}"] = shimPath;
                    }

                    _logger?.LogInformation("[ShimAssemblyGenerator] Generated shims for {Assembly} conflict",
                        conflict.AssemblyName);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[ShimAssemblyGenerator] Failed to generate shims for {Assembly}",
                        conflict.AssemblyName);
                }
            }

            return shimMap;
        }

        /// <summary>
        /// 清理旧的 Shim 程序集
        /// </summary>
        public void CleanupOldShims(TimeSpan maxAge)
        {
            try
            {
                var shimFiles = Directory.GetFiles(_shimOutputDirectory, "*.Shim.*.dll");
                var cutoffTime = DateTime.UtcNow - maxAge;

                foreach (var shimFile in shimFiles)
                {
                    var fileInfo = new FileInfo(shimFile);
                    if (fileInfo.LastWriteTimeUtc < cutoffTime)
                    {
                        File.Delete(shimFile);
                        _logger?.LogDebug("[ShimAssemblyGenerator] Deleted old shim: {File}", shimFile);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[ShimAssemblyGenerator] Failed to cleanup old shims");
            }
        }
    }

    /// <summary>
    /// Shim 程序集信息
    /// </summary>
    public class ShimAssemblyInfo
    {
        /// <summary>
        /// 源程序集名称
        /// </summary>
        public string SourceAssemblyName { get; set; } = string.Empty;

        /// <summary>
        /// 源程序集版本
        /// </summary>
        public Version SourceVersion { get; set; } = new Version(1, 0, 0, 0);

        /// <summary>
        /// 目标程序集版本
        /// </summary>
        public Version TargetVersion { get; set; } = new Version(1, 0, 0, 0);

        /// <summary>
        /// Shim 程序集路径
        /// </summary>
        public string ShimPath { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public override string ToString()
        {
            return $"{SourceAssemblyName} v{SourceVersion} -> v{TargetVersion}";
        }
    }
}
