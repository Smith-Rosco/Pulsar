// [Path]: Pulsar/Pulsar/Tools/IconParserTest.cs
using System;
using System.IO;
using System.Linq;
using Pulsar.Helpers;

namespace Pulsar.Tools
{
    /// <summary>
    /// 图标解析器测试程序（仅用于开发时运行）
    /// </summary>
    public static class IconParserTest
    {
        public static void Run()
        {
            try
            {
                Console.WriteLine("=== Segoe Icon Parser Test ===\n");

                // HTML 文件路径（项目根目录）
                var htmlPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "..", "..",
                    "segoe_icon.html");
                
                htmlPath = Path.GetFullPath(htmlPath);
                
                Console.WriteLine($"Reading HTML from: {htmlPath}");
                
                if (!File.Exists(htmlPath))
                {
                    Console.WriteLine($"ERROR: File not found!");
                    return;
                }

                // 解析图标
                var icons = SegoeIconParser.ParseFromHtml(htmlPath);
                
                Console.WriteLine($"\n✅ Parsed {icons.Count} icons successfully!\n");

                // 显示统计信息
                Console.WriteLine(SegoeIconParser.GenerateStatistics(icons));

                // 显示前 10 个图标
                Console.WriteLine("\n--- First 10 Icons ---");
                foreach (var icon in icons.Take(10))
                {
                    Console.WriteLine($"  {icon.Code} - {icon.Name}");
                }

                // 显示后 10 个图标
                Console.WriteLine("\n--- Last 10 Icons ---");
                foreach (var icon in icons.TakeLast(10))
                {
                    Console.WriteLine($"  {icon.Code} - {icon.Name}");
                }

                // 生成 C# 代码（保存到文件）
                var outputPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "GeneratedIcons.cs");
                
                var csharpCode = SegoeIconParser.GenerateCSharpCode(icons);
                File.WriteAllText(outputPath, csharpCode);
                
                Console.WriteLine($"\n✅ Generated C# code saved to: {outputPath}");
                Console.WriteLine("\nYou can now copy this code to GlyphData.cs");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
