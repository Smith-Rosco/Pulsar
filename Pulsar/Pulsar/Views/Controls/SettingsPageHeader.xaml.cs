using System.Windows;
using System.Windows.Controls;

namespace Pulsar.Views.Controls
{
    public partial class SettingsPageHeader : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingsPageHeader), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsPageHeader), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ActionsContentProperty =
            DependencyProperty.Register(nameof(ActionsContent), typeof(object), typeof(SettingsPageHeader), new PropertyMetadata(null));

        public static readonly DependencyProperty ActionsContentTemplateProperty =
            DependencyProperty.Register(nameof(ActionsContentTemplate), typeof(DataTemplate), typeof(SettingsPageHeader), new PropertyMetadata(null));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public object ActionsContent
        {
            get => GetValue(ActionsContentProperty);
            set => SetValue(ActionsContentProperty, value);
        }

        public DataTemplate ActionsContentTemplate
        {
            get => (DataTemplate)GetValue(ActionsContentTemplateProperty);
            set => SetValue(ActionsContentTemplateProperty, value);
        }

        public SettingsPageHeader()
        {
            InitializeComponent();
        }
    }
}
