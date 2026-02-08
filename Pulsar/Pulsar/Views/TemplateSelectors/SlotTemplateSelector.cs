using Pulsar.Models;
using System.Windows;
using System.Windows.Controls;

namespace Pulsar.Views.TemplateSelectors
{
    public class SlotTemplateSelector : DataTemplateSelector
    {
        public DataTemplate LauncherTemplate { get; set; } = null!;
        public DataTemplate CommandTemplate { get; set; } = null!;
        public DataTemplate SecretTemplate { get; set; } = null!;

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is PluginSlot slot)
            {
                if (slot.PluginId == "com.pulsar.winswitcher")
                    return LauncherTemplate;

                if (slot.PluginId == "com.pulsar.pki")
                    return SecretTemplate;

                return CommandTemplate; // Default to command
            }

            return base.SelectTemplate(item, container);
        }
    }
}