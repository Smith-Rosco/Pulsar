using System;
using System.Globalization;
using System.Windows.Data;

namespace Pulsar.Core.Converters
{
    public class StringToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return bool.TryParse(stringValue, out bool boolValue) && boolValue;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue.ToString();
            }
            return "false";
        }
    }
}
