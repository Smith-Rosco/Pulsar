using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Pulsar.Core.Converters
{
    public class SlotBrushConverter : IValueConverter, IMultiValueConverter
    {
        private static readonly System.Windows.Media.Brush TypeFallbackBrush = CreateFallbackBrush(System.Windows.Media.Color.FromRgb(0x4B, 0x55, 0x63));
        private static readonly System.Windows.Media.Brush HealthFallbackBrush = CreateFallbackBrush(System.Windows.Media.Color.FromRgb(0x15, 0x80, 0x3D));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string tone = parameter as string ?? string.Empty;

            if (value is not string key || string.IsNullOrWhiteSpace(key))
            {
                return ResolveBrush(null, null, tone);
            }

            return ResolveBrush(key, null, tone);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            string? resourceKey = values.Length > 0 ? values[0] as string : null;
            FrameworkElement? resourceScope = values.Length > 1 ? values[1] as FrameworkElement : null;
            string tone = parameter as string ?? string.Empty;

            return ResolveBrush(resourceKey, resourceScope, tone);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            return targetTypes.Length == 0
                ? Array.Empty<object>()
                : new object[targetTypes.Length];
        }

        private static System.Windows.Media.Brush ResolveBrush(string? resourceKey, FrameworkElement? resourceScope, string tone)
        {
            if (TryResolveBrush(resourceScope, resourceKey, out System.Windows.Media.Brush? brush))
            {
                return brush!;
            }

            string fallbackKey = FallbackKey(tone);
            if (!string.Equals(resourceKey, fallbackKey, StringComparison.Ordinal)
                && TryResolveBrush(resourceScope, fallbackKey, out brush))
            {
                return brush!;
            }

            return tone == "Health" ? HealthFallbackBrush : TypeFallbackBrush;
        }

        private static bool TryResolveBrush(FrameworkElement? resourceScope, string? resourceKey, out System.Windows.Media.Brush? brush)
        {
            brush = null;
            if (resourceScope == null || string.IsNullOrWhiteSpace(resourceKey))
            {
                return false;
            }

            object resource = resourceScope.TryFindResource(resourceKey);
            if (resource is System.Windows.Media.Brush resolvedBrush)
            {
                brush = resolvedBrush;
                return true;
            }

            return false;
        }

        private static string FallbackKey(string tone)
        {
            return tone == "Health" ? "SlotHealthBrushReady" : "SlotTypeBrushDefault";
        }

        private static System.Windows.Media.Brush CreateFallbackBrush(System.Windows.Media.Color color)
        {
            var brush = new System.Windows.Media.SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
