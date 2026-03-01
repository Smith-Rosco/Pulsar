using System;
using System.Globalization;
using System.Windows.Data;

namespace Pulsar.Core.Converters
{
    /// <summary>
    /// 将 UTC 时间转换为本地时间字符串
    /// </summary>
    public class UtcToLocalTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                // 如果是 UTC 时间，转换为本地时间
                if (dateTime.Kind == DateTimeKind.Utc)
                {
                    dateTime = dateTime.ToLocalTime();
                }
                
                // 格式化为本地时间字符串
                return dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
