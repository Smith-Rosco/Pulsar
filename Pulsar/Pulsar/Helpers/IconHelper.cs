using System.Drawing; // 需要引用 System.Drawing.Common 或 System.Drawing
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
        /// 尝试从路径提取图标并转换为 WPF ImageSource
        /// </summary>
        public static ImageSource? GetIconFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            try
            {
                // 使用 System.Drawing 提取图标
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon == null) return null;

                // 转换为 WPF BitmapSource
                var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                // 冻结对象以跨线程使用
                imageSource.Freeze();
                return imageSource;
            }
            catch
            {
                // 忽略提取失败（如权限不足或非标准格式），返回 null 以便 UI 回退
                return null;
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