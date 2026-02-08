using Pulsar.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media; // VisualTreeHelper, CompositionTarget
using Forms = System.Windows.Forms;

// [����] ǿ��ָ�� Point Ϊ WPF ����
using Point = System.Windows.Point;

namespace Pulsar.Views.Controls
{
    public partial class JellyOrb : UserControl
    {
        // ============================
        // �˶��㷨���� (�Ѹ�Ϊ Lerp ��ֵģʽ)
        // ============================

        // ��ǰ��λ���� (X, Y)
        private Vector _currentOffset = new Vector(0, 0);

        // [�ؼ�����] ƽ������ (0.05 - 0.2)
        // ֵԽС������Խ����Խճ����ֵԽ����ӦԽ�졣
        // 0.1 ��һ���ǳ����ŵ���ֵ�������ڷ������ƶ���
        private const double SmoothFactor = 0.1;

        // �Ӳ�ǿ��: ����ƶ� 100px��Orb �ƶ� 12px
        private const double ParallaxIntensity = 0.12;

        // ���λ������ (����): �������Χ
        private const double MaxOffsetLimit = 12.0;

        public JellyOrb()
        {
            InitializeComponent();
            this.Loaded += (s, e) => CompositionTarget.Rendering += OnRenderFrame;
            this.Unloaded += (s, e) => CompositionTarget.Rendering -= OnRenderFrame;

            // [Fix 3.1] �����ɼ��Ա仯��������ʱ��������λ��
            this.IsVisibleChanged += OnIsVisibleChanged;
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
            {
                // [Fix 3.2] ���ؼ����ɼ�ʱ����������ƫ����
                // ��ֹ�´���ʾʱ���ִӾ�λ��"��"���������
                _currentOffset = new Vector(0, 0);
                if (OrbTranslate != null)
                {
                    OrbTranslate.X = 0;
                    OrbTranslate.Y = 0;
                }
            }
        }

        // ============================
        // �������� (���ֲ���)
        // ============================
        public static readonly DependencyProperty IconKeyProperty =
            DependencyProperty.Register(nameof(IconKey), typeof(string), typeof(JellyOrb), new PropertyMetadata(string.Empty, OnIconKeyChanged));
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(JellyOrb), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register(nameof(Size), typeof(double), typeof(JellyOrb), new PropertyMetadata(50.0));
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(JellyOrb), new PropertyMetadata(false));
        public static readonly DependencyProperty IsRecommendedProperty =
            DependencyProperty.Register(nameof(IsRecommended), typeof(bool), typeof(JellyOrb), new PropertyMetadata(false));
        public static readonly DependencyProperty IsTransparentProperty =
            DependencyProperty.Register(nameof(IsTransparent), typeof(bool), typeof(JellyOrb), new PropertyMetadata(false));
        public static readonly DependencyProperty ShowActiveGlowProperty =
            DependencyProperty.Register(nameof(ShowActiveGlow), typeof(bool), typeof(JellyOrb), new PropertyMetadata(true));

        public string IconKey { get => (string)GetValue(IconKeyProperty); set => SetValue(IconKeyProperty, value); }
        public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
        public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
        public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
        public bool IsRecommended { get => (bool)GetValue(IsRecommendedProperty); set => SetValue(IsRecommendedProperty, value); }
        public bool IsTransparent { get => (bool)GetValue(IsTransparentProperty); set => SetValue(IsTransparentProperty, value); }
        public bool ShowActiveGlow { get => (bool)GetValue(ShowActiveGlowProperty); set => SetValue(ShowActiveGlowProperty, value); }

        // ============================
        // ��Ⱦѭ�� (Lerp ����)
        // ============================
        private void OnRenderFrame(object? sender, EventArgs e)
        {
            if (OrbTranslate == null || this.Visibility != Visibility.Visible) return;

            Vector targetOffset = new Vector(0, 0);

            // 1. ����Ŀ��λ�� (Target)
            if (IsActive)
            {
                try
                {
                    Point orbCenterScreen = this.PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
                    var mouseScreen = Forms.Cursor.Position;

                    double diffX = (mouseScreen.X - orbCenterScreen.X);
                    double diffY = (mouseScreen.Y - orbCenterScreen.Y);

                    // �������λ��
                    double targetX = Math.Max(-MaxOffsetLimit, Math.Min(MaxOffsetLimit, diffX * ParallaxIntensity));
                    double targetY = Math.Max(-MaxOffsetLimit, Math.Min(MaxOffsetLimit, diffY * ParallaxIntensity));

                    targetOffset = new Vector(targetX, targetY);
                }
                catch
                {
                    // Ignore
                }
            }

            // 2. ���Բ�ֵ (Lerp) - ȡ����������
            // ��ʽ����ǰֵ = ��ǰֵ + (Ŀ��ֵ - ��ǰֵ) * ϵ��
            // ����һ�����������㷨����Զ������壬Ҳ����Զ����"����"

            _currentOffset.X += (targetOffset.X - _currentOffset.X) * SmoothFactor;
            _currentOffset.Y += (targetOffset.Y - _currentOffset.Y) * SmoothFactor;

            // 3. ��Сֵ���� (ֹͣ�����ʡ����)
            if (Math.Abs(targetOffset.X - _currentOffset.X) < 0.05) _currentOffset.X = targetOffset.X;
            if (Math.Abs(targetOffset.Y - _currentOffset.Y) < 0.05) _currentOffset.Y = targetOffset.Y;

            // 4. Ӧ�ñ任
            var dpi = VisualTreeHelper.GetDpi(this);
            OrbTranslate.X = _currentOffset.X / dpi.DpiScaleX;
            OrbTranslate.Y = _currentOffset.Y / dpi.DpiScaleY;
        }

        // ============================
        // �ڲ���Ⱦ���� (���ֲ���)
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