using System.Windows;
using System.Windows.Controls;

namespace Pulsar.Views.Controls
{
    /// <summary>
    /// 设置项标准行布局：左列标题 + 可选描述，右列注入的编辑控件。
    /// 控件（Content）由调用页面注入，其绑定上下文保持在页面内，
    /// 本控件只负责排版，不持有任何命令或业务逻辑。
    /// </summary>
    public partial class SettingsRow : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty));

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

        public SettingsRow()
        {
            InitializeComponent();
        }
    }
}
