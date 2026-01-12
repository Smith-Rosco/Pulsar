using Pulsar.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media; // VisualTreeHelper, CompositionTarget
using Forms = System.Windows.Forms;

// [保持] 强制指定 Point 为 WPF 类型
using Point = System.Windows.Point;

namespace Pulsar.Views.Controls
{
    public partial class JellyOrb : UserControl
    {
        // ============================
        // 运动算法参数 (已改为 Lerp 插值模式)
        // ============================

        // 当前的位移量 (X, Y)
        private Vector _currentOffset = new Vector(0, 0);

        // [关键参数] 平滑因子 (0.05 - 0.2)
        // 值越小，跟随越慢、越粘稠；值越大，响应越快。
        // 0.1 是一个非常优雅的数值，像是在蜂蜜中移动。
        private const double SmoothFactor = 0.1;

        // 视差强度: 鼠标移动 100px，Orb 移动 12px
        private const double ParallaxIntensity = 0.12;

        // 最大位移限制 (像素): 锁死活动范围
        private const double MaxOffsetLimit = 12.0;

        public JellyOrb()
        {
            InitializeComponent();
            this.Loaded += (s, e) => CompositionTarget.Rendering += OnRenderFrame;
            this.Unloaded += (s, e) => CompositionTarget.Rendering -= OnRenderFrame;

            // [Fix 3.1] 监听可见性变化，当隐藏时重置物理位置
            this.IsVisibleChanged += OnIsVisibleChanged;
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
            {
                // [Fix 3.2] 当控件不可见时，立即重置偏移量
                // 防止下次显示时出现从旧位置"飞"过来的情况
                _currentOffset = new Vector(0, 0);
                if (OrbTranslate != null)
                {
                    OrbTranslate.X = 0;
                    OrbTranslate.Y = 0;
                }
            }
        }

        // ============================
        // 依赖属性 (保持不变)
        // ============================
        public static readonly DependencyProperty IconKeyProperty =
            DependencyProperty.Register(nameof(IconKey), typeof(string), typeof(JellyOrb), new PropertyMetadata(string.Empty, OnIconKeyChanged));
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(JellyOrb), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register(nameof(Size), typeof(double), typeof(JellyOrb), new PropertyMetadata(50.0));
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(JellyOrb), new PropertyMetadata(false));

        public string IconKey { get => (string)GetValue(IconKeyProperty); set => SetValue(IconKeyProperty, value); }
        public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
        public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
        public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

        // ============================
        // 渲染循环 (Lerp 核心)
        // ============================
        private void OnRenderFrame(object? sender, EventArgs e)
        {
            if (OrbTranslate == null || this.Visibility != Visibility.Visible) return;

            Vector targetOffset = new Vector(0, 0);

            // 1. 计算目标位置 (Target)
            if (IsActive)
            {
                try
                {
                    Point orbCenterScreen = this.PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
                    var mouseScreen = Forms.Cursor.Position;

                    double diffX = (mouseScreen.X - orbCenterScreen.X);
                    double diffY = (mouseScreen.Y - orbCenterScreen.Y);

                    // 限制最大位移
                    double targetX = Math.Max(-MaxOffsetLimit, Math.Min(MaxOffsetLimit, diffX * ParallaxIntensity));
                    double targetY = Math.Max(-MaxOffsetLimit, Math.Min(MaxOffsetLimit, diffY * ParallaxIntensity));

                    targetOffset = new Vector(targetX, targetY);
                }
                catch
                {
                    // Ignore
                }
            }

            // 2. 线性插值 (Lerp) - 取代弹簧物理
            // 公式：当前值 = 当前值 + (目标值 - 当前值) * 系数
            // 这是一种无限趋近算法，永远不会过冲，也就永远不会"乱跳"

            _currentOffset.X += (targetOffset.X - _currentOffset.X) * SmoothFactor;
            _currentOffset.Y += (targetOffset.Y - _currentOffset.Y) * SmoothFactor;

            // 3. 极小值归零 (停止计算节省性能)
            if (Math.Abs(targetOffset.X - _currentOffset.X) < 0.05) _currentOffset.X = targetOffset.X;
            if (Math.Abs(targetOffset.Y - _currentOffset.Y) < 0.05) _currentOffset.Y = targetOffset.Y;

            // 4. 应用变换
            var dpi = VisualTreeHelper.GetDpi(this);
            OrbTranslate.X = _currentOffset.X / dpi.DpiScaleX;
            OrbTranslate.Y = _currentOffset.Y / dpi.DpiScaleY;
        }

        // ============================
        // 内部渲染属性 (保持不变)
        // ============================
        public static readonly DependencyProperty RenderImageProperty = DependencyProperty.Register("RenderImage", typeof(ImageSource), typeof(JellyOrb), new PropertyMetadata(null));
        public static readonly DependencyProperty RenderGlyphProperty = DependencyProperty.Register("RenderGlyph", typeof(string), typeof(JellyOrb), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty ShowImageProperty = DependencyProperty.Register("ShowImage", typeof(bool), typeof(JellyOrb), new PropertyMetadata(false));

        public ImageSource RenderImage { get => (ImageSource)GetValue(RenderImageProperty); private set => SetValue(RenderImageProperty, value); }
        public string RenderGlyph { get => (string)GetValue(RenderGlyphProperty); private set => SetValue(RenderGlyphProperty, value); }
        public bool ShowImage { get => (bool)GetValue(ShowImageProperty); private set => SetValue(ShowImageProperty, value); }

        private static void OnIconKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is JellyOrb orb) orb.RefreshIcon(e.NewValue as string);
        }

        private void RefreshIcon(string key)
        {
            RenderImage = null; RenderGlyph = string.Empty; ShowImage = false;
            if (string.IsNullOrWhiteSpace(key)) return;
            if (key.Contains("\\") || key.Contains("."))
            {
                var img = IconHelper.GetIconFromPath(key);
                if (img != null) { RenderImage = img; ShowImage = true; return; }
            }
            var glyph = IconHelper.GetGlyph(key);
            if (!string.IsNullOrEmpty(glyph)) { RenderGlyph = glyph; ShowImage = false; }
        }
    }
}