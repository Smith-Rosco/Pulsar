// [Path]: Pulsar/Pulsar/Helpers/IconExtractor.cs

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Pulsar.Helpers
{
    /// <summary>
    /// 应用图标提取器 - 从 .exe 或 .ico 文件提取图标并缓存为 PNG
    /// </summary>
    public class IconExtractor
    {
        private readonly ILogger? _logger;
        private readonly string _iconCacheDir;
        private const string LogPrefix = "[IconExtractor]";

        public IconExtractor(ILogger? logger = null)
        {
            _logger = logger;
            
            // 图标缓存目录: %AppData%\Pulsar\Icons\
            _iconCacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar",
                "Icons");
            
            Directory.CreateDirectory(_iconCacheDir);
        }

        /// <summary>
        /// 提取应用图标并缓存为 PNG 文件
        /// </summary>
        /// <param name="exePath">可执行文件路径</param>
        /// <param name="processName">进程名称（用作缓存文件名）</param>
        /// <returns>缓存的 PNG 文件路径，失败返回 null</returns>
        public string? ExtractAndCacheIcon(string exePath, string processName)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                _logger?.LogWarning($"{LogPrefix} Executable not found: {exePath}");
                return null;
            }

            try
            {
                // 生成缓存文件路径
                var cacheFileName = $"{SanitizeFileName(processName)}.png";
                var cachePath = Path.Combine(_iconCacheDir, cacheFileName);

                // 如果缓存已存在，直接返回
                if (File.Exists(cachePath))
                {
                    _logger?.LogTrace($"{LogPrefix} Using cached icon: {cachePath}");
                    return cachePath;
                }

                // 提取图标
                Icon? icon = null;
                
                // 方法 1: 从 .exe 提取关联图标
                try
                {
                    icon = Icon.ExtractAssociatedIcon(exePath);
                }
                catch (Exception ex)
                {
                    _logger?.LogTrace(ex, $"{LogPrefix} Failed to extract associated icon from {exePath}");
                }

                // 方法 2: 如果方法 1 失败，尝试使用 ExtractIcon API
                if (icon == null)
                {
                    try
                    {
                        IntPtr hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
                        if (hIcon != IntPtr.Zero && hIcon != new IntPtr(1))
                        {
                            icon = Icon.FromHandle(hIcon);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogTrace(ex, $"{LogPrefix} Failed to extract icon using ExtractIcon API");
                    }
                }

                if (icon == null)
                {
                    _logger?.LogWarning($"{LogPrefix} No icon found for {exePath}");
                    return null;
                }

                // 转换为 Bitmap 并保存为 PNG
                using (icon)
                using (var bitmap = icon.ToBitmap())
                {
                    bitmap.Save(cachePath, ImageFormat.Png);
                }

                _logger?.LogDebug($"{LogPrefix} Extracted and cached icon: {processName} -> {cachePath}");
                return cachePath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"{LogPrefix} Failed to extract icon from {exePath}");
                return null;
            }
        }

        /// <summary>
        /// 从图标位置字符串提取图标（格式: "path,index"）
        /// </summary>
        public string? ExtractIconFromLocation(string iconLocation, string processName)
        {
            if (string.IsNullOrEmpty(iconLocation))
                return null;

            try
            {
                // 解析图标位置 (格式: "C:\path\to\file.exe,0")
                var parts = iconLocation.Split(',');
                var iconPath = parts[0].Trim();
                var iconIndex = parts.Length > 1 && int.TryParse(parts[1], out var idx) ? idx : 0;

                if (!File.Exists(iconPath))
                    return null;

                // 生成缓存文件路径
                var cacheFileName = $"{SanitizeFileName(processName)}.png";
                var cachePath = Path.Combine(_iconCacheDir, cacheFileName);

                // 如果缓存已存在，直接返回
                if (File.Exists(cachePath))
                {
                    _logger?.LogTrace($"{LogPrefix} Using cached icon: {cachePath}");
                    return cachePath;
                }

                // 提取指定索引的图标
                IntPtr hIcon = ExtractIcon(IntPtr.Zero, iconPath, iconIndex);
                if (hIcon == IntPtr.Zero || hIcon == new IntPtr(1))
                {
                    // 如果指定索引失败，尝试索引 0
                    if (iconIndex != 0)
                    {
                        hIcon = ExtractIcon(IntPtr.Zero, iconPath, 0);
                    }
                }

                if (hIcon == IntPtr.Zero || hIcon == new IntPtr(1))
                {
                    _logger?.LogWarning($"{LogPrefix} Failed to extract icon from {iconLocation}");
                    return null;
                }

                // 转换为图标并保存
                using (var icon = Icon.FromHandle(hIcon))
                using (var bitmap = icon.ToBitmap())
                {
                    bitmap.Save(cachePath, ImageFormat.Png);
                }

                DestroyIcon(hIcon);

                _logger?.LogDebug($"{LogPrefix} Extracted icon from location: {iconLocation} -> {cachePath}");
                return cachePath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"{LogPrefix} Failed to extract icon from location: {iconLocation}");
                return null;
            }
        }

        /// <summary>
        /// 清理文件名中的非法字符
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        /// <summary>
        /// 获取图标缓存目录路径
        /// </summary>
        public string GetIconCacheDirectory() => _iconCacheDir;

        #region Win32 API

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        #endregion
    }
}
