using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pulsar.Plugins.VbaRunner
{
    /// <summary>
    /// 脚本指令解析器 - 解析 VBA 脚本头部的 @Runner 指令
    /// </summary>
    public static class ScriptDirectiveParser
    {
        /// <summary>
        /// 解析脚本内容中的 Runner 指令
        /// </summary>
        /// <param name="content">脚本文件内容</param>
        /// <returns>指令名称，如果未找到则返回 "None"</returns>
        public static string ParseDirectiveFromContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return "None";
            }

            try
            {
                // 使用 StringReader 读取前 100 行
                using (var reader = new StringReader(content))
                {
                    string? line;
                    int lineCount = 0;
                    
                    while ((line = reader.ReadLine()) != null && lineCount < 100)
                    {
                        // 匹配格式: ' @Runner: CommandName
                        var match = Regex.Match(line, @"'\s*@Runner:\s*(\w+)");
                        if (match.Success) 
                        {
                            return match.Groups[1].Value;
                        }
                        lineCount++;
                    }
                }
            }
            catch (Exception)
            {
                // 忽略解析错误
            }
            
            return "None";
        }

        // Keep legacy method for compatibility if needed, or redirect it
        public static string ParseDirective(string path)
        {
            if (!File.Exists(path)) return "None";
            return ParseDirectiveFromContent(File.ReadAllText(path));
        }

    }
}