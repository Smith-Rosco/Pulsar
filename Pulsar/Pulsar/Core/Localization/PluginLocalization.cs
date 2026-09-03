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
            => Localize(loc, "Plugin.Name.", displayName, displayName);

        public static string LocalizePluginDescription(ILocalizationService loc, string description, string displayName)
            => Localize(loc, "Plugin.Description.", description, displayName);

        public static string LocalizePluginCategory(ILocalizationService loc, string category)
            => Localize(loc, "Plugin.Category.", category, category);

        private static string Localize(ILocalizationService loc, string prefix, string value, string keySource)
        {
            if (loc == null || string.IsNullOrWhiteSpace(value))
            {
                return value;
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
