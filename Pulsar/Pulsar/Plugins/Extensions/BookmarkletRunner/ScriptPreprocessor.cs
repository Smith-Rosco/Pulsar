// [Path]: Pulsar/Pulsar/Plugins/BookmarkletRunner/ScriptPreprocessor.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Pulsar.Plugins.Extensions.BookmarkletRunner
{
    internal static class ScriptPreprocessor
    {
        /// <summary>
        /// 验证结果，包含处理后的脚本和错误信息
        /// </summary>
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ProcessedScript { get; set; } = string.Empty;
            public List<string> Errors { get; set; } = new();
            public List<string> Warnings { get; set; } = new();
        }

        /// <summary>
        /// 从文件读取并预处理书签脚本（保持向后兼容）
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

            // 3. 预处理脚本（使用新的验证方法）
            var result = ProcessScriptContent(content, null);
            
            // 如果验证失败，抛出异常（保持向后兼容）
            if (!result.IsValid)
            {
                throw new InvalidOperationException($"Script validation failed: {string.Join("; ", result.Errors)}");
            }

            return result.ProcessedScript;
        }

        public static ValidationResult ProcessScriptContent(string content, ILogger? logger = null)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(content))
            {
                result.Errors.Add("Script content is empty");
                return result;
            }

            // Remove BOM if present
            content = RemoveBOM(content);

            result = ProcessWithRegex(content, logger);
            return result;
        }

        /// <summary>
        private static ValidationResult ProcessWithRegex(string content, ILogger? logger)
        {
            var result = new ValidationResult();

            try
            {
                string processed = content.Trim();

                // Remove javascript: prefix
                if (processed.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                {
                    processed = processed.Substring(11).Trim();
                }

                // Remove single-line comments (but preserve URLs like http://)
                processed = Regex.Replace(processed, @"(?<!:)//.*?(?=\r?\n|$)", "");

                // Remove multi-line comments
                processed = Regex.Replace(processed, @"/\*.*?\*/", "", RegexOptions.Singleline);

                // Replace line breaks with spaces
                processed = Regex.Replace(processed, @"[\r\n]+", " ");

                // Collapse multiple spaces (but be careful around operators)
                processed = Regex.Replace(processed, @"\s+", " ");

                // Clean up spaces around operators and punctuation
                processed = Regex.Replace(processed, @"\s*([{}();,])\s*", "$1");

                result.IsValid = true;
                result.ProcessedScript = processed.Trim();
                logger?.LogWarning("[ScriptPreprocessor] Using regex processing");
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Regex processing failed: {ex.Message}");
                logger?.LogError(ex, "[ScriptPreprocessor] Regex fallback failed");
            }

            return result;
        }

        /// <summary>
        /// Remove BOM (Byte Order Mark) if present
        /// </summary>
        private static string RemoveBOM(string content)
        {
            if (content.Length > 0 && content[0] == '\uFEFF')
            {
                return content.Substring(1);
            }
            return content;
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
