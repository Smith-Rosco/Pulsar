using System;
using System.Collections.Generic;

namespace Pulsar.Models
{
    /// <summary>
    /// 插件包信息
    /// </summary>
    public class PluginPackageInfo
    {
        /// <summary>
        /// 插件 ID
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 插件名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 插件版本
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// 插件描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 作者
        /// </summary>
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// 图标 URL 或 Key
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// 标签
        /// </summary>
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// 依赖项
        /// </summary>
        public List<PluginDependency> Dependencies { get; set; } = new();

        /// <summary>
        /// 下载 URL
        /// </summary>
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// 包大小（字节）
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// 发布日期
        /// </summary>
        public DateTime PublishedDate { get; set; }

        /// <summary>
        /// 最后更新日期
        /// </summary>
        public DateTime LastUpdated { get; set; }

        /// <summary>
        /// 下载次数
        /// </summary>
        public int DownloadCount { get; set; }

        /// <summary>
        /// 评分 (0-5)
        /// </summary>
        public double Rating { get; set; }

        /// <summary>
        /// 许可证
        /// </summary>
        public string License { get; set; } = "MIT";

        /// <summary>
        /// 项目 URL
        /// </summary>
        public string ProjectUrl { get; set; } = string.Empty;

        /// <summary>
        /// 文档 URL
        /// </summary>
        public string DocumentationUrl { get; set; } = string.Empty;

        /// <summary>
        /// 是否已安装
        /// </summary>
        public bool IsInstalled { get; set; }

        /// <summary>
        /// 已安装的版本
        /// </summary>
        public string? InstalledVersion { get; set; }

        /// <summary>
        /// 是否有更新
        /// </summary>
        public bool HasUpdate { get; set; }

        /// <summary>
        /// 包文件路径（本地）
        /// </summary>
        public string? LocalPath { get; set; }

        /// <summary>
        /// SHA256 校验和
        /// </summary>
        public string? Sha256 { get; set; }
    }

    /// <summary>
    /// 插件依赖项
    /// </summary>
    public class PluginDependency
    {
        /// <summary>
        /// 依赖的插件 ID
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// 版本约束（例如：">= 1.0.0", "~> 2.1"）
        /// </summary>
        public string VersionConstraint { get; set; } = "*";

        /// <summary>
        /// 是否为可选依赖
        /// </summary>
        public bool IsOptional { get; set; }

        public override string ToString()
        {
            return $"{PluginId} {VersionConstraint}";
        }
    }

    /// <summary>
    /// 插件安装状态
    /// </summary>
    public enum PluginInstallStatus
    {
        /// <summary>
        /// 未安装
        /// </summary>
        NotInstalled,

        /// <summary>
        /// 正在下载
        /// </summary>
        Downloading,

        /// <summary>
        /// 正在安装
        /// </summary>
        Installing,

        /// <summary>
        /// 已安装
        /// </summary>
        Installed,

        /// <summary>
        /// 正在更新
        /// </summary>
        Updating,

        /// <summary>
        /// 正在卸载
        /// </summary>
        Uninstalling,

        /// <summary>
        /// 安装失败
        /// </summary>
        Failed
    }

    /// <summary>
    /// 插件操作结果
    /// </summary>
    public class PluginOperationResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 操作的插件 ID
        /// </summary>
        public string PluginId { get; set; } = string.Empty;

        /// <summary>
        /// 操作类型
        /// </summary>
        public PluginOperationType OperationType { get; set; }

        /// <summary>
        /// 操作耗时
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// 额外信息
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        public static PluginOperationResult Successful(string pluginId, PluginOperationType operationType, TimeSpan duration)
        {
            return new PluginOperationResult
            {
                Success = true,
                PluginId = pluginId,
                OperationType = operationType,
                Duration = duration
            };
        }

        public static PluginOperationResult Failed(string pluginId, PluginOperationType operationType, string errorMessage)
        {
            return new PluginOperationResult
            {
                Success = false,
                PluginId = pluginId,
                OperationType = operationType,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// 插件操作类型
    /// </summary>
    public enum PluginOperationType
    {
        Install,
        Update,
        Uninstall,
        Download,
        Verify
    }
}
