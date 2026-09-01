using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Pulsar.Core.Converters
{
    /// <summary>
    /// EmptyState 按钮可见性：ActionText 为空（无操作）时隐藏，否则采用 ActionVisibility。
    /// 需要 MultiBinding 才能同时判断文本与显式可见性，故不能复用 StringEmptyToVisibilityConverter。
    /// </summary>
    public class ActionVisibilityMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 1 && values[0] is string text && string.IsNullOrEmpty(text))
            {
                return Visibility.Collapsed;
            }

            if (values.Length >= 2 && values[1] is Visibility visibility)
            {
                return visibility;
            }

            return Visibility.Visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
