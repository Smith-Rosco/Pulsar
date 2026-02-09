// [Path]: Pulsar/Pulsar/Views/RadialMenuWindow.xaml.cs

using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.Native;        // WindowHelper
using Pulsar.Models;        // AppTheme
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
// 引用 WinForms 获取全局鼠标坐标
using Forms = System.Windows.Forms;

namespace Pulsar.Views
{
    public partial class RadialMenuWindow : Window
    {
        private readonly RadialMenuViewModel _viewModel;
        private readonly IConfigService _configService;
        private readonly IThemeService _themeService;

        // [Fix] 添加 WindowService 字段以解决报错
        private readonly IWindowService _windowService;

        // DPI 缩放比例缓存
        private double _dpiScaleX = 1.0;
        private double _dpiScaleY = 1.0;

        // [注意] _previousForegroundWindow 本地字段已移除，状态提升至 WindowService 管理

        public RadialMenuWindow(RadialMenuViewModel vm, IConfigService configService, IWindowService windowService, IThemeService themeService)
        {
            // Initialize Fields First
            _viewModel = vm;
            _configService = configService;
            _windowService = windowService;
            _themeService = themeService;

            // [Theme Isolation] Apply Default Theme immediately before InitializeComponent
            // This ensures resources are available for XAML parsing and initial layout.
            // We use Dark as safe default until config loads.
            _themeService.ApplyTheme(this, AppTheme.Dark, WindowBackdropType.None, updateGlobal: false);

            InitializeComponent();
            DataContext = vm;

            // 1. 监听 ViewModel 的属性变化 (Show/Hide 信号)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            
            // [New] Listen for CenterPreviewImage changes for smooth transition
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(RadialMenuViewModel.CenterPreviewImage))
                {
                    UpdatePreviewTransition(_viewModel.CenterPreviewImage);
                }
            };

            // 2. 窗口加载时初始化主题 (Loads user config)
            InitializeTheme();
            
            // Listen for theme changes from other windows (e.g., Settings)
            _themeService.ThemeChanged += (s, theme) =>
            {
                // Re-apply local resources but DO NOT trigger global update again (infinite loop risk)
                _themeService.ApplyTheme(this, theme, WindowBackdropType.None, updateGlobal: false);
                _themeService.EnforceTransparency(this);
            };

            // 3. [Fix] 注册隐藏自身的能力 (使用 Dispatcher 调度回 UI 线程)
            _windowService.RegisterHideAction(() =>
            {
                // 检查是否在 UI 线程，如果不是，则调度过去
                this.Dispatcher.Invoke(() =>
                {
                    Dismiss();
                });
            });

            // 4. [New] Handle Mouse Clicks for Drill-Down
            this.MouseLeftButtonUp += (s, e) => _viewModel.HandleLeftClick();
            
            // 5. [New] Handle Mouse Wheel for Paging
            this.PreviewMouseWheel += (s, e) => _viewModel.HandleMouseWheelMixed(e.Delta);

            // 6. [Optimized] Global Polling for "Infinite" Radial Trigger
            // this.MouseMove += OnWindowMouseMove; // Removed local event handler


