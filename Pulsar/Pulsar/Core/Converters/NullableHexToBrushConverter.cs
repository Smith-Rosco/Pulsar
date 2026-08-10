using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Pulsar.Core.Converters
{
    /// <summary>
    /// Converts a #RRGGBB (or named) color string to a Brush.
    /// Returns null for empty/invalid input so that the target falls back to theme resources
    /// (mirrors the radial menu's behavior for slots without a custom color).
    /// </summary>
    public class NullableHexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex && !string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    return brush;
                }
                catch
                {
                    return null!;
                }
            }

            return null!;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
