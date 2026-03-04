// [Path]: Pulsar/Pulsar/Plugins/BookmarkletRunner/ScriptPreprocessor.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NUglify;
using NUglify.JavaScript;

namespace Pulsar.Plugins.Extensions.BookmarkletRunner
{
    /// <summary>
    /// 脚本预处理工具类 - 使用 NUglify 进行 JavaScript 验证和压缩
    /// </summary>
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

        /// <summary>
        /// 处理脚本内容 - 使用 NUglify 进行验证和压缩
        /// </summary>
        /// <param name="content">原始脚本内容</param>
        /// <param name="logger">可选的日志记录器</param>
        /// <returns>验证结果，包含处理后的脚本和错误信息</returns>
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

            // Try NUglify first (primary method)
            try
            {
                result = MinifyWithNUglify(content, logger);
                if (result.IsValid)
                {
                    logger?.LogDebug("[ScriptPreprocessor] NUglify minification successful ({Length} chars)", 
                        result.ProcessedScript.Length);
                    return result;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[ScriptPreprocessor] NUglify failed, using fallback");
            }

            // Fallback to improved regex method
            logger?.LogInformation("[ScriptPreprocessor] Using regex fallback for script processing");
            result = ProcessWithRegex(content, logger);
            return result;
        }

        /// <summary>
        /// Primary: Use NUglify for robust minification and validation
        /// </summary>
        private static ValidationResult MinifyWithNUglify(string content, ILogger? logger)
        {
            var result = new ValidationResult();

            // Remove javascript: prefix if present (we'll add it back later)
            string scriptBody = content.Trim();
            if (scriptBody.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                scriptBody = scriptBody.Substring(11).Trim();
            }

            // Configure NUglify settings
            var settings = new CodeSettings
            {
                RemoveUnneededCode = true,
                StripDebugStatements = false,
                PreserveImportantComments = false,  // Strip all comments
                OutputMode = OutputMode.SingleLine,
                LocalRenaming = LocalRenaming.KeepAll,  // Don't rename variables
                EvalTreatment = EvalTreatment.MakeAllSafe,
                MinifyCode = true
            };

            // Minify
            UglifyResult uglifyResult = Uglify.Js(scriptBody, settings);

            // Check for errors
            if (uglifyResult.HasErrors)
            {
                result.IsValid = false;
                foreach (var error in uglifyResult.Errors)
                {
                    string errorMsg = $"Line {error.StartLine}:{error.StartColumn} - {error.Message}";
                    result.Errors.Add(errorMsg);
                    logger?.LogError("[ScriptPreprocessor] Syntax error: {Error}", errorMsg);
                }
                return result;
            }

            // Success
            result.IsValid = true;
            result.ProcessedScript = uglifyResult.Code?.Trim() ?? string.Empty;

            // Log warnings (non-fatal)
            if (uglifyResult.Errors != null)
            {
                foreach (var warning in uglifyResult.Errors.Where(e => !e.IsError))
                {
                    string warnMsg = $"Line {warning.StartLine}: {warning.Message}";
                    result.Warnings.Add(warnMsg);
                    logger?.LogWarning("[ScriptPreprocessor] {Warning}", warnMsg);
                }
            }

            return result;
        }

        /// <summary>
        /// Fallback: Improved regex-based processing
        /// </summary>
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
                result.Warnings.Add("Used fallback regex processing (less robust than NUglify)");

                logger?.LogWarning("[ScriptPreprocessor] Using regex fallback");
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
