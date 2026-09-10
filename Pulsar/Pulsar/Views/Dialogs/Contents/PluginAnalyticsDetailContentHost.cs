using System;
using System.Windows;
using System.Windows.Controls;

namespace Pulsar.Views.Dialogs.Contents
{
    public class PluginAnalyticsDetailContentHost : UserControl
    {
        public PluginAnalyticsDetailContentHost()
        {
            var uri = new Uri("/Pulsar;component/Views/Dialogs/Contents/PluginAnalyticsDetailContent.xaml", UriKind.Relative);
            Content = System.Windows.Application.LoadComponent(uri) as UIElement;
        }
    }
}
