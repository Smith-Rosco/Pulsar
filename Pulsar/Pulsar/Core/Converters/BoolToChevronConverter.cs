using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace Pulsar.Core.Converters
{
    /// <summary>
    /// Converts boolean to Chevron icon (Down when collapsed, Up when expanded)
    /// </summary>
    public class BoolToChevronConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isExpanded)
            {
                return isExpanded ? SymbolRegular.ChevronUp24 : SymbolRegular.ChevronDown24;
            }
            return SymbolRegular.ChevronDown24;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
