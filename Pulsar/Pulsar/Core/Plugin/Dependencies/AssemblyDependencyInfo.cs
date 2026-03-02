using System;
using System.Collections.Generic;
using System.Reflection;

namespace Pulsar.Core.Plugin.Dependencies
{
    /// <summary>
    /// 程序集依赖信息
    /// </summary>
    public class AssemblyDependencyInfo
    {
        /// <summary>
        /// 程序集名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 程序集版本
        /// </summary>
        public Version Version { get; set; } = new Version(1, 0, 0, 0);

        /// <summary>
        /// 程序集文件路径
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// 公钥令牌 (用于强名称程序集)
        /// </summary>
        public byte[]? PublicKeyToken { get; set; }

        /// <summary>
        /// 依赖的其他程序集
        /// </summary>
        public List<AssemblyName> Dependencies { get; set; } = new();

        /// <summary>
        /// 是否为系统程序集 (System.*, Microsoft.*)
        /// </summary>
        public bool IsSystemAssembly { get; set; }

        /// <summary>
        /// 是否为 Pulsar 主程序集或契约程序集
        /// </summary>
        public bool IsPulsarContract { get; set; }

        /// <summary>
        /// 程序集来源类型
        /// </summary>
        public AssemblySource Source { get; set; } = AssemblySource.Unknown;

        public override string ToString()
        {
            return $"{Name} v{Version} ({Source})";
        }
    }

    /// <summary>
    /// 程序集来源类型
    /// </summary>
    public enum AssemblySource
    {
        Unknown,
        Host,           // 主程序程序集
        Plugin,         // 插件程序集
        NuGet,          // NuGet 包
        System,         // 系统程序集
        Shim            // Shim 程序集
    }
}
