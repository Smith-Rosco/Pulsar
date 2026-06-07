// [Path]: Pulsar/Pulsar/Helpers/IconHelper.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing; // System.Drawing.Common
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pulsar.Helpers
{
    /// <summary>
    /// Icon helper for loading and caching icons from various sources
    /// </summary>
    public static class IconHelper
    {
        private static ILogger _logger = NullLogger.Instance;
        
        /// <summary>
        /// Initialize the logger for IconHelper. Should be called once during application startup.
        /// </summary>
        /// <param name="loggerFactory">Logger factory from DI container</param>
        public static void Initialize(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger("IconHelper") ?? NullLogger.Instance;
        }
        
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
                _logger.LogDebug(ex, "Failed to save icon to cache for process {ProcessName}", processName);
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

        public static System.Windows.Media.FontFamily GetGlyphFontFamily(string glyph)
        {
            if (string.IsNullOrEmpty(glyph))
            {
                return new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets, Segoe UI Emoji");
            }

            char first = glyph[0];
            return first >= 0xE000 && first <= 0xF8FF
                ? new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets")
                : new System.Windows.Media.FontFamily("Segoe UI, Segoe UI Emoji, Microsoft YaHei UI");
        }

        // ============================================================
        // GlyphData reverse-lookup dictionaries (lazy-built)
        // ============================================================
        private static Dictionary<string, IconItem>? _byCode;
        private static Dictionary<string, IconItem>? _byCharacter;

        private static void EnsureLookups()
        {
            if (_byCode != null) return;
            var byCode = new Dictionary<string, IconItem>(StringComparer.OrdinalIgnoreCase);
            var byChar = new Dictionary<string, IconItem>(StringComparer.Ordinal);
            foreach (var item in GlyphData.CommonIcons)
            {
                byCode[item.Code] = item;
                if (!string.IsNullOrEmpty(item.Character))
                    byChar[item.Character] = item;
            }
            _byCode = byCode;
            _byCharacter = byChar;
        }

        /// <summary>
        /// Resolve an IconKey to a human-readable display string.
        /// "E756" → "E756 · CommandPrompt"
        /// "C:\path\chrome.png" → "chrome.png"
        /// PUA glyph → lookup Code then display
        /// Empty → ""
        /// </summary>
        public static string ResolveIconDisplay(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;

            // File path → show filename
            if (key.Contains('\\') || key.Contains('/') || key.Contains('.'))
            {
                try { return Path.GetFileName(key); }
                catch { return key; }
            }

            EnsureLookups();

            // Try lookup by Code (hex)
            if (_byCode!.TryGetValue(key.Trim(), out var byCode))
                return $"{byCode.Code} · {byCode.Name}";

            // Try lookup by Character (PUA glyph)
            if (_byCharacter!.TryGetValue(key, out var byChar))
                return $"{byChar.Code} · {byChar.Name}";

            // Try parsing as hex → then lookup
            var glyph = GetGlyph(key);
            if (!string.IsNullOrEmpty(glyph) && glyph != key.Trim())
            {
                if (_byCharacter.TryGetValue(glyph, out var byGlyph))
                    return $"{byGlyph.Code} · {byGlyph.Name}";
            }

            // Fallback: raw key
            return key.Trim();
        }

        /// <summary>
        /// Normalize user-pasted input to a canonical IconKey.
        /// "0xE756" → "E756"
        /// "\uE756" → "E756"
        /// "CommandPrompt" → "E756"
        /// PUA glyph → "E756"
        /// File path → path as-is
        /// </summary>
        public static string NormalizeIconKey(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            input = input.Trim();

            // File path
            if (input.Contains('\\') || input.Contains('/') || (input.Contains('.') && input.Length < 260))
                return input;

            EnsureLookups();

            // Explicit hex prefix → convert to plain uppercase hex
            bool hasHexPrefix = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                             || input.StartsWith("u+", StringComparison.OrdinalIgnoreCase)
                             || input.StartsWith("\\u", StringComparison.OrdinalIgnoreCase);
            string clean = input.Replace("0x", "").Replace("u+", "").Replace("\\u", "").Replace("0X", "");
            if (hasHexPrefix && int.TryParse(clean, System.Globalization.NumberStyles.HexNumber, null, out int cp))
                return cp.ToString("X");

            // Already a valid hex code known to GlyphData
            if (_byCode!.ContainsKey(input))
                return input.ToUpper();

            // Single PUA character → lookup Code
            if (input.Length <= 2 && input[0] >= 0xE000)
            {
                if (_byCharacter!.TryGetValue(input, out var byChar))
                    return byChar.Code;
                return ((int)input[0]).ToString("X");
            }

            // Name match → return Code
            var byName = _byCode.Values.FirstOrDefault(i => string.Equals(i.Name, input, StringComparison.OrdinalIgnoreCase));
            if (byName != null) return byName.Code;

            // Try parsing as implicit hex (only if it looks like one and is in PUA range)
            if (input.Length <= 6 && int.TryParse(input, System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
            {
                if (codePoint >= 0xE000)
                    return codePoint.ToString("X");
            }

            return input.ToUpper();
        }
    }
}
