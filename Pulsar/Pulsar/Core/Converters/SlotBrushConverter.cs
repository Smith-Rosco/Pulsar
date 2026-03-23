using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Pulsar.Core.Converters
{
    public class SlotBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string tone = parameter as string ?? string.Empty;

            if (value is not string key || string.IsNullOrWhiteSpace(key))
            {
                return ResolveBrush(FallbackKey(tone));
            }

            return tone switch
            {
                "Type" => ResolveBrush(key),
                "Health" => ResolveBrush(key),
                _ => ResolveBrush(FallbackKey(tone))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Windows.DependencyProperty.UnsetValue;
        }

        private static System.Windows.Media.Brush ResolveBrush(string resourceKey)
        {
            object resource = System.Windows.Application.Current.TryFindResource(resourceKey);
            if (resource is System.Windows.Media.Brush brush)
            {
                return brush;
            }

            return System.Windows.Media.Brushes.Transparent;
        }

        private static string FallbackKey(string tone)
        {
            return tone == "Health" ? "SlotHealthBrushReady" : "SlotTypeBrushDefault";
        }
    }
}
