using System.Windows.Controls;
using System.Windows;
using Pulsar.ViewModels.Settings;

namespace Pulsar.Views.TemplateSelectors
{
    public class PluginSettingTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? BooleanTemplate { get; set; }
        public DataTemplate? StringTemplate { get; set; }
        public DataTemplate? SelectionTemplate { get; set; }
        public DataTemplate? PathTemplate { get; set; }
        public DataTemplate? IntegerTemplate { get; set; }
        public DataTemplate? SecretTemplate { get; set; }
        public DataTemplate? MultiSelectTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is BooleanSettingViewModel) return BooleanTemplate;
            if (item is SelectionSettingViewModel) return SelectionTemplate;
            if (item is StringSettingViewModel) return StringTemplate;
            if (item is PathSettingViewModel) return PathTemplate;
            if (item is IntegerSettingViewModel) return IntegerTemplate;
            if (item is SecretSettingViewModel) return SecretTemplate;
            if (item is MultiSelectSettingViewModel) return MultiSelectTemplate;

            return base.SelectTemplate(item, container);
        }
    }
}