            // ====================================================
            // 👻 [驻留模式初始化] (Resident Mode Init)
            // ====================================================
            this.Opacity = 0;
            this.Visibility = Visibility.Visible; // [Fix] Start Visible but Transparent
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
                // [Fix] Do not apply global theme, only local resources
                _themeService.ApplyTheme(this, config.Settings.LauncherThemeEnum, WindowBackdropType.None, updateGlobal: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Theme Init Failed: {ex.Message}");
            }
        }


        private bool _usePreviewA = true;

        private void UpdatePreviewTransition(ImageSource? newImage)
        {
             // Identify active and inactive layers
             var active = _usePreviewA ? PreviewEllipseA : PreviewEllipseB;
             var inactive = _usePreviewA ? PreviewEllipseB : PreviewEllipseA;
             
             // If new image is null, fade out current active layer
             if (newImage == null)
             {
                 var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200));
                 active.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                 return;
             }
             
             // Set new image to inactive layer
             if (inactive.Fill is ImageBrush brush)
             {
                 brush.ImageSource = newImage;
             }
             else
             {
                 // Should not happen if XAML is correct, but safe guard
                 inactive.Fill = new ImageBrush(newImage) { Stretch = Stretch.UniformToFill };
             }
             
             // Cross-fade
             var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(250));
             var fadeOutActive = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
             
             inactive.BeginAnimation(UIElement.OpacityProperty, fadeIn);
             active.BeginAnimation(UIElement.OpacityProperty, fadeOutActive);
             
             // Swap active flag
             _usePreviewA = !_usePreviewA;
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
            // [修改] 1. 将当前窗口句柄存储到 Service 中
            var current = WindowHelper.GetForegroundWindow();
            var selfHandle = new WindowInteropHelper(this).Handle;

            if (current != IntPtr.Zero && current != selfHandle)
            {
                // 这一步至关重要，PKI 全靠它找回家的路
                _windowService.SetPreviousWindow(current);
            }

            // 1. [定位] 瞬间移动位置 & 刷新主题
            UpdateDpiAndPosition();
            
            // [New] Animation (Pop-in)
            // Ensure initial state is ready for animation
            this.IsHitTestVisible = true; // [Fix] Enable interaction immediately before animation
            
            // Clear any HoldEnd animations from Dismiss
            this.BeginAnimation(UIElement.OpacityProperty, null);
            this.Opacity = 0;

            var scaleAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(150));
            scaleAnim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            
            var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(100));

            var trans = new ScaleTransform(1, 1);
            MenuCanvas.RenderTransform = trans;
            MenuCanvas.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            
            trans.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            trans.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            this.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            // ... 后续逻辑保持不变 (Activate, Focus, Opacity=1) ...
            // this.Show(); // [Fix] 移除 Show()，因为窗口一直 Visible，避免 Trigger 重绘闪烁
            this.Activate(); // 抢占焦点
            this.Focus();
            this.UpdateLayout();
            
            // CompositionTarget.Rendering += UpdateLoop; // [Optimized] Removed polling
            
            // [Fix] Resume Global Polling (Requirement: Full Screen Trigger)
            CompositionTarget.Rendering += UpdateLoop;
        }

        private void Dismiss()
        {
            // 1. [冻结] 停止计算循环
            // CompositionTarget.Rendering -= UpdateLoop; // [Optimized] Removed polling
            
            // [Fix] Pause Global Polling
            CompositionTarget.Rendering -= UpdateLoop;
            
            // 2. [隐身] 优雅退出 (Resident Mode)
            // [Fix] Immediately hide Preview to prevent residue
            _viewModel.CenterPreviewImage = null;

            // 显式停止之前的动画并播放淡出动画，确保 Opacity 归零
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
            fadeOut.FillBehavior = FillBehavior.HoldEnd;
            
            // [Fix 1] Wait for animation to complete before clearing visuals
            fadeOut.Completed += (s, e) =>
            {
                _viewModel.ClearVisuals();
            };
            
            this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            
            // 3. [穿透] 关闭交互
            this.IsHitTestVisible = false;

            // [修改] 4. 显式归还焦点 (条件性)
            // 只有在 "未执行任何动作" (即取消操作) 时，才由 UI 层负责归还焦点。
            // 如果 ActionExecuted 为 TRUE，说明 Handler (如 PKI) 接管了控制权，UI 不要插手。
            if (!_viewModel.ActionExecuted)
            {
                var prev = _windowService.GetPreviousWindow();
                if (prev != IntPtr.Zero)
                {
                    WindowHelper.SetForegroundWindow(prev);
                }
            }

            // 无论是否归还，都清空记录，防止逻辑污染
            // 注意：这里不清除 Service 里的记录，因为 PKI 可能稍后还需要读取它
            // _windowService.SetPreviousWindow(IntPtr.Zero); 
            
            // 5. [清理] ViewModel 清理逻辑已移至动画完成回调中
            // _viewModel.ClearVisuals();
        }

        private async void RefreshThemeOnShow()
        {
            System.Diagnostics.Debug.WriteLine($"[RadialMenuWindow] RefreshThemeOnShow: Before ApplyTheme, Background={this.Background}, Style={this.Style}");
            var config = await _configService.LoadAsync();
            // [Fix] Do not trigger global update when showing, just refresh local
            _themeService.ApplyTheme(this, config.Settings.LauncherThemeEnum, WindowBackdropType.None, updateGlobal: false);
            
            // [Fix] Enforce transparency and remove any potential style overrides
            _themeService.EnforceTransparency(this);
            
            System.Diagnostics.Debug.WriteLine($"[RadialMenuWindow] RefreshThemeOnShow: After ApplyTheme, Background={this.Background}, Style={this.Style}");
        }

        // ==========================================
        // 🎮 游戏循环 & 坐标计算 (Game Loop & Math)
        // ==========================================

        private void UpdateLoop(object? sender, EventArgs e)
        {
            if (this.Opacity < 0.1) return;

            // 1. Get Global Cursor Position (Physical Pixels)
            var screenPoint = Forms.Cursor.Position;

            // 2. Convert to WPF Coordinates (DPI Aware)
            double wpfMouseX = screenPoint.X / _dpiScaleX;
            double wpfMouseY = screenPoint.Y / _dpiScaleY;

            // 3. Convert to Window-Relative Coordinates
            // Even if the mouse is outside the window, this calculation is valid.
            // Center of window is at (250, 250) locally.
            // We need: MouseX - WindowLeft, MouseY - WindowTop
            
            double relX = wpfMouseX - this.Left;
            double relY = wpfMouseY - this.Top;

            // 4. Send to ViewModel for Polar Calculation
            _viewModel.HandleMouseMove(relX, relY);
        }

        /* [Removed] Local Event Handler
        private void OnWindowMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (this.Opacity < 0.1) return;

            // Get position relative to this window (DPI aware)
            var p = e.GetPosition(this);
            
            // 3. 发送给 ViewModel
            _viewModel.HandleMouseMove(p.X, p.Y);
        }
        */

        private void UpdateDpiAndPosition()
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            _dpiScaleX = dpi.DpiScaleX;
            _dpiScaleY = dpi.DpiScaleY;

            var screenPoint = Forms.Cursor.Position;
            double wpfMouseX = screenPoint.X / _dpiScaleX;
            double wpfMouseY = screenPoint.Y / _dpiScaleY;

            double width = this.ActualWidth > 0 ? this.ActualWidth : 500;
            double height = this.ActualHeight > 0 ? this.ActualHeight : 500;

            // [Enhanced] Smart Layout - Removed as per user request (Requirement 1: Cancel Smart Layout)
            // The menu should appear exactly where summoned (centered on cursor usually), without shifting.
            // If we still want to center on cursor:
            double left = wpfMouseX - (width / 2);
            double top = wpfMouseY - (height / 2);

            this.Left = left;
            this.Top = top;
        }

        protected override void OnClosed(EventArgs e)
        {
            CompositionTarget.Rendering -= UpdateLoop; // [Fix] Ensure loop stops
            // this.MouseMove -= OnWindowMouseMove; // Removed
            
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }
            base.OnClosed(e);
        }
    }
}