using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Pulsar.Features.Tutorial.Services.Prerequisites;

namespace Pulsar.Views.Converters
{
    public class PrerequisiteStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PrerequisiteStatus status)
            {
                return status switch
                {
                    PrerequisiteStatus.Met => new SolidColorBrush(Color.FromRgb(39, 174, 96)),
                    PrerequisiteStatus.NotMet => new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                    PrerequisiteStatus.Pending => new SolidColorBrush(Color.FromRgb(243, 156, 18)),
                    _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
                };
            }
            return new SolidColorBrush(Color.FromRgb(149, 165, 166));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
