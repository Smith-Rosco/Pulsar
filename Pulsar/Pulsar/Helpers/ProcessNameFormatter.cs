using System;
using System.Globalization;

namespace Pulsar.Helpers
{
    /// <summary>
    /// 进程名格式化工具 - 统一处理存储规范化和显示格式化
    /// </summary>
    public static class ProcessNameFormatter
    {
        /// <summary>
        /// 规范化为存储键名 (全大写)
        /// 用于配置文件存储和字典查找
        /// </summary>
        /// <param name="processName">原始进程名</param>
        /// <returns>规范化的全大写进程名</returns>
        public static string Normalize(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return processName;

            return processName.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// 格式化为显示名称 (首字母大写)
        /// 用于 UI 显示，提升用户体验
        /// </summary>
        /// <param name="processName">原始进程名 (通常为全大写)</param>
        /// <returns>首字母大写的友好显示名称</returns>
        /// <example>
        /// ToDisplayName("EXCEL") => "Excel"
        /// ToDisplayName("GOOGLE CHROME") => "Google Chrome"
        /// ToDisplayName("vscode") => "Vscode"
        /// </example>
        public static string ToDisplayName(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return processName;

            // 使用 TextInfo.ToTitleCase 实现首字母大写
            // 注意: ToTitleCase 要求输入为小写才能正确工作
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(processName.ToLower());
        }

        /// <summary>
        /// 从文件路径提取进程名并规范化
        /// </summary>
        /// <param name="filePath">可执行文件路径</param>
        /// <returns>规范化的进程名 (无 .exe 后缀)</returns>
        public static string NormalizeFromPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return string.Empty;

            var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            return Normalize(fileName);
        }
    }
}
