using Pulsar.Helpers;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media; // VisualTreeHelper, CompositionTarget

// [����] ǿ��ָ�� Point Ϊ WPF ����
using Point = System.Windows.Point;

namespace Pulsar.Views.Controls
{
    public partial class SlotOrb : UserControl
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

        // ���λ������ (����): �������Χ
        private const double MaxOffsetLimit = 12.0;

        public SlotOrb()
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
            DependencyProperty.Register(nameof(IconKey), typeof(string), typeof(SlotOrb), new PropertyMetadata(string.Empty, OnIconKeyChanged));
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(SlotOrb), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register(nameof(Size), typeof(double), typeof(SlotOrb), new PropertyMetadata(50.0));
        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(SlotOrb), new PropertyMetadata(false));
        public static readonly DependencyProperty IsRecommendedProperty =
            DependencyProperty.Register(nameof(IsRecommended), typeof(bool), typeof(SlotOrb), new PropertyMetadata(false));
        public static readonly DependencyProperty IsTransparentProperty =
            DependencyProperty.Register(nameof(IsTransparent), typeof(bool), typeof(SlotOrb), new PropertyMetadata(false));
        public static readonly DependencyProperty ShowActiveGlowProperty =
            DependencyProperty.Register(nameof(ShowActiveGlow), typeof(bool), typeof(SlotOrb), new PropertyMetadata(true));
        
        // [New] Custom Fill/Stroke Color (Overrides Theme)
        public static readonly DependencyProperty CustomFillProperty =
            DependencyProperty.Register(nameof(CustomFill), typeof(System.Windows.Media.Brush), typeof(SlotOrb), new PropertyMetadata(null));

        public static readonly DependencyProperty CustomStrokeProperty =
            DependencyProperty.Register(nameof(CustomStroke), typeof(System.Windows.Media.Brush), typeof(SlotOrb), new PropertyMetadata(null));

        // [New] Controls visibility of the inner content (Image/Text) without affecting the Orb shape/glow
        public static readonly DependencyProperty IsContentVisibleProperty =
            DependencyProperty.Register(nameof(IsContentVisible), typeof(bool), typeof(SlotOrb), new PropertyMetadata(true));

        // [New] Custom Foreground (For Adaptive Contrast)
        public static readonly DependencyProperty CustomForegroundProperty =
            DependencyProperty.Register(nameof(CustomForeground), typeof(System.Windows.Media.Brush), typeof(SlotOrb), new PropertyMetadata(null));

        // [New] Badge Count
        public static readonly DependencyProperty BadgeCountProperty =
            DependencyProperty.Register(nameof(BadgeCount), typeof(int), typeof(SlotOrb), new PropertyMetadata(0));
            
        // [New] Allow external binding of ImageSource (e.g. from ProcessWindowInfo)
        public static readonly DependencyProperty OrbImageProperty =
            DependencyProperty.Register(nameof(OrbImage), typeof(ImageSource), typeof(SlotOrb), new PropertyMetadata(null, OnOrbImageChanged));

        public string IconKey { get => (string)GetValue(IconKeyProperty); set => SetValue(IconKeyProperty, value); }
        public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
        public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
        public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
        public bool IsRecommended { get => (bool)GetValue(IsRecommendedProperty); set => SetValue(IsRecommendedProperty, value); }
        public bool IsTransparent { get => (bool)GetValue(IsTransparentProperty); set => SetValue(IsTransparentProperty, value); }
        public bool ShowActiveGlow { get => (bool)GetValue(ShowActiveGlowProperty); set => SetValue(ShowActiveGlowProperty, value); }
        public System.Windows.Media.Brush CustomFill { get => (System.Windows.Media.Brush)GetValue(CustomFillProperty); set => SetValue(CustomFillProperty, value); }
        public System.Windows.Media.Brush CustomStroke { get => (System.Windows.Media.Brush)GetValue(CustomStrokeProperty); set => SetValue(CustomStrokeProperty, value); }
        public System.Windows.Media.Brush CustomForeground { get => (System.Windows.Media.Brush)GetValue(CustomForegroundProperty); set => SetValue(CustomForegroundProperty, value); }
        public bool IsContentVisible { get => (bool)GetValue(IsContentVisibleProperty); set => SetValue(IsContentVisibleProperty, value); }
        public int BadgeCount { get => (int)GetValue(BadgeCountProperty); set => SetValue(BadgeCountProperty, value); }
        public ImageSource OrbImage { get => (ImageSource)GetValue(OrbImageProperty); set => SetValue(OrbImageProperty, value); }

        // ============================
        // Ⱦѭ (Lerp )
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
                    Pulsar.Native.PulsarNative.GetCursorPos(out var cursorPt);
                    var mouseScreen = new System.Drawing.Point(cursorPt.X, cursorPt.Y);

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


        private static void OnOrbImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SlotOrb orb) orb.RefreshIcon(orb.IconKey); 
        }

        private static void OnIconKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SlotOrb orb) orb.RefreshIcon(e.NewValue as string);
        }
        
        // ============================
        // Internal Rendering Properties (Read-Only)
        // ============================
        private static readonly DependencyPropertyKey RenderImagePropertyKey = 
            DependencyProperty.RegisterReadOnly(nameof(RenderImage), typeof(ImageSource), typeof(SlotOrb), new PropertyMetadata(null));
        
        public static readonly DependencyProperty RenderImageProperty = RenderImagePropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey RenderGlyphPropertyKey = 
            DependencyProperty.RegisterReadOnly(nameof(RenderGlyph), typeof(string), typeof(SlotOrb), new PropertyMetadata(string.Empty));
        
        public static readonly DependencyProperty RenderGlyphProperty = RenderGlyphPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey ShowImagePropertyKey = 
            DependencyProperty.RegisterReadOnly(nameof(ShowImage), typeof(bool), typeof(SlotOrb), new PropertyMetadata(false));
        
        public static readonly DependencyProperty ShowImageProperty = ShowImagePropertyKey.DependencyProperty;

        // [Fix] Dynamic Font Family Support
        private static readonly DependencyPropertyKey GlyphFontFamilyPropertyKey = 
            DependencyProperty.RegisterReadOnly(nameof(GlyphFontFamily), typeof(System.Windows.Media.FontFamily), typeof(SlotOrb), 
                new PropertyMetadata(new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets, Segoe UI Emoji")));

        public static readonly DependencyProperty GlyphFontFamilyProperty = GlyphFontFamilyPropertyKey.DependencyProperty;

        public ImageSource RenderImage
        {
            get => (ImageSource)GetValue(RenderImageProperty);
            private set => SetValue(RenderImagePropertyKey, value);
        }

        public string RenderGlyph
        {
            get => (string)GetValue(RenderGlyphProperty);
            private set => SetValue(RenderGlyphPropertyKey, value);
        }

        public System.Windows.Media.FontFamily GlyphFontFamily
        {
            get => (System.Windows.Media.FontFamily)GetValue(GlyphFontFamilyProperty);
            private set => SetValue(GlyphFontFamilyPropertyKey, value);
        }

        public bool ShowImage
        {
            get => (bool)GetValue(ShowImageProperty);
            private set => SetValue(ShowImagePropertyKey, value);
        }

        // ============================
        // Circular HitTest Override
        // ============================
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        {
            double radius = ActualWidth / 2.0;
            Point center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            Point pt = hitTestParameters.HitPoint;
            double dx = pt.X - center.X;
            double dy = pt.Y - center.Y;
            if (dx * dx + dy * dy <= radius * radius)
                return new PointHitTestResult(this, pt);
            return null!;
        }

        protected override GeometryHitTestResult HitTestCore(GeometryHitTestParameters hitTestParameters)
        {
            double radius = ActualWidth / 2.0;
            Point center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);
            EllipseGeometry circle = new EllipseGeometry(center, radius, radius);
            IntersectionDetail detail = circle.FillContainsWithDetail(hitTestParameters.HitGeometry);
            if (detail != IntersectionDetail.Empty)
                return new GeometryHitTestResult(this, detail);
            return null!;
        }

        private void RefreshIcon(string? key)
        {
            // Reset state
            // Don't clear RenderImage immediately to avoid flicker if we just swap sources
            
            bool showingImage = false;
            ImageSource? newImage = null;
            string newGlyph = string.Empty;

            // 1. Priority: OrbImage (Direct Image Binding) - e.g. Window Icon
            // [Design Decision] If OrbImage is provided, it usually overrides the IconKey (which might be a generic fallback)
            if (OrbImage != null)
            {
                newImage = OrbImage;
                showingImage = true;
            }
            // 2. Fallback: IconKey (Glyph or Path)
            else if (!string.IsNullOrWhiteSpace(key))
            {
                if (key.Contains("\\") || key.Contains("."))
                {
                    // Path to image file
                    try 
                    {
                        var img = IconHelper.GetIconFromPath(key);
                        if (img != null) { newImage = img; showingImage = true; }
                    }
                    catch {}
                }
                else
                {
                    // Glyph key
                    var glyph = IconHelper.GetGlyph(key);
                    if (!string.IsNullOrEmpty(glyph)) { newGlyph = glyph; showingImage = false; }
                }
            }
            
            // Apply
            if (showingImage)
            {
                SetValue(RenderImagePropertyKey, newImage);
                SetValue(ShowImagePropertyKey, true);
                SetValue(RenderGlyphPropertyKey, string.Empty);
            }
            else
            {
                SetValue(RenderImagePropertyKey, null);
                SetValue(ShowImagePropertyKey, false);
                SetValue(RenderGlyphPropertyKey, newGlyph);

                // [Fix] Determine correct font family
                SetValue(GlyphFontFamilyPropertyKey, IconHelper.GetGlyphFontFamily(newGlyph));
            }
        }
    }
}
