// [Path]: Pulsar/Pulsar/Core/Plugin/Metadata/PluginManifestReader.cs

using System.IO;
using System.Text.Json;

namespace Pulsar.Core.Plugin.Metadata
{
    /// <summary>
    /// 插件清单文件解析的单一事实来源。
    ///
    /// 四个位置曾各自实现「plugin.manifest.json → 回退 manifest.json → 大小写不敏感
    /// 反序列化」：<c>PluginLoader</c>（外部发现）、<c>LocalPluginScanner</c>（本地扫描）、
    /// <c>PluginPackageManager</c>（安装前校验/存在性）。文件名约定与反序列化选项的
    /// 漂移风险被多份复制放大（例如新增第三种清单名需要改四个地方）。
    ///
    /// 本类只收敛 <b>locate + 反序列化</b> 两步不变量。内容语义由各调用方在自己的
    /// 错误层上执行——Id 是否为空、权限 token 是否已知、Pulsar 版本是否兼容、以及
    /// 各自的失败消息（ADR/架构审查候选 C，2026-09-04）。
    /// </summary>
    public static class PluginManifestReader
    {
        /// <summary>新版清单文件名（插件包与已安装目录）。</summary>
        public const string ManifestFileName = "plugin.manifest.json";

        /// <summary>旧版清单文件名（兼容遗留包）。</summary>
        public const string LegacyManifestFileName = "manifest.json";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// 解析插件目录/解压目录中的清单文件路径：优先
        /// <see cref="ManifestFileName"/>，缺失时回退 <see cref="LegacyManifestFileName"/>。
        /// 两者都不存在时返回 null。
        /// </summary>
        public static string? TryResolveManifestPath(string pluginFolderPath)
        {
            var preferred = Path.Combine(pluginFolderPath, ManifestFileName);
            if (File.Exists(preferred))
            {
                return preferred;
            }

            var legacy = Path.Combine(pluginFolderPath, LegacyManifestFileName);
            return File.Exists(legacy) ? legacy : null;
        }

        /// <summary>
        /// 大小写不敏感地把清单 JSON 反序列化为 <see cref="PluginManifest"/>。
        /// 传入的 JSON 为字面量 "null" 时返回 null。不做任何内容校验；JSON 格式错误
        /// 抛 <see cref="JsonException"/>，由调用方决定错误保真度。
        /// </summary>
        public static PluginManifest? Parse(string manifestJson)
            => JsonSerializer.Deserialize<PluginManifest>(manifestJson, JsonOptions);
    }
}
