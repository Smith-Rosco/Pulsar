// [Path]: Pulsar/Pulsar/Views/Tutorial/TutorialOverlayWindow.xaml.cs

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Pulsar.Models.Tutorial;

namespace Pulsar.Views.Tutorial
{
    /// <summary>
    /// 教程遮罩窗口状态
    /// </summary>
    public enum OverlayState
    {
        /// <summary>
        /// 聚焦状态：全屏遮罩 + 聚光灯
        /// </summary>
        Focused,

        /// <summary>
        /// 观察状态：无遮罩，浮动卡片
        /// </summary>
        Observing,

        /// <summary>
        /// 隐藏状态
        /// </summary>
        Hidden
    }

    /// <summary>
    /// 教程遮罩窗口 - 实现聚光灯效果和状态机
    /// </summary>
    public partial class TutorialOverlayWindow : Window
    {
        private Rect _spotlightBounds = Rect.Empty;
        private readonly System.Windows.Shapes.Rectangle _overlayBackground;
        private readonly ContentPresenter _cardPresenter;
        private OverlayState _currentState = OverlayState.Focused;

        private HwndSource? _hwndSource;

        private const int WM_NCHITTEST = 0x0084;
        private const int HTTRANSPARENT = -1;

        // [Fix] 防抖机制：避免卡片位置飘忽
        private System.Threading.CancellationTokenSource? _positionDebounceToken;
        private CardPosition _pendingPosition = CardPosition.TopRight;
        
        // 固定大小模式配置
        private CardSizeMode _sizeMode = CardSizeMode.Auto;
        private double _fixedWidth = 450;
        private double _fixedHeight = 300;
        
        // [Performance] 聚光灯缓存：避免重复创建 VisualBrush
        private Rect _cachedSpotlightBounds = Rect.Empty;
        private VisualBrush? _cachedSpotlightBrush = null;
        
        // [Performance] 防抖机制：避免频繁更新聚光灯
        private System.Threading.CancellationTokenSource? _spotlightDebounceToken;
        
        // [Performance] 性能监控
        private System.Diagnostics.Stopwatch? _perfStopwatch;

        // Win32 API for click-through
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public TutorialOverlayWindow()
        {
            InitializeComponent();
            
            // Get references to XAML elements
            _overlayBackground = (System.Windows.Shapes.Rectangle)FindName("OverlayBackground");
            _cardPresenter = (ContentPresenter)FindName("CardPresenter");
            
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Enable click-through outside the card in Focused mode.
            _hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            _hwndSource?.AddHook(WndProc);
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _hwndSource?.RemoveHook(WndProc);
            }
            catch
            {
            }

            _hwndSource = null;
            base.OnClosed(e);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_NCHITTEST && _currentState == OverlayState.Focused)
            {
                try
                {
                    if (_cardPresenter == null || _cardPresenter.ActualWidth <= 0 || _cardPresenter.ActualHeight <= 0)
                    {
                        return IntPtr.Zero;
                    }

                    // lParam contains screen coordinates in pixels.
                    int x = unchecked((short)((long)lParam & 0xFFFF));
                    int y = unchecked((short)(((long)lParam >> 16) & 0xFFFF));
                    var mouse = new System.Windows.Point(x, y);

                    var topLeft = _cardPresenter.PointToScreen(new System.Windows.Point(0, 0));
                    var bottomRight = _cardPresenter.PointToScreen(new System.Windows.Point(_cardPresenter.ActualWidth, _cardPresenter.ActualHeight));
                    var cardRect = new Rect(topLeft, bottomRight);

                    if (!cardRect.Contains(mouse))
                    {
                        handled = true;
                        return new IntPtr(HTTRANSPARENT);
                    }
                }
                catch
                {
                    // If hit-testing fails, keep the window interactive to avoid breaking the tutorial.
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// 启用点击穿透（让鼠标事件穿透到下层窗口）
        /// </summary>
        private void EnableClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
        }

        /// <summary>
        /// 禁用点击穿透（恢复正常交互）
        /// </summary>
        private void DisableClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }

        /// <summary>
        /// 切换到 Focused 状态（全屏遮罩 + 聚光灯）
        /// </summary>
        public void EnterFocusedState()
        {
            // [Performance] 性能监控开始
            _perfStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            _currentState = OverlayState.Focused;
            
            // 禁用点击穿透，恢复正常交互
            DisableClickThrough();
            
            // 重置窗口位置和尺寸（清除 Observing 模式的残留）
            Left = 0;
            Top = 0;
            Width = double.NaN;
            Height = double.NaN;
            
            // 恢复全屏
            Topmost = true;
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.Manual;
            
            // 显示遮罩
            if (_overlayBackground != null)
            {
                _overlayBackground.Visibility = Visibility.Visible;
            }
            
            // 卡片居中
            if (_cardPresenter != null)
            {
                _cardPresenter.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
                _cardPresenter.VerticalAlignment = System.Windows.VerticalAlignment.Center;
                _cardPresenter.Margin = new Thickness(0);
            }
            
            // 移除阴影效果
            Effect = null;
            
            // 恢复聚光灯
            UpdateOverlay();
            
            // 确保窗口在最前面
            Activate();
            
            LogPerformance("EnterFocusedState");
        }

