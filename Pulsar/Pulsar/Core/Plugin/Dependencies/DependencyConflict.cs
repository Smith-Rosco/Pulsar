using System;
using System.Collections.Generic;

namespace Pulsar.Core.Plugin.Dependencies
{
    /// <summary>
    /// 依赖冲突信息
    /// </summary>
    public class DependencyConflict
    {
        /// <summary>
        /// 冲突的程序集名称
        /// </summary>
        public string AssemblyName { get; set; } = string.Empty;

        /// <summary>
        /// 冲突类型
        /// </summary>
        public ConflictType Type { get; set; }

        /// <summary>
        /// 冲突的版本列表
        /// </summary>
        public List<ConflictingVersion> ConflictingVersions { get; set; } = new();

        /// <summary>
        /// 冲突严重程度
        /// </summary>
        public ConflictSeverity Severity { get; set; }

        /// <summary>
        /// 建议的解决方案
        /// </summary>
        public string? Resolution { get; set; }

        public override string ToString()
        {
            return $"{Type} conflict: {AssemblyName} ({Severity})";
        }
    }

    /// <summary>
    /// 冲突的版本信息
    /// </summary>
    public class ConflictingVersion
    {
        /// <summary>
        /// 版本号
        /// </summary>
        public Version Version { get; set; } = new Version(1, 0, 0, 0);

        /// <summary>
        /// 使用此版本的插件列表
        /// </summary>
        public List<string> UsedByPlugins { get; set; } = new();

        /// <summary>
        /// 程序集文件路径
        /// </summary>
        public string? FilePath { get; set; }

        public override string ToString()
        {
            return $"v{Version} (used by {string.Join(", ", UsedByPlugins)})";
        }
    }

    /// <summary>
    /// 冲突类型
    /// </summary>
    public enum ConflictType
    {
        /// <summary>
        /// 版本冲突 - 多个插件依赖同一程序集的不同版本
        /// </summary>
        VersionMismatch,

        /// <summary>
        /// 缺失依赖 - 插件依赖的程序集未找到
        /// </summary>
        MissingDependency,

        /// <summary>
        /// 循环依赖 - 插件之间存在循环依赖
        /// </summary>
        CircularDependency,

        /// <summary>
        /// 不兼容版本 - 依赖的版本与主程序不兼容
        /// </summary>
        IncompatibleVersion,

        /// <summary>
        /// 重复程序集 - 同一程序集在多个位置存在
        /// </summary>
        DuplicateAssembly
    }

    /// <summary>
    /// 冲突严重程度
    /// </summary>
    public enum ConflictSeverity
    {
        /// <summary>
        /// 信息 - 不影响运行，仅供参考
        /// </summary>
        Info,

        /// <summary>
        /// 警告 - 可能影响运行，建议修复
        /// </summary>
        Warning,

        /// <summary>
        /// 错误 - 会导致运行失败，必须修复
        /// </summary>
        Error,

        /// <summary>
        /// 致命 - 严重错误，无法加载插件
        /// </summary>
        Critical
    }
}
