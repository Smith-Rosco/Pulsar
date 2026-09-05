using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Pulsar.Models;

namespace Pulsar.Core.Converters
{
    /// <summary>
    /// 分析页排序表头箭头可见性：仅当该列是当前排序列且方向匹配时显示箭头。
    /// MultiBinding values: [0]=SortColumn(enum), [1]=SortAscending(bool)；
    /// ConverterParameter 形如 "Executions|Up" / "Executions|Down"。
    /// </summary>
    public class SortArrowVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is not string spec)
                return Visibility.Collapsed;

            var parts = spec.Split('|');
            if (parts.Length != 2)
                return Visibility.Collapsed;

            if (values.Length < 2 || values[0] is not SortColumn column || values[1] is not bool ascending)
                return Visibility.Collapsed;

            bool columnMatches = string.Equals(column.ToString(), parts[0], StringComparison.OrdinalIgnoreCase);
            bool wantsAscending = string.Equals(parts[1], "Up", StringComparison.OrdinalIgnoreCase);

            return columnMatches && ascending == wantsAscending
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
