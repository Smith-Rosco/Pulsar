using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Pulsar.Core.Plugin.Versioning
{
    /// <summary>
    /// 插件清单 - 描述插件的元数据、依赖和权限
    /// 
    /// 文件名: plugin.manifest.json
    /// 位置: 插件 DLL 同目录
    /// </summary>
    public class PluginManifest
    {
        /// <summary>
        /// 插件唯一标识符（反向域名格式，如 "com.pulsar.pki"）
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 语义化版本号（如 "2.1.0"）
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// 最低 Pulsar 版本要求
        /// </summary>
        [JsonPropertyName("minPulsarVersion")]
        public string MinPulsarVersion { get; set; } = "1.0.0";

        /// <summary>
        /// 最高 Pulsar 版本要求（可选）
        /// </summary>
        [JsonPropertyName("maxPulsarVersion")]
        public string? MaxPulsarVersion { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 简短描述
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 作者/维护者
        /// </summary>
        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        /// <summary>
        /// 许可证（如 "MIT", "Apache-2.0", "Proprietary"）
        /// </summary>
        [JsonPropertyName("license")]
        public string License { get; set; } = "MIT";

        /// <summary>
        /// 图标（Emoji 或图片路径）
        /// </summary>
        [JsonPropertyName("icon")]
        public string Icon { get; set; } = "📦";

        /// <summary>
        /// 插件主程序集入口点（完全限定类型名）
        /// 例如: "Pulsar.Plugins.Pki.PkiPlugin"
        /// </summary>
        [JsonPropertyName("entryPoint")]
        public string EntryPoint { get; set; } = string.Empty;

        /// <summary>
        /// 插件依赖（插件 ID -> 版本范围）
        /// 例如: { "com.pulsar.crypto": "^1.0.0" }
        /// </summary>
        [JsonPropertyName("dependencies")]
        public Dictionary<string, string> Dependencies { get; set; } = new();

        /// <summary>
        /// NuGet 包依赖（包名 -> 版本）
        /// 例如: { "Newtonsoft.Json": "13.0.3" }
        /// </summary>
        [JsonPropertyName("packageDependencies")]
        public Dictionary<string, string> PackageDependencies { get; set; } = new();

        /// <summary>
        /// 所需权限列表
        /// 例如: ["clipboard.read", "window.focus", "filesystem.read"]
        /// </summary>
        [JsonPropertyName("permissions")]
        public List<string> Permissions { get; set; } = new();

        /// <summary>
        /// 插件文件列表（相对路径）
        /// </summary>
        [JsonPropertyName("files")]
        public List<string> Files { get; set; } = new();

        /// <summary>
        /// 插件分类标签
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        /// <summary>
        /// 文档链接
        /// </summary>
        [JsonPropertyName("documentationUrl")]
        public string? DocumentationUrl { get; set; }

        /// <summary>
        /// 项目主页
        /// </summary>
        [JsonPropertyName("homepage")]
        public string? Homepage { get; set; }

        /// <summary>
        /// 仓库地址
        /// </summary>
        [JsonPropertyName("repository")]
        public string? Repository { get; set; }

        /// <summary>
        /// 变更日志
        /// </summary>
        [JsonPropertyName("changelog")]
        public string? Changelog { get; set; }

        /// <summary>
        /// 是否为核心插件（不可禁用）
        /// </summary>
        [JsonPropertyName("isCore")]
        public bool IsCore { get; set; } = false;

        /// <summary>
        /// 插件层级（Core/Extension）
        /// </summary>
        [JsonPropertyName("tier")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PluginTier Tier { get; set; } = PluginTier.Extension;

        /// <summary>
        /// 自定义元数据（扩展字段）
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
