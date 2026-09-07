// [Path]: Pulsar/Pulsar/Helpers/ComboBoxSelectionLeftAlign.cs

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Pulsar.Helpers
{
    /// <summary>
    /// 修正 ComboBox 收起态选中内容居中的问题。
    ///
    /// 根因（WPF 核心行为，与 Wpf.Ui 版本无关）：当 ComboBox 的选中项内容是 UIElement
    /// （如显式声明的 ComboBoxItem 内含 TextBlock/StackPanel）时，收起态的 SelectionBoxItem
    /// 不是内容本身，而是 WPF 自动生成的一个固定宽度（= 内容期望宽）的 Rectangle + VisualBrush
    /// 快照（避免把下拉项从 Popup 中"搬走"）。该 Rectangle 固定宽 + 默认 Stretch 对齐 = 在
    /// PART_ContentPresenter 中水平居中。字符串项（ItemsSource + DisplayMemberPath）则生成
    /// 可撑满的 TextBlock，贴左渲染 —— 这就是"有的左对齐有的居中"的来源。
    ///
    /// 修复：在 Loaded / SelectionChanged 时找到模板中的 PART_ContentPresenter，把自动生成的
    /// Rectangle 设为 HorizontalAlignment.Left。不触碰 ComboBox 模板本身 —— 切勿改用
    /// ComboBox.HorizontalContentAlignment（那会同时缩放"内容+箭头+点击层"整个 grid，
    /// 导致箭头左移、点击区缩小，Wpf.Ui 模板的内层 Grid 对齐绑在该属性上）。
    ///
    /// 使用方式（EnableLeftAlign 可继承，设一次即可覆盖整棵子树的所有 ComboBox）：
    ///   xmlns:helpers="clr-namespace:Pulsar.Helpers"
    ///   <Grid helpers:ComboBoxSelectionLeftAlign.EnableLeftAlign="True">
    /// </summary>
    public static class ComboBoxSelectionLeftAlign
    {
        public static readonly DependencyProperty EnableLeftAlignProperty =
            DependencyProperty.RegisterAttached(
                "EnableLeftAlign",
                typeof(bool),
                typeof(ComboBoxSelectionLeftAlign),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits, OnEnableChanged));

        public static bool GetEnableLeftAlign(DependencyObject obj) => (bool)obj.GetValue(EnableLeftAlignProperty);

        public static void SetEnableLeftAlign(DependencyObject obj, bool value) => obj.SetValue(EnableLeftAlignProperty, value);

        private static void OnEnableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ComboBox combo)
                return;

            if ((bool)e.NewValue)
            {
                combo.Loaded += OnLoaded;
                combo.SelectionChanged += OnSelectionChanged;
            }
            else
            {
                combo.Loaded -= OnLoaded;
                combo.SelectionChanged -= OnSelectionChanged;
            }
        }

        private static void OnLoaded(object sender, RoutedEventArgs e) => Apply((ComboBox)sender);

        private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e) => Apply((ComboBox)sender);

        private static void Apply(ComboBox combo)
        {
            // SelectionBoxItem 每次选中项变化都会重新生成，需在每次变化后重新对齐。
            var presenter = combo.Template?.FindName("PART_ContentPresenter", combo) as ContentPresenter;
            if (presenter is null || VisualTreeHelper.GetChildrenCount(presenter) == 0)
                return;
            if (VisualTreeHelper.GetChild(presenter, 0) is Rectangle rect && rect.HorizontalAlignment != HorizontalAlignment.Left)
                rect.HorizontalAlignment = HorizontalAlignment.Left;
        }
    }
}
