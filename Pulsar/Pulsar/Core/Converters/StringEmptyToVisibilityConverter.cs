using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Pulsar.Core.Converters
{
    /// <summary>
    /// 字符串为空时返回 Collapsed，否则返回 Visible
    /// 用于根据字符串内容控制 UI 元素的可见性
    /// </summary>
    public class StringEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
