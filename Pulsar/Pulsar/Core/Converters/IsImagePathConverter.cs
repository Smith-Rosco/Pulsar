using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;

namespace Pulsar.Core.Converters
{
    /// <summary>
    /// 判断字符串是否为图片路径（文件路径）
    /// 用于在 Unicode 图标和图片图标之间切换显示
    /// </summary>
    public class IsImagePathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string str) return false;
            
            // 检查是否为文件路径（包含盘符、路径分隔符或常见图片扩展名）
            return str.Contains("\\") || str.Contains("/") || 
                   str.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                   str.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
                   str.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                   str.StartsWith("pack://", StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
