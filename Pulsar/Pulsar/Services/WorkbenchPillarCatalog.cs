using System;
using System.Collections.Generic;

namespace Pulsar.Services
{
    /// <summary>
    /// 办公工作台三支柱。M2 叙事约定："功能顺序 = 叙事顺序"，
    /// 所有应用内入口（设置导航、插件列表、首次启动向导）的排序都从本目录读取，
    /// 保证支柱入口始终排在系统工具之前。
    /// </summary>
    public enum WorkbenchPillar
    {
        /// <summary>Excel/WPS 宏（VBA Runner）。</summary>
        MacroRunner,

        /// <summary>老旧网页脚本（Bookmarklet Runner）。</summary>
        WebScripts,

        /// <summary>安全表单填写 / 签名（PKI）。</summary>
        SecureFill
    }

    /// <summary>
    /// 三支柱到插件/入口标识的稳定映射（单一事实来源）。
    /// 插件重命名时只需更新本文件。
    /// </summary>
    public static class WorkbenchPillarCatalog
    {
        public const string VbaRunnerPluginId = "com.pulsar.vbarunner";
        public const string BookmarkletPluginId = "com.pulsar.bookmarklet";
        public const string PkiPluginId = "com.pulsar.pki";

        /// <summary>设置导航中的工作台页（支柱组），顺序即展示顺序。</summary>
        public static readonly IReadOnlyList<string> PillarPageIds =
        [
            SettingsPageIds.Slots,
            SettingsPageIds.Plugins
        ];

        /// <summary>支柱插件优先顺序：宏 → 网页脚本 → 安全填写。</summary>
        public static readonly IReadOnlyList<string> PillarPluginIds =
        [
            VbaRunnerPluginId,
            BookmarkletPluginId,
            PkiPluginId
        ];

        /// <summary>支柱首次启动场景（Excel 宏 → 网页脚本）。未列出的场景（如 notepad）属于背景组。</summary>
        public static readonly IReadOnlyList<string> PillarScenarioIds =
        [
            "excel",
            "browser"
        ];

        /// <summary>设置导航中归属系统/支持组的页。与 PillarPageIds 之外的全部页面对应。</summary>
        public static readonly IReadOnlyList<string> SystemPageIds =
        [
            SettingsPageIds.General,
            SettingsPageIds.Analytics,
            SettingsPageIds.About
        ];

        public static bool IsPillarPlugin(string? pluginId) => GetPluginPriority(pluginId) < PillarPluginIds.Count;

        /// <summary>
        /// 插件排序键：支柱插件按目录顺序返回 0..N-1；其余（系统/扩展工具）统一返回
        /// <see cref="BackgroundPriority"/>，保持相对顺序交给调用者的次级排序。
        /// </summary>
        public static int GetPluginPriority(string? pluginId)
        {
            for (var i = 0; i < PillarPluginIds.Count; i++)
            {
                if (string.Equals(PillarPluginIds[i], pluginId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return BackgroundPriority;
        }

        /// <summary>场景排序键：支柱场景在前，其余场景（记事本等通用演示）为背景组。</summary>
        public static int GetScenarioPriority(string? scenarioId)
        {
            for (var i = 0; i < PillarScenarioIds.Count; i++)
            {
                if (string.Equals(PillarScenarioIds[i], scenarioId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return BackgroundPriority;
        }

        /// <summary>页面排序键：工作台页在前，系统/支持页为背景组。</summary>
        public static int GetPagePriority(string? pageId)
        {
            for (var i = 0; i < PillarPageIds.Count; i++)
            {
                if (string.Equals(PillarPageIds[i], pageId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return BackgroundPriority;
        }

        /// <summary>背景（非支柱）条目的统一排序键，保证永远排在支柱之后。</summary>
        public const int BackgroundPriority = int.MaxValue;
    }
}
