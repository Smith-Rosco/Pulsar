using System;
using System.Windows;
using System.Windows.Controls;

namespace Pulsar.Views.Dialogs.Contents
{
    public class PluginLogViewerContentHost : UserControl
    {
        public PluginLogViewerContentHost()
        {
            var uri = new Uri("/Pulsar;component/Views/Dialogs/Contents/PluginLogViewerContent.xaml", UriKind.Relative);
            Content = System.Windows.Application.LoadComponent(uri) as UIElement;
        }
    }
}
