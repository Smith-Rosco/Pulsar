using Pulsar.Core.Rendering;
using Pulsar.Helpers;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media; // VisualTreeHelper, CompositionTarget
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Microsoft.Extensions.DependencyInjection;

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

        // [Time-based damping] The parallax converges exponentially with a fixed
        // time constant (frame-rate independent) and a hard velocity ceiling, so a
        // fast sweep across a large circle eases toward the far offset instead of
        // snapping to it. Speed adapts to remaining distance up to the ceiling.
        private const double ParallaxTimeConstant = 1.0 / 12.0; // ~83ms to close 63% of the gap
        private const double MaxParallaxSpeed = 90.0;           // px/s, velocity ceiling

        // �Ӳ�ǿ��: ����ƶ� 100px��Orb �ƶ� 12px
        private const double ParallaxIntensity = 0.12;

        // ���λ������ (����): �������Χ
        private const double MaxOffsetLimit = 12.0;

        private DateTime _lastFrameTimeUtc = DateTime.MinValue;
        private bool _renderLoopSubscribed;

        public SlotOrb()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
            this.Unloaded += OnUnloaded;

            // [Fix 3.1] �����ɼ��Ա仯��������ʱ��������λ��
            this.IsVisibleChanged += OnIsVisibleChanged;
        }

        // ============================
        // [RadialRenderer] Highlight seam
        // ============================
        // SlotOrb is instantiated inside XAML DataTemplates (not via DI), so it
        // resolves the registered renderer through the app service provider, matching
        // the existing service-locator pattern used elsewhere in the codebase.
        private static IRadialRenderer? GetRenderer()
        {
            if (Application.Current is App app)
            {
                return app.Services.GetService<IRadialRenderer>();
            }
            return null;
        }

        /// <summary>
        /// Writes the renderer-resolved highlight (glow brush / effect / opacity) onto
        /// the <see cref="ActiveShape"/> glow layer. This replaces the hard-coded
        /// highlight effect that used to live in the active-state XAML trigger.
        /// </summary>
        internal void ApplyHighlight(IRadialSlotHighlight highlight)
        {
            if (ActiveShape == null) return;

            // Opacity transition (matches the original 300ms enter / 320ms release feel).
            var duration = highlight.Opacity > 0
                ? TimeSpan.FromMilliseconds(300)
                : TimeSpan.FromMilliseconds(320);
            var easeOut = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            // Glow brush: a custom fill wins over the theme-derived glow brush,
            // matching the previous template precedence. A null glow (inactive
            // highlight) clears the previous fill so no stale blue disc lingers
            // while the ring fades out.
            if (highlight.GlowBrush != null)
            {
                ActiveShape.Fill = CustomFill ?? highlight.GlowBrush;
            }
            else
            {
                ActiveShape.Fill = null;
            }

            // Effect kind (Blur by default, never a per-slot DropShadow in the default).
            switch (highlight.EffectKind)
            {
                case RadialSlotEffectKind.Blur:
                    ActiveShape.Effect = new BlurEffect { Radius = highlight.BlurRadius };
                    break;
                case RadialSlotEffectKind.DropShadow:
                    ActiveShape.Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = highlight.BlurRadius,
                        ShadowDepth = 0,
                        Opacity = highlight.Opacity
                    };
                    break;
                default:
                    // Release: don't detach the blur instantly — that snaps a crisp
                    // ring into view. Animate its radius down to 0 in the same rhythm
                    // as the opacity fade, then detach the effect once it settles.
                    if (ActiveShape.Effect is BlurEffect releaseBlur)
                    {
                        var blurEffect = ActiveShape.Effect;
                        var radiusAnim = new DoubleAnimation(releaseBlur.Radius, 0, duration) { EasingFunction = easeOut };
                        radiusAnim.Completed += (_, _) =>
                        {
                            if (ReferenceEquals(ActiveShape.Effect, blurEffect))
                            {
                                ActiveShape.Effect = null;
                            }
                        };
                        releaseBlur.BeginAnimation(BlurEffect.RadiusProperty, radiusAnim);
                    }
                    else if (ActiveShape.Effect is DropShadowEffect releaseShadow)
                    {
                        var shadowEffect = ActiveShape.Effect;
                        var radiusAnim = new DoubleAnimation(releaseShadow.BlurRadius, 0, duration) { EasingFunction = easeOut };
                        radiusAnim.Completed += (_, _) =>
                        {
                            if (ReferenceEquals(ActiveShape.Effect, shadowEffect))
                            {
                                ActiveShape.Effect = null;
                            }
                        };
                        releaseShadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, radiusAnim);
                    }
                    break;
            }

            ActiveShape.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(highlight.Opacity, duration) { EasingFunction = easeOut });
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (IsActive)
            {
                EnsureRenderLoop();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopRenderLoop();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Reset the frame clock so a resumed render loop never computes a
            // giant delta (which would otherwise snap the offset to its target).
            _lastFrameTimeUtc = DateTime.MinValue;

            if (!(bool)e.NewValue)
            {
                StopRenderLoop();

                // [Fix 3.2] ���ؼ����ɼ�ʱ����������ƫ����
                // ��ֹ�´���ʾʱ���ִӾ�λ��"��"���������
                _currentOffset = new Vector(0, 0);
                if (OrbTranslate != null)
                {
                    OrbTranslate.X = 0;
                    OrbTranslate.Y = 0;
                }
            }
            else if (IsActive)
            {
                // Re-shown while still active: resume the drift loop.
                EnsureRenderLoop();
            }
        }

        // The render loop drives ALL parallax motion (drift while active, eased
        // release back to rest while inactive). It never animates OrbTranslate via
        // a WPF Timeline, so OrbTranslate's base value stays clean and re-activation
        // can never snap to a stale "far" position.
        private void EnsureRenderLoop()
        {
            if (_renderLoopSubscribed) return;
            CompositionTarget.Rendering += OnRenderFrame;
            _renderLoopSubscribed = true;
        }

        private void StopRenderLoop()
        {
            if (!_renderLoopSubscribed) return;
            CompositionTarget.Rendering -= OnRenderFrame;
            _renderLoopSubscribed = false;
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
            DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(SlotOrb), new PropertyMetadata(false, OnIsActiveChanged));

        private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var orb = (SlotOrb)d;
            bool isActive = (bool)e.NewValue;

            // Fluid motion language: a single QuadraticEase family and matched
            // durations so activation and release feel like one breathing gesture
            // rather than a sequence of disconnected jerks.
            var enter = TimeSpan.FromMilliseconds(300);
            var release = TimeSpan.FromMilliseconds(320);
            var easeOut = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            // [RadialRenderer] Route the highlight through the injected renderer.
            // ShowActiveGlow gates whether the glow is drawn at all (matches the
            // removed XAML MultiDataTrigger condition).
            var renderer = GetRenderer();
            if (renderer != null)
            {
                orb.ApplyHighlight(renderer.ResolveHighlight(isActive && orb.ShowActiveGlow));
            }

            if (isActive)
            {
                // Re-activation must not snap: _currentOffset already tracks where
                // the drift actually is (the render loop keeps it in sync during
                // release), so just reset the frame clock and resume the loop.
                orb._lastFrameTimeUtc = DateTime.MinValue;

                // No hard-coded From values: animate from wherever the value
                // currently is, so rapid hover flips glide instead of snapping.
                orb.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, enter) { EasingFunction = easeOut });
                if (orb.OrbScale != null)
                {
                    orb.OrbScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.15, enter) { EasingFunction = easeOut });
                    orb.OrbScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.15, enter) { EasingFunction = easeOut });
                }

                orb.EnsureRenderLoop();
            }
            else
            {
                // Graceful release: ease scale and opacity back to rest. The
                // parallax drift is NOT animated with a WPF Timeline here — the
                // render loop stays subscribed and eases _currentOffset back to 0
                // itself, then unsubscribes once settled. This keeps OrbTranslate's
                // base value clean so a later re-activation never snaps to a stale
                // "far" position.
                orb.BeginAnimation(OpacityProperty, new DoubleAnimation(0.8, release) { EasingFunction = easeOut });
                if (orb.OrbScale != null)
                {
                    orb.OrbScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, release) { EasingFunction = easeOut });
                    orb.OrbScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, release) { EasingFunction = easeOut });
                }
            }
        }
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
            if (OrbTranslate == null || this.Visibility != Visibility.Visible)
            {
                StopRenderLoop();
                return;
            }

            // Active: drift toward the cursor. Inactive (releasing): ease back to rest.
            Vector targetOffset;
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
                    targetOffset = new Vector(0, 0);
                }
            }
            else
            {
                targetOffset = new Vector(0, 0);
            }

            // 2. Time-based lerp: the step depends only on elapsed time (so the
            //    speed is consistent across refresh rates) and is capped by a
            //    velocity ceiling. Close to the target it settles gently; far
            //    from it (large circle sweeps) it approaches faster, but never
            //    more than MaxParallaxSpeed per second.
            var now = DateTime.UtcNow;
            double dt = _lastFrameTimeUtc == DateTime.MinValue ? 0.0 : (now - _lastFrameTimeUtc).TotalSeconds;
            _lastFrameTimeUtc = now;

            if (dt > 0)
            {
                double alpha = 1.0 - Math.Exp(-dt / ParallaxTimeConstant);
                double maxStep = MaxParallaxSpeed * dt;

                double stepX = Math.Clamp((targetOffset.X - _currentOffset.X) * alpha, -maxStep, maxStep);
                double stepY = Math.Clamp((targetOffset.Y - _currentOffset.Y) * alpha, -maxStep, maxStep);

                _currentOffset.X += stepX;
                _currentOffset.Y += stepY;

                // 3. Settle when close enough (stop sub-pixel jitter).
                if (Math.Abs(targetOffset.X - _currentOffset.X) < 0.05) _currentOffset.X = targetOffset.X;
                if (Math.Abs(targetOffset.Y - _currentOffset.Y) < 0.05) _currentOffset.Y = targetOffset.Y;
            }

            // 4. Apply transform
            var dpi = VisualTreeHelper.GetDpi(this);
            OrbTranslate.X = _currentOffset.X / dpi.DpiScaleX;
            OrbTranslate.Y = _currentOffset.Y / dpi.DpiScaleY;

            // 5. Once a release has fully settled, stop the loop to save cycles.
            if (!IsActive && Math.Abs(_currentOffset.X) < 0.05 && Math.Abs(_currentOffset.Y) < 0.05)
            {
                _currentOffset = new Vector(0, 0);
                OrbTranslate.X = 0;
                OrbTranslate.Y = 0;
                StopRenderLoop();
            }
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
