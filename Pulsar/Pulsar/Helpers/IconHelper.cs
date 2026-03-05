// [Path]: Pulsar/Pulsar/Helpers/IconHelper.cs

using System;
using System.Collections.Concurrent;
using System.Drawing; // System.Drawing.Common
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace Pulsar.Helpers
{
    public static class IconHelper
    {
        public static ILogger? Logger { get; set; }
        
        // Cache for GetGlyph results to avoid repeated parsing
        private static readonly ConcurrentDictionary<string, string> _glyphCache = new();
        /// <summary>
        /// 智能获取图标：如果是图片文件则直接加载，如果是程序则提取图标
        /// </summary>
        public static ImageSource? GetIconFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            try
            {
                // [Fix] 1. 先判断是否为图片格式 (PNG, ICO, JPG, BMP)
                string ext = Path.GetExtension(path).ToLower();
                if (ext == ".png" || ext == ".ico" || ext == ".jpg" || ext == ".bmp")
                {
                    return LoadImageDirectly(path);
                }

                // [Fix] 2. 如果不是图片，再尝试作为程序提取图标 (EXE, LNK)
                return ExtractExeIcon(path);
            }
            catch
            {
                return null;
            }
        }

        // 新增：直接加载图片文件的方法
        private static ImageSource LoadImageDirectly(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad; // 加载后释放文件锁
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze(); // 必须冻结以跨线程使用
            return bitmap;
        }

        // 原有的逻辑，改名为 ExtractExeIcon 专门处理程序
        private static ImageSource? ExtractExeIcon(string path)
        {
            try
            {
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon == null) return null;

                var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                imageSource.Freeze();
                return imageSource;
            }
            catch
            {
                return null;
            }
        }

        public static string? SaveIconToCache(ImageSource image, string processName)
        {
            if (image is not BitmapSource bitmapSource) return null;

            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "Pulsar", "Cache", "Icons");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                // Use ProcessName for filename, sanitized
                string safeName = string.Join("_", processName.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(folder, $"{safeName}.png");

                // [Optimization] If file exists, check if valid and return
                if (File.Exists(filePath))
                {
                    // Basic check: is file accessible?
                    try 
                    {
                        using (var fs = File.OpenRead(filePath)) { if (fs.Length > 0) return filePath; }
                    }
                    catch { /* File locked or corrupted, try overwrite */ }
                }

                SaveBitmap(bitmapSource, filePath);
                return filePath;
            }
            catch (Exception ex)
            {
                Logger?.LogDebug(ex, "[IconHelper] Save failed");
                return null;
            }
        }

        private static void SaveBitmap(BitmapSource bitmapSource, string path)
        {
            using (var fileStream = new FileStream(path, FileMode.Create))
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(fileStream);
            }
        }

        public static string GetGlyph(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            
            // Check cache first
            return _glyphCache.GetOrAdd(key, k =>
            {
                // 1. Detect Explicit Hex Prefix (User INTENDS hex)
                bool hasHexPrefix = k.StartsWith("0x", StringComparison.OrdinalIgnoreCase) || 
                                    k.StartsWith("u+", StringComparison.OrdinalIgnoreCase) || 
                                    k.StartsWith("\\u", StringComparison.OrdinalIgnoreCase);

                string cleanKey = k.Trim().Replace("0x", "").Replace("u+", "").Replace("\\u", "");

                // 2. Try Parse Hex
                if (int.TryParse(cleanKey, System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
                {
                    // If explicit prefix, trust it.
                    if (hasHexPrefix) return char.ConvertFromUtf32(codePoint);

                    // If implicit (no prefix), only treat as Hex if it looks like a symbol code point
                    // Segoe Fluent: E000 - F8FF (Private Use Area)
                    // Emoji: 1F000+
                    // Avoid ambiguous ASCII range (e.g., "Add" -> 0xADD) unless explicit
                    if (codePoint >= 0xE000) 
                    {
                        return char.ConvertFromUtf32(codePoint);
                    }
                }
                
                // 3. Return as-is (Allows "VBA", "🚀", "🧑‍💻", "CMD")
                return k.Trim();
            });
        }
    }
}
