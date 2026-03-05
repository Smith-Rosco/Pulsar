// [Path]: Pulsar/Pulsar/Tools/SegoeIconParser.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Pulsar.Helpers;

namespace Pulsar.Tools
{
    /// <summary>
    /// Segoe Fluent Icons HTML 解析工具
    /// </summary>
    public static class SegoeIconParser
    {
        /// <summary>
        /// 从 HTML 文件解析图标数据
        /// </summary>
        /// <param name="htmlFilePath">HTML 文件路径</param>
        /// <returns>图标列表</returns>
        public static List<IconItem> ParseFromHtml(string htmlFilePath)
        {
            var icons = new List<IconItem>();
            
            if (!File.Exists(htmlFilePath))
            {
                throw new FileNotFoundException($"HTML file not found: {htmlFilePath}");
            }

            var html = File.ReadAllText(htmlFilePath);
            
            // 正则表达式匹配模式
            // 匹配: <td>E700</td>\n<td>GlobalNavigationButton</td>
            var pattern = @"<td>([A-F0-9]{4})</td>\s*<td>([^<]+)</td>";
            var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    var code = match.Groups[1].Value.ToUpper();
                    var name = match.Groups[2].Value.Trim();
                    
                    // 跳过空名称
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    // 计算 Character
                    var character = string.Empty;
                    if (int.TryParse(code, System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
                    {
                        character = char.ConvertFromUtf32(codePoint);
                    }

                    icons.Add(new IconItem
                    {
                        Name = name,
                        Code = code,
                        Character = character
                    });
                }
            }

            return icons;
        }

        /// <summary>
        /// 生成 C# 代码（用于更新 GlyphData.cs）
        /// </summary>
        public static string GenerateCSharpCode(List<IconItem> icons)
        {
            var code = "// Auto-generated from Segoe Fluent Icons HTML\n";
            code += "public static readonly List<IconItem> CommonIcons = new()\n{\n";

            foreach (var icon in icons)
            {
                // 直接在生成时计算 Character，避免运行时开销
                if (int.TryParse(icon.Code, System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
                {
                    var character = char.ConvertFromUtf32(codePoint);
                    // 转义反斜杠和引号
                    var escapedChar = character.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    code += $"    new() {{ Name = \"{icon.Name}\", Code = \"{icon.Code}\", Character = \"{escapedChar}\" }},\n";
                }
                else
                {
                    // 如果解析失败，使用空字符串
                    code += $"    new() {{ Name = \"{icon.Name}\", Code = \"{icon.Code}\", Character = \"\" }},\n";
                }
            }

            code += "};\n";
            return code;
        }

        /// <summary>
        /// 生成统计信息
        /// </summary>
        public static string GenerateStatistics(List<IconItem> icons)
        {
            var stats = $"Total Icons: {icons.Count}\n";
            stats += $"Code Range: {icons[0].Code} - {icons[^1].Code}\n";
            
            // 统计名称长度分布
            var shortNames = 0;
            var mediumNames = 0;
            var longNames = 0;
            
            foreach (var icon in icons)
            {
                if (icon.Name.Length <= 10)
                    shortNames++;
                else if (icon.Name.Length <= 20)
                    mediumNames++;
                else
                    longNames++;
            }
            
            stats += $"Name Length Distribution:\n";
            stats += $"  Short (≤10 chars): {shortNames}\n";
            stats += $"  Medium (11-20 chars): {mediumNames}\n";
            stats += $"  Long (>20 chars): {longNames}\n";
            
            return stats;
        }
    }
}
