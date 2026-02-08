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
        /// 解析脚本中的 Runner 指令
        /// </summary>
        /// <param name="path">脚本文件路径</param>
        /// <returns>指令名称，如果未找到则返回 "None"</returns>
        public static string ParseDirective(string path)
        {
            if (!File.Exists(path))
            {
                return "None";
            }

            try
            {
                // 读取前 10 行 (比原版稍微宽松一点)
                var lines = File.ReadLines(path).Take(10);
                
                foreach (var line in lines)
                {
                    // 匹配格式: ' @Runner: CommandName
                    // \s* 允许 ' 和 @ 之间有空格，以及 : 后的空格
                    var match = Regex.Match(line, @"'\s*@Runner:\s*(\w+)");
                    
                    if (match.Success) 
                    {
                        string found = match.Groups[1].Value;
                        return found;
                    }
                }
            }
            catch (Exception)
            {
                // 忽略读取错误
            }
            
            return "None";
        }
    }
}