        /// <summary>
        /// 设置卡片大小模式
        /// </summary>
        public void SetCardSizeMode(CardSizeMode sizeMode, double fixedWidth = 450, double fixedHeight = 300)
        {
            _sizeMode = sizeMode;
            _fixedWidth = fixedWidth;
            _fixedHeight = fixedHeight;
        }

        /// <summary>
        /// 切换到 Observing 状态（浮动卡片）
        /// </summary>
        public void EnterObservingState(CardPosition position = CardPosition.TopRight)
        {
            // [Performance] 性能监控开始
            _perfStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            _currentState = OverlayState.Observing;
            _pendingPosition = position;  // [Fix] 记录目标位置
            
            // [Fix] 取消之前的定位任务（防抖）
            _positionDebounceToken?.Cancel();
            _positionDebounceToken = new System.Threading.CancellationTokenSource();
            var token = _positionDebounceToken.Token;
            
            // 关键修复：在 Observing 模式下，不使用 Topmost，让 SettingsWindow 可以正常接收事件
            Topmost = false;
            
            // 隐藏遮罩
            if (_overlayBackground != null)
            {
                _overlayBackground.Visibility = Visibility.Collapsed;
            }
            
            // 清除聚光灯
            _spotlightBounds = Rect.Empty;
            
            // 调整窗口为浮动卡片模式
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            // 注意：AllowsTransparency 已在 XAML 中设置，不能在运行时修改
            
            // 根据大小模式设置窗口尺寸
            if (_sizeMode == CardSizeMode.Fixed)
            {
                // 固定大小模式：使用预设的固定宽高
                SizeToContent = SizeToContent.Manual;
                Width = _fixedWidth;
                Height = _fixedHeight;
                MaxWidth = double.PositiveInfinity;
                MaxHeight = double.PositiveInfinity;
            }
            else
            {
                // 自动调整大小模式：根据内容自适应
                SizeToContent = SizeToContent.WidthAndHeight;
                Width = double.NaN;
                Height = double.NaN;
                MaxWidth = 450;
                MaxHeight = 350;
            }
            
            // [Performance] 优化阴影效果：降低模糊半径以提升性能
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 15,      // 从 20 降低到 15
                ShadowDepth = 4,      // 从 5 降低到 4
                Opacity = 0.4,        // 从 0.5 降低到 0.4
                RenderingBias = RenderingBias.Performance  // 优先性能而非质量
            };
            
            // 卡片对齐调整
            if (_cardPresenter != null)
            {
                _cardPresenter.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
                _cardPresenter.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
                _cardPresenter.Margin = new Thickness(10);
            }
            
            LogPerformance("EnterObservingState");
            
            // [Fix] 防抖延迟定位（50ms 内只执行最后一次）
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(50, token);  // 防抖延迟
                    if (!token.IsCancellationRequested)
                    {
                        PositionCard(_pendingPosition);
                    }
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                    // 被新的定位任务取消，忽略
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 定位卡片到指定位置
        /// </summary>
        private void PositionCard(CardPosition position)
        {
            // [Performance] 性能监控开始
            _perfStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var screen = SystemParameters.WorkArea;
            const double margin = 20;
            
            // 根据大小模式确定卡片尺寸
            double cardWidth;
            double cardHeight;
            
            if (_sizeMode == CardSizeMode.Fixed)
            {
                // 固定大小模式：直接使用预设的固定宽高
                cardWidth = _fixedWidth;
                cardHeight = _fixedHeight;
            }
            else
            {
                // [Performance] 自动调整大小模式：避免同步 UpdateLayout()
                // 使用 Dispatcher 异步等待布局完成
                if (ActualWidth <= 0 || ActualHeight <= 0)
                {
                    // 布局尚未完成，延迟定位
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        PositionCard(position);
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                    return;
                }
                
                cardWidth = ActualWidth;
                cardHeight = ActualHeight;
            }
            
            switch (position)
            {
                case CardPosition.TopRight:
                    Left = screen.Right - cardWidth - margin;
                    Top = screen.Top + margin;
                    break;
                    
                case CardPosition.TopLeft:
                    Left = screen.Left + margin;
                    Top = screen.Top + margin;
                    break;
                    
                case CardPosition.BottomRight:
                    Left = screen.Right - cardWidth - margin;
                    Top = screen.Bottom - cardHeight - margin;
                    break;
                    
                case CardPosition.BottomLeft:
                    Left = screen.Left + margin;
                    Top = screen.Bottom - cardHeight - margin;
                    break;
                    
                case CardPosition.Center:
                    Left = (screen.Width - cardWidth) / 2 + screen.Left;
                    Top = (screen.Height - cardHeight) / 2 + screen.Top;
                    break;
                    
                case CardPosition.Smart:
                    // TODO: 实现智能定位算法（避开目标窗口）
                    // 暂时使用右上角
                    Left = screen.Right - cardWidth - margin;
                    Top = screen.Top + margin;
                    break;
            }
            
            LogPerformance($"PositionCard ({position})");
        }

