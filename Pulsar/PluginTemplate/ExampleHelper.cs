using System;
using Microsoft.Extensions.Logging;

namespace ExamplePlugin
{
    /// <summary>
    /// 示例辅助类 - 演示多文件项目结构
    /// </summary>
    public class ExampleHelper
    {
        private readonly ILogger? _logger;

        public ExampleHelper(ILogger? logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 处理数据的示例方法
        /// </summary>
        public string ProcessData(string input)
        {
            _logger?.LogDebug("[ExampleHelper] Processing data: {Input}", input);

            // 示例：将输入转换为大写并添加前缀
            var result = $"[PROCESSED] {input.ToUpperInvariant()}";

            _logger?.LogDebug("[ExampleHelper] Result: {Result}", result);

            return result;
        }

        /// <summary>
        /// 验证数据的示例方法
        /// </summary>
        public bool ValidateData(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                _logger?.LogWarning("[ExampleHelper] Validation failed: input is empty");
                return false;
            }

            _logger?.LogDebug("[ExampleHelper] Validation passed");
            return true;
        }
    }
}
