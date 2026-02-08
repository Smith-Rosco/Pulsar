using System.Windows;
using System.Windows.Controls;
using Pulsar.Models;

namespace Pulsar.Views.Selectors
{
    public class SlotItemFieldSelector : DataTemplateSelector
    {
        public DataTemplate? LauncherFieldsTemplate { get; set; }
        public DataTemplate? CommandFieldsTemplate { get; set; }
        public DataTemplate? BookmarkletFieldsTemplate { get; set; }
        public DataTemplate? VbaRunnerFieldsTemplate { get; set; }
        public DataTemplate? SecretFieldsTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
             if (item is PluginSlot slot)
            {
                if (slot.PluginId == "com.pulsar.winswitcher")
                    return LauncherFieldsTemplate;

                if (slot.PluginId == "com.pulsar.bookmarklet")
                    return BookmarkletFieldsTemplate;

                if (slot.PluginId == "com.pulsar.vbarunner")
                    return VbaRunnerFieldsTemplate;

                if (slot.PluginId == "com.pulsar.pki")
                    return SecretFieldsTemplate;

                return CommandFieldsTemplate;
            }
            return base.SelectTemplate(item, container);
        }
    }
}