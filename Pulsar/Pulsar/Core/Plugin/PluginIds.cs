using System;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 内置插件的持久化 ID 常量（单一事实来源）。
    ///
    /// <para>
    /// 这些字面量是**用户可见的持久化契约**：它们写在
    /// <c>%AppData%\Pulsar\Profiles.json</c> 的 slot <c>plugin</c> 字段与
    /// <c>plugins</c> 字典 key 上，并作为 <c>PluginUsageStats.json</c> 的
    /// <c>pluginId</c>。改动任何一个值都必须配套一条
    /// <see cref="Services.ConfigService"/> 加载时自愈迁移（见 ADR-032），
    /// 否则存量的用户配置会静默失效。
    /// </para>
    ///
    /// <para>
    /// 全部声明为 <c>const</c>（而非 <c>static readonly</c>）是刻意的：
    /// <c>switch</c> 表达式的 case 标签要求编译期常量模式，
    /// <see cref="Models.SlotPresentation"/> 的 type-badge / tone 解析依赖这一点。
    /// </para>
    /// </summary>
    public static class PluginIds
    {
        // ── 活跃 ID ────────────────────────────────────────────────────────────

        public const string WinSwitcher = "com.pulsar.winswitcher";
        public const string Command = "com.pulsar.command";
        public const string Bookmarklet = "com.pulsar.bookmarklet";
        public const string VbaRunner = "com.pulsar.vbarunner";
        public const string SystemCommand = "com.pulsar.system";

        /// <summary>
        /// Secret Fill 模块的当前 ID。
        /// 2026-09-10 由 <see cref="LegacySecretFill"/>（<c>com.pulsar.pki</c>）更名而来；
        /// 旧值仍在迁移读路径中被识别，见 <see cref="LegacySecretFill"/>。
        /// </summary>
        public const string SecretFill = "com.pulsar.secretfill";

        // ── 迁移用旧 ID（只读识别，不得再写出） ────────────────────────────────

        /// <summary>
        /// Secret Fill 模块的历史 ID（曾用名 <c>com.pulsar.pki</c>）。
        ///
        /// <para>
        /// **仅用于迁移读路径**：<see cref="Services.ConfigService"/> 与
        /// <c>PluginUsageTracker</c> 在加载时识别该值并归一化为
        /// <see cref="SecretFill"/>，随后幂等回写。
        /// 任何新的写出路径都**不得**再产生该值。
        /// </para>
        /// </summary>
        public const string LegacySecretFill = "com.pulsar.pki";

        /// <summary>
        /// 把可能为历史值的插件 ID 归一化为当前值；非历史值原样返回。
        /// 大小写不敏感（历史值经 JSON 手改可能大小写不一）。
        /// 空 / 空白输入归一化为空串（空白不是合法 ID）。
        /// </summary>
        public static string Normalize(string? pluginId)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
            {
                return string.Empty;
            }

            var trimmed = pluginId.Trim();

            return string.Equals(trimmed, LegacySecretFill, StringComparison.OrdinalIgnoreCase)
                ? SecretFill
                : trimmed;
        }

        /// <summary>该 ID（含历史别名）是否指向 Secret Fill 模块。</summary>
        public static bool IsSecretFill(string? pluginId)
        {
            return !string.IsNullOrWhiteSpace(pluginId)
                && string.Equals(Normalize(pluginId), SecretFill, StringComparison.OrdinalIgnoreCase);
        }
    }
}
