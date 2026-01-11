// [Path]: Pulsar/Pulsar/Views/Controls/JellyOrb.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pulsar.Helpers;

namespace Pulsar.Views.Controls
{
    public partial class JellyOrb : UserControl
    {
        public JellyOrb()
        {
            InitializeComponent();
        }

        // ==========================================
        // 1. 公共依赖属性 (外部输入)
        // ==========================================

        // [核心] 图标键值：路径 (C:\app.exe) 或 字体编码 (E710)
        public static readonly DependencyProperty IconKeyProperty =
            DependencyProperty.Register(nameof(IconKey), typeof(string), typeof(JellyOrb),
                new PropertyMetadata(string.Empty, OnIconKeyChanged));

        // 标签文本
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(JellyOrb),
                new PropertyMetadata(string.Empty));

        // 激活状态 (控制动画)
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(JellyOrb),
                new PropertyMetadata(false));

        // 尺寸 (默认为 50)
        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register(nameof(Size), typeof(double), typeof(JellyOrb),
                new PropertyMetadata(50.0));

        public string IconKey
        {
            get => (string)GetValue(IconKeyProperty);
            set => SetValue(IconKeyProperty, value);
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public double Size
        {
            get => (double)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        // ==========================================
        // 2. 私有依赖属性 (内部渲染状态)
        // ==========================================
        // 注意：这里使用只读依赖属性的简化版（直接用 private set 对于 UserControl 内部绑定也够用了，
        // 但为了 XAML 绑定稳定，我们注册私有的 DependencyProperty）

        public static readonly DependencyProperty RenderImageProperty =
            DependencyProperty.Register("RenderImage", typeof(ImageSource), typeof(JellyOrb), new PropertyMetadata(null));

        public static readonly DependencyProperty RenderGlyphProperty =
            DependencyProperty.Register("RenderGlyph", typeof(string), typeof(JellyOrb), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ShowImageProperty =
            DependencyProperty.Register("ShowImage", typeof(bool), typeof(JellyOrb), new PropertyMetadata(false));

        public ImageSource RenderImage
        {
            get => (ImageSource)GetValue(RenderImageProperty);
            set => SetValue(RenderImageProperty, value);
        }

        public string RenderGlyph
        {
            get => (string)GetValue(RenderGlyphProperty);
            set => SetValue(RenderGlyphProperty, value);
        }

        public bool ShowImage
        {
            get => (bool)GetValue(ShowImageProperty);
            set => SetValue(ShowImageProperty, value);
        }

        // ==========================================
        // 3. 逻辑处理
        // ==========================================

        private static void OnIconKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is JellyOrb orb)
            {
                orb.RefreshIcon(e.NewValue as string);
            }
        }

        private void RefreshIcon(string key)
        {
            // 重置状态
            RenderImage = null;
            RenderGlyph = string.Empty;
            ShowImage = false;

            if (string.IsNullOrWhiteSpace(key)) return;

            // 1. 尝试作为路径处理
            if (key.Contains("\\") || key.Contains("."))
            {
                var img = IconHelper.GetIconFromPath(key);
                if (img != null)
                {
                    RenderImage = img;
                    ShowImage = true;
                    return;
                }
            }

            // 2. 尝试作为字体图标处理
            var glyph = IconHelper.GetGlyph(key);
            if (!string.IsNullOrEmpty(glyph))
            {
                RenderGlyph = glyph;
                ShowImage = false;
            }
            // 如果都不是，可以显示首字母或者默认问号，这里暂留空
        }
    }
}