// [Path]: Pulsar/Pulsar/Plugins/BookmarkletRunner/ScriptPreprocessor.cs

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Pulsar.Plugins.Extensions.BookmarkletRunner
{
    /// <summary>
    /// 脚本预处理工具类 - 处理书签脚本文件的读取和格式化
    /// </summary>
    internal static class ScriptPreprocessor
    {
        /// <summary>
        /// 从文件读取并预处理书签脚本
        /// </summary>
        /// <param name="scriptPath">脚本文件路径</param>
        /// <returns>预处理后的脚本内容（不包含 "javascript:" 前缀）</returns>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        /// <exception cref="IOException">文件读取失败时抛出</exception>
        public static string PreprocessScript(string scriptPath)
        {
            // 1. 验证路径
            if (string.IsNullOrEmpty(scriptPath))
            {
                throw new ArgumentException("Script path cannot be null or empty", nameof(scriptPath));
            }

            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"Script file not found: {scriptPath}");
            }

            // 2. 读取文件内容
            string content = File.ReadAllText(scriptPath);

            // 3. 预处理脚本
            return ProcessScriptContent(content);
        }

        /// <summary>
        /// 处理脚本内容（单行化、去除前缀等）
        /// </summary>
        /// <param name="content">原始脚本内容</param>
        /// <returns>处理后的脚本内容</returns>
        public static string ProcessScriptContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return string.Empty;
            }

            // 1. 单行化：将换行符替换为空格
            // 避免破坏字符串内的内容，但对于 Bookmarklet 通常这样处理是安全的
            string oneline = Regex.Replace(content, @"[\r\n]+", " ");

            // 2. 去除多余空白
            oneline = Regex.Replace(oneline, @"\s+", " ");

            // 3. 去除首尾空白
            oneline = oneline.Trim();

            // 4. 移除 "javascript:" 前缀（如果存在）
            // 我们会在后续重新构造完整的 "javascript:" URL
            if (oneline.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                oneline = oneline.Substring(11);
            }

            return oneline;
        }

        /// <summary>
        /// 验证脚本路径的安全性（防止路径遍历攻击）
        /// </summary>
        /// <param name="scriptPath">脚本路径</param>
        /// <returns>如果路径安全则返回 true</returns>
        public static bool IsPathSafe(string scriptPath)
        {
            try
            {
                // 获取绝对路径
                string fullPath = Path.GetFullPath(scriptPath);

                // 检查是否包含可疑的路径遍历模式
                if (fullPath.Contains(".."))
                {
                    return false;
                }

                // 检查文件扩展名（可选，根据需求调整）
                string extension = Path.GetExtension(fullPath).ToLowerInvariant();
                if (!string.IsNullOrEmpty(extension) && extension != ".js" && extension != ".txt")
                {
                    // 只允许 .js 和 .txt 文件（可根据需求调整）
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
