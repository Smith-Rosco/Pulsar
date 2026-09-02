using System.Text.RegularExpressions;

namespace Pulsar.Core.Localization
{
    /// <summary>
    /// 内置插件元数据字符串（显示名/描述）的约定式本地化助手。
    /// key 由英文原文规范化而来：Plugin.Name.{AlphaNumOnly} / Plugin.Description.{AlphaNumOnly}；
    /// 未命中时回退为英文原文。与 SlotAction./SlotParam. 的约定保持一致。
    /// </summary>
    public static class PluginLocalization
    {
        public static string LocalizePluginName(ILocalizationService loc, string displayName)
            => Localize(loc, "Plugin.Name.", displayName);

        public static string LocalizePluginDescription(ILocalizationService loc, string description)
            => Localize(loc, "Plugin.Description.", description);

        private static string Localize(ILocalizationService loc, string prefix, string value)
        {
            if (loc == null || string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var safeKey = Regex.Replace(value, @"[^a-zA-Z0-9]", "");
            var key = $"{prefix}{safeKey}";
            var localized = loc[key];
            return localized != key ? localized : value;
        }
    }
}
