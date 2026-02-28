// [Path]: Pulsar/Pulsar/Views/RadialMenuWindow.xaml.cs

using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.Native;        // WindowHelper
using Pulsar.Models;        // AppTheme
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
// 引用 WinForms 获取全局鼠标坐标
using Forms = System.Windows.Forms;
using System.Windows.Input; // For Mouse.Capture

namespace Pulsar.Views
{
    public partial class RadialMenuWindow : Window
    {
        private readonly RadialMenuViewModel _viewModel;
        private readonly IConfigService _configService;
        private readonly IThemeService _themeService;
        private readonly ILogger<RadialMenuWindow> _logger;

        // [Fix] 添加 WindowService 字段以解决报错
        private readonly IWindowService _windowService;

        // DPI 缩放比例缓存
        private double _dpiScaleX = 1.0;
        private double _dpiScaleY = 1.0;

        public RadialMenuWindow(
            RadialMenuViewModel vm,
            IConfigService configService,
            IWindowService windowService,
            IThemeService themeService,
            ILogger<RadialMenuWindow> logger)
        {
            // Initialize Fields First
            _viewModel = vm;
            _configService = configService;
            _windowService = windowService;
            _themeService = themeService;
            _logger = logger;

            // [Theme Isolation] Apply Default Theme immediately before InitializeComponent
            _themeService.ApplyTheme(this, AppTheme.Dark, WindowBackdropType.None, updateGlobal: false);

            InitializeComponent();
            DataContext = vm;

            // 1. 监听 ViewModel 的属性变化 (Show/Hide 信号)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // 2. 窗口加载时初始化主题 (Loads user config)
            InitializeTheme();
            
            // Listen for theme changes from other windows
            _themeService.ThemeChanged += (s, theme) =>
            {
                _themeService.ApplyTheme(this, theme, WindowBackdropType.None, updateGlobal: false);
                _themeService.EnforceTransparency(this);
            };

            // 3. [Fix] 注册隐藏自身的能力
            _windowService.RegisterHideAction(() =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    Dismiss();
                });
            });

            // 4. [New] Handle Mouse Clicks for Drill-Down
            this.MouseLeftButtonUp += (s, e) => _viewModel.HandleLeftClick();
            
            // 5. [New] Handle Mouse Wheel for Paging
            this.PreviewMouseWheel += (s, e) => _viewModel.HandleMouseWheel(e.Delta);

            // ====================================================
            // 👻 [驻留模式初始化] (Resident Mode Init)
            // ====================================================
            this.Opacity = 0;
            this.Visibility = Visibility.Visible;
            this.IsHitTestVisible = false;
            this.ShowInTaskbar = false;
        }

        // [新增] 窗口句柄创建后的初始化钩子
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // 1. 获取窗口句柄
            var hwnd = new WindowInteropHelper(this).Handle;
            // 2. 获取当前扩展样式
            long currentStyle = WindowHelper.GetWindowLong(hwnd, WindowHelper.GWL_EXSTYLE);
            // 3. 注入 ToolWindow 样式 (使其在 Alt+Tab 中不可见)
            WindowHelper.SetWindowLong(hwnd, WindowHelper.GWL_EXSTYLE, currentStyle | WindowHelper.WS_EX_TOOLWINDOW);

            // 4. [New] Activate "Self-Healing" Transparency
            _themeService.EnforceTransparency(this);
        }

        private async void InitializeTheme()
        {
            try
            {
                var config = await _configService.LoadAsync();
                _themeService.ApplyTheme(this, config.Settings.LauncherThemeEnum, WindowBackdropType.None, updateGlobal: false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Theme init failed");
            }
        }



        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RadialMenuViewModel.IsVisible))
            {
                if (_viewModel.IsVisible)
                {
                    Summon();
                }
                else
                {
                    Dismiss();
                }
            }
        }

        // ==========================================
        // 🚀 核心交互逻辑 (Core Interaction)
        // ==========================================

        private void Summon()
        {
            // [Fix] Removed redundant SetPreviousWindow call. 
            // The ViewModel now handles this via PulsarContext.Capture() BEFORE the window is shown.

            // 1. [定位] 瞬间移动位置 (紧凑模式) - 此时 Opacity 为 0，移动无痕
            UpdateWindowPosition();
            
            // [Refactor] Resident Mode: Window is always Visible.
            // Ensure Visibility is Visible (just in case)
            if (this.Visibility != Visibility.Visible)
            {
                this.Visibility = Visibility.Visible;
            }

            // Bring to foreground and Activate
            this.Activate();
            
            // Critical: Capture mouse to track gestures outside the 500x500 bounds
            bool captured = MenuCanvas.CaptureMouse();
            _logger.LogDebug("[RadialMenuWindow] Summon - CaptureMouse: {Captured}", captured);

            // [New] Restore Interaction
            this.IsHitTestVisible = true; 
            
            // Clear any HoldEnd animations from Dismiss to prevent "flicker" from old values
            this.BeginAnimation(UIElement.OpacityProperty, null);
            this.Opacity = 0;

            // Prepare Animations
            var scaleAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(150));
            scaleAnim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            
            var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(100));

            var trans = new ScaleTransform(1, 1);
            MenuCanvas.RenderTransform = trans;
            MenuCanvas.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            
            trans.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            trans.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            
            // Start Fade In
            this.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            this.Focus();
            this.UpdateLayout();
            
            // [Fix] Resume Global Polling
            CompositionTarget.Rendering += UpdateLoop;
        }

        private void Dismiss()
        {
            // 1. [冻结] 停止计算循环
            CompositionTarget.Rendering -= UpdateLoop;
            
            // [Refactor] Release Capture
            MenuCanvas.ReleaseMouseCapture();
            
            // 2. [穿透] 立即关闭交互，防止在淡出过程中误触
            this.IsHitTestVisible = false;

            // 3. [隐身] 优雅退出 (Ghost Mode)
            _viewModel.CenterPreviewImage = null;

            // 显式停止之前的动画并播放淡出动画
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
            fadeOut.FillBehavior = FillBehavior.HoldEnd;
            
            fadeOut.Completed += (s, e) =>
            {
                _viewModel.ClearVisuals();
                // [Refactor] Never Hide() the window. 
                // Just leave it transparent and non-hit-testable.
                // this.Hide(); <--- REMOVED
            };
            
            this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            
            // [修改] 4. 显式归还焦点 (条件性)
            if (!_viewModel.ActionExecuted)
            {
                var prev = _windowService.GetPreviousWindow();
                if (prev != IntPtr.Zero)
                {
                    WindowHelper.SetForegroundWindow(prev);
                }
            }
        }

        private async void RefreshThemeOnShow()
        {
            var config = await _configService.LoadAsync();
            _themeService.ApplyTheme(this, config.Settings.LauncherThemeEnum, WindowBackdropType.None, updateGlobal: false);
            _themeService.EnforceTransparency(this);
        }

        // ==========================================
        // 🎮 游戏循环 & 坐标计算 (Game Loop & Math)
        // ==========================================

        private void UpdateLoop(object? sender, EventArgs e)
        {
            if (this.Opacity < 0.1) return;

            // [Fix] Use Global Cursor Position directly to avoid WPF clamping/capture issues outside window bounds.
            var screenPoint = Forms.Cursor.Position;

            // Convert Screen Pixels to WPF Logical Units
            // Note: _dpiScaleX/Y are updated in UpdateWindowPosition()
            double wpfGlobalX = screenPoint.X / _dpiScaleX;
            double wpfGlobalY = screenPoint.Y / _dpiScaleY;

            // Calculate relative to the MenuCanvas (Window)
            // RelX = GlobalMouse - WindowLeft
            // Since MenuCanvas is at 0,0 of the Window
            double relX = wpfGlobalX - this.Left;
            double relY = wpfGlobalY - this.Top;

            // 4. Send to ViewModel (0..500 relative coordinates)
            _viewModel.HandleMouseMove(relX, relY);
        }

        private void UpdateWindowPosition()
        {
            // Update DPI Scale
            var dpi = VisualTreeHelper.GetDpi(this);
            _dpiScaleX = dpi.DpiScaleX;
            _dpiScaleY = dpi.DpiScaleY;

            // Get Global Cursor Position (Physical Pixels)
            var screenPoint = Forms.Cursor.Position;
            
            // Convert to WPF Logical Units
            double wpfMouseX = screenPoint.X / _dpiScaleX;
            double wpfMouseY = screenPoint.Y / _dpiScaleY;

            // Center window on cursor
            // Window Width/Height is fixed at 500 in XAML
            double halfWidth = 250; 
            double halfHeight = 250;

            this.Left = wpfMouseX - halfWidth;
            this.Top = wpfMouseY - halfHeight;

            // Reset Canvas Margin (it's now 0,0 relative to window)
            MenuCanvas.Margin = new Thickness(0);
        }

        protected override void OnClosed(EventArgs e)
        {
            CompositionTarget.Rendering -= UpdateLoop; 
            
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }
            base.OnClosed(e);
        }
    }
}
