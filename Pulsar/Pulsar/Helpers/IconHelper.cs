// [Path]: Pulsar/Pulsar/Helpers/IconHelper.cs

using System;
using System.Drawing; // System.Drawing.Common
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Pulsar.Helpers
{
    public static class IconHelper
    {
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

        // [New] 之前提供的保存方法 (为了完整性再次列出，你不需要修改这部分如果已经添加了)
        public static string SaveIconToCache(ImageSource image, string processName)
        {
            if (image is not BitmapSource bitmapSource) return null;

            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "Pulsar", "Cache", "Icons");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string safeName = string.Join("_", processName.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(folder, $"{safeName}.png");

                // [Optimization] 如果文件已存在且有效，直接返回，避免重复写入导致的 IO 冲突
                if (File.Exists(filePath))
                {
                    try
                    {
                        using (var fs = File.OpenRead(filePath))
                        {
                            if (fs.Length > 0) return filePath;
                        }
                    }
                    catch
                    {
                        // 如果读取失败，说明文件可能有问题或被锁，尝试覆盖或生成新名
                    }
                }

                try
                {
                    SaveBitmap(bitmapSource, filePath);
                    return filePath;
                }
                catch (IOException)
                {
                    // [Fallback] 如果主文件被占用，生成带时间戳的副本
                    string timestamp = DateTime.Now.Ticks.ToString();
                    string altPath = Path.Combine(folder, $"{safeName}_{timestamp}.png");
                    SaveBitmap(bitmapSource, altPath);
                    return altPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IconHelper] Save failed: {ex.Message}");
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
            string cleanKey = key.Trim().Replace("0x", "").Replace("u+", "").Replace("\\u", "");

            if (int.TryParse(cleanKey, System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
            {
                return char.ConvertFromUtf32(codePoint);
            }
            return string.Empty;
        }
    }
}