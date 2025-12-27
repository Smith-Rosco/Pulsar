// [File]: Pulsar/Helpers/IconHelper.cs
using System.Globalization;

namespace Pulsar.Helpers
{
    public static class IconHelper
    {
        /// <summary>
        /// 将 IconKey 解析为 Segoe Fluent Icons 字符
        /// 示例: "E700" -> "\uE700"
        /// </summary>
        public static string GetGlyph(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;

            // 移除可能的前缀 (0x, u+, \u)
            string cleanKey = key.Trim().Replace("0x", "").Replace("u+", "").Replace("\\u", "");

            // 尝试解析 16 进制
            if (int.TryParse(cleanKey, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codePoint))
            {
                return char.ConvertFromUtf32(codePoint);
            }

            // 如果解析失败，返回空或首字母作为兜底
            return string.Empty;
        }

        // TODO: Phase 6.2 - Implement GetIconFromPath(string path) using ExtractIconEx
    }
}