        /// <summary>
        /// 设置聚光灯区域
        /// </summary>
        /// <param name="bounds">聚光灯区域的屏幕坐标</param>
        public void SetSpotlight(Rect bounds)
        {
            // [Performance] 如果位置没变化，跳过更新
            if (_spotlightBounds == bounds)
            {
                return;
            }
            
            _spotlightBounds = bounds;
            
            // [Performance] 防抖：避免频繁更新（30ms 内只执行最后一次）
            _spotlightDebounceToken?.Cancel();
            _spotlightDebounceToken = new System.Threading.CancellationTokenSource();
            var token = _spotlightDebounceToken.Token;
            
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(30, token);
                    if (!token.IsCancellationRequested)
                    {
                        UpdateOverlay();
                    }
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                    // 被新的更新任务取消，忽略
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// 清除聚光灯效果
        /// </summary>
        public void ClearSpotlight()
        {
            _spotlightBounds = Rect.Empty;
            _cachedSpotlightBounds = Rect.Empty;
            _cachedSpotlightBrush = null; // [Performance] 清除缓存
            UpdateOverlay();
        }

        /// <summary>
        /// 更新遮罩显示
        /// </summary>
        private void UpdateOverlay()
        {
            // [Performance] 性能监控开始
            _perfStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            if (_overlayBackground == null) return;

            // 如果在 Observing 状态，不显示遮罩
            if (_currentState == OverlayState.Observing)
            {
                return;
            }

            if (_spotlightBounds.IsEmpty)
            {
                // No spotlight, show full overlay
                _overlayBackground.OpacityMask = null;
                _cachedSpotlightBounds = Rect.Empty;
                _cachedSpotlightBrush = null;
                
                LogPerformance("UpdateOverlay (No Spotlight)");
                return;
            }

            // [Performance] 使用缓存：如果聚光灯位置没变，复用之前的 VisualBrush
            if (_cachedSpotlightBounds == _spotlightBounds && _cachedSpotlightBrush != null)
            {
                _overlayBackground.OpacityMask = _cachedSpotlightBrush;
                LogPerformance("UpdateOverlay (Cached)");
                return;
            }

            // Create spotlight effect using opacity mask
            var visualBrush = CreateSpotlightBrush();
            
            // [Performance] 缓存 VisualBrush
            _cachedSpotlightBounds = _spotlightBounds;
            _cachedSpotlightBrush = visualBrush;
            
            _overlayBackground.OpacityMask = visualBrush;
            
            LogPerformance("UpdateOverlay (New Brush)");
        }
        
        /// <summary>
        /// [Performance] 创建聚光灯画刷（提取为独立方法，便于优化）
        /// </summary>
        private VisualBrush CreateSpotlightBrush()
        {
            var visualBrush = new VisualBrush();
            
            // [Performance] 使用 DrawingVisual 替代 Canvas（更轻量）
            var drawingVisual = new DrawingVisual();
            
            // [Performance] 启用缓存以提升渲染性能
            drawingVisual.CacheMode = new BitmapCache
            {
                RenderAtScale = 1.0,
                SnapsToDevicePixels = true
            };
            
            using (var context = drawingVisual.RenderOpen())
            {
                // 绘制黑色背景
                context.DrawRectangle(
                    System.Windows.Media.Brushes.Black,
                    null,
                    new Rect(0, 0, ActualWidth, ActualHeight)
                );
                
                // 绘制透明的聚光灯区域（圆角矩形）
                var geometry = new RectangleGeometry(
                    _spotlightBounds,
                    8, // RadiusX
                    8  // RadiusY
                );
                
                context.DrawGeometry(
                    System.Windows.Media.Brushes.Transparent,
                    null,
                    geometry
                );
            }
            
            visualBrush.Visual = drawingVisual;
            
            return visualBrush;
        }
        
        /// <summary>
        /// [Performance] 记录性能日志
        /// </summary>
        private void LogPerformance(string operation)
        {
            if (_perfStopwatch != null)
            {
                _perfStopwatch.Stop();
                var elapsed = _perfStopwatch.ElapsedMilliseconds;
                
                // 只记录超过 5ms 的操作
                if (elapsed > 5)
                {
                    // Keep debug spam low in production builds.
                    System.Diagnostics.Debug.WriteLine($"[TutorialOverlay Performance] {operation}: {elapsed}ms");
                }
                
                _perfStopwatch = null;
            }
        }

        /// <summary>
        /// 设置教程卡片内容
        /// </summary>
        public void SetCardContent(UIElement content)
        {
            if (_cardPresenter != null)
            {
                _cardPresenter.Content = content;
            }
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public OverlayState CurrentState => _currentState;
    }
}
