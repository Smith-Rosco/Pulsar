// [File]: Pulsar/Views/TemplateSelectors/SlotTemplateSelector.cs
using Pulsar.Features.Pki.Models;
using Pulsar.Models;
using System.Windows;
using System.Windows.Controls;

namespace Pulsar.Views.TemplateSelectors
{
    public class SlotTemplateSelector : DataTemplateSelector
    {
        // 对应 XAML 中的 LauncherTemplate
        public DataTemplate LauncherTemplate { get; set; }

        // 对应 XAML 中的 CommandTemplate (旧代码可能叫 ActionTemplate，导致报错)
        public DataTemplate CommandTemplate { get; set; }

        // [New] 新增 Secret 模板属性
        public DataTemplate SecretTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is LauncherItem)
                return LauncherTemplate;

            if (item is CommandItem)
                return CommandTemplate;
            // [New] 识别 SecretItem
            if (item is SecretItem)
                return SecretTemplate;

            return base.SelectTemplate(item, container);
        }

    }
}