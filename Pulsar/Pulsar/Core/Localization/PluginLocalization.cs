using System.Text.RegularExpressions;

namespace Pulsar.Core.Localization
{
    /// <summary>
    /// 内置插件元数据字符串（显示名/描述/分类）的约定式本地化助手。
    /// key 由英文原文规范化而来：
    ///   Plugin.Name.{AlphaNumOnly(DisplayName)} /
    ///   Plugin.Description.{AlphaNumOnly(DisplayName)} /
    ///   Plugin.Category.{AlphaNumOnly(Category)}
    /// 描述键按插件显示名推导（而非描述文本），与 resx 既有的 6 个 Plugin.Description.* 键对齐；
    /// 未命中时回退为英文原文。与 SlotAction./SlotParam. 的约定保持一致。
    /// </summary>
    public static class PluginLocalization
    {
        public static string LocalizePluginName(ILocalizationService loc, string displayName)
            => ConventionLookup(loc, "Plugin.Name.", displayName);

        public static string LocalizePluginDescription(ILocalizationService loc, string description, string displayName)
            => ConventionLookup(loc, "Plugin.Description.", description, displayName);

        public static string LocalizePluginCategory(ILocalizationService loc, string category)
            => ConventionLookup(loc, "Plugin.Category.", category);

        /// <summary>
        /// 约定式本地化查表的单一实现（SlotAction./SlotParam./Plugin.* 共用）：
        /// key = {prefix}{AlphaNumOnly(keySource)}，命中则取本地化值，未命中回退原文。
        /// keySource 缺省时与 value 相同；描述类键按显示名推导时显式传入。
        /// </summary>
        public static string ConventionLookup(ILocalizationService? loc, string prefix, string? value, string? keySource = null)
        {
            if (loc == null || string.IsNullOrWhiteSpace(value))
            {
                return value ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(keySource))
            {
                keySource = value;
            }

            var safeKey = Regex.Replace(keySource, @"[^a-zA-Z0-9]", "");
            var key = $"{prefix}{safeKey}";
            var localized = loc[key];
            return localized != key ? localized : value;
        }
    }
}
