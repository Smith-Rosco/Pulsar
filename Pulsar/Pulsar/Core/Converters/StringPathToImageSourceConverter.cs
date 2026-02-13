using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Pulsar.Core.Converters
{
    /// <summary>
    /// Safely converts a string path to an ImageSource.
    /// Returns null if the string is not a valid file path (e.g. a font icon character),
    /// preventing IOException binding errors in Image controls.
    /// </summary>
    public class StringPathToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string str || string.IsNullOrWhiteSpace(str))
                return null;

            // Simple heuristic to distinguish file paths from font glyphs (single char)
            // A file path typically has length > 1 and contains directory separators or extensions
            bool isLikelyPath = str.Length > 1 && (
                str.Contains("\\") || 
                str.Contains("/") || 
                str.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                str.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
                str.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                str.StartsWith("pack://", StringComparison.OrdinalIgnoreCase)
            );

            if (!isLikelyPath)
                return null;

            try
            {
                // Create BitmapImage with CacheOption to prevent file locking
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(str, UriKind.RelativeOrAbsolute);
                bitmap.EndInit();
                bitmap.Freeze(); // Make it cross-thread accessible
                return bitmap;
            }
            catch (Exception)
            {
                // Return null on any error (file not found, invalid format) to fail gracefully
                return null; 
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
