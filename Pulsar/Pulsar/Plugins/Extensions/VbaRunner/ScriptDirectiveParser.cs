using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Pulsar.Plugins.Extensions.VbaRunner
{
    /// <summary>
    /// 脚本指令解析器 - 解析 VBA 脚本头部的指令
    /// 支持多种指令类型：@Runner, @Macro, @Requires, @OnMissing, @SheetFilter, @AutoSelectSingle
    /// </summary>
    public static class ScriptDirectiveParser
    {
        private static readonly Regex DirectivePattern = 
            new Regex(@"'\s*@(\w+):\s*(.+)", RegexOptions.Compiled);

        /// <summary>
        /// 解析脚本内容中的所有指令
        /// </summary>
        /// <param name="content">脚本文件内容</param>
        /// <returns>包含所有解析指令的对象</returns>
        public static ScriptDirectives ParseAllDirectives(string content)
        {
            var directives = new ScriptDirectives();
            
            if (string.IsNullOrWhiteSpace(content))
                return directives;

            try
            {
                using var reader = new StringReader(content);
                string? line;
                int lineCount = 0;
                
                while ((line = reader.ReadLine()) != null && lineCount < 100)
                {
                    var match = DirectivePattern.Match(line);
                    if (match.Success)
                    {
                        string key = match.Groups[1].Value;
                        string value = match.Groups[2].Value.Trim();
                        
                        switch (key.ToLowerInvariant())
                        {
                            case "runner":
                                directives.Runner = value;
                                break;
                            case "macro":
                                directives.Macro = value;
                                break;
                            case "requires":
                                directives.Requires.Add(value);
                                break;
                            case "onmissing":
                                directives.OnMissing = value;
                                break;
                            case "autoselectsingle":
                                directives.AutoSelectSingle = 
                                    bool.TryParse(value, out bool result) && result;
                                break;
                            case "sheetfilter":
                                directives.SheetFilter = value;
                                break;
                        }
                    }
                    lineCount++;
                }
            }
            catch (Exception)
            {
                // Return defaults on parse error
            }
            
            return directives;
        }

        /// <summary>
        /// 解析脚本内容中的 Runner 指令（向后兼容方法）
        /// </summary>
        /// <param name="content">脚本文件内容</param>
        /// <returns>指令名称，如果未找到则返回 "None"</returns>
        public static string ParseDirectiveFromContent(string content)
        {
            return ParseAllDirectives(content).Runner;
        }

        /// <summary>
        /// 从文件路径解析指令（向后兼容方法）
        /// </summary>
        public static string ParseDirective(string path)
        {
            if (!File.Exists(path)) return "None";
            return ParseDirectiveFromContent(File.ReadAllText(path));
        }
    }
}