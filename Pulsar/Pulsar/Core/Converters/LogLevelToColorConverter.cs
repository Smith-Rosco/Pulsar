using Pulsar.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Pulsar.Core.Converters
{
    /// <summary>
    /// 将插件日志级别转换为对应的颜色画刷
    /// </summary>
    public class LogLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PluginLogLevel level)
            {
                return level switch
                {
                    PluginLogLevel.Critical => new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)),   // Red
                    PluginLogLevel.Error => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 69, 0)),       // OrangeRed
                    PluginLogLevel.Warning => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 0)),    // Orange
                    PluginLogLevel.Info => new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 144, 255)),      // DodgerBlue
                    PluginLogLevel.Debug => new SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 128, 128)),    // Gray
                    _ => new SolidColorBrush(Colors.White)
                };
            }
            return new SolidColorBrush(Colors.White);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
