// [Path]: Pulsar/Pulsar/Views/RadialMenuWindow.xaml.cs

using Pulsar.Helpers;       // ThemeManager
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.Native;        // WindowHelper
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
// 引用 WinForms 获取全局鼠标坐标
using Forms = System.Windows.Forms;

namespace Pulsar.Views
{
    public partial class RadialMenuWindow : Window
    {
        private readonly RadialMenuViewModel _viewModel;
        private readonly IConfigService _configService;

        // [Fix] 添加 WindowService 字段以解决报错
        private readonly IWindowService _windowService;

        // DPI 缩放比例缓存
        private double _dpiScaleX = 1.0;
        private double _dpiScaleY = 1.0;

        // [注意] _previousForegroundWindow 本地字段已移除，状态提升至 WindowService 管理

        public RadialMenuWindow(RadialMenuViewModel vm, IConfigService configService, IWindowService windowService)
        {
            InitializeComponent();
            DataContext = vm;
            _viewModel = vm;
            _configService = configService;
            _windowService = windowService; // [Fix] 赋值字段

            // 1. 监听 ViewModel 的属性变化 (Show/Hide 信号)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // 2. 窗口加载时初始化主题
            InitializeTheme();

            // 3. [Fix] 注册隐藏自身的能力 (使用 Dispatcher 调度回 UI 线程)
            _windowService.RegisterHideAction(() =>
            {
                // 检查是否在 UI 线程，如果不是，则调度过去
                this.Dispatcher.Invoke(() =>
                {
                    Dismiss();
                });
            });

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
        }

        private async void InitializeTheme()
        {
            try
            {
                var config = await _configService.LoadAsync();
                ThemeManager.ApplyTheme(this, config.Settings.LauncherTheme);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Theme Init Failed: {ex.Message}");
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
            // [修改] 1. 将当前窗口句柄存储到 Service 中
            var current = WindowHelper.GetForegroundWindow();
            var selfHandle = new WindowInteropHelper(this).Handle;

            if (current != IntPtr.Zero && current != selfHandle)
            {
                // 这一步至关重要，PKI 全靠它找回家的路
                _windowService.SetPreviousWindow(current);
            }

            // 2. [定位] 瞬间移动位置 & 刷新主题
            UpdateDpiAndPosition();
            RefreshThemeOnShow();

            // ... 后续逻辑保持不变 (Activate, Focus, Opacity=1) ...
            this.IsHitTestVisible = true;
            this.Activate(); // 抢占焦点
            this.Focus();
            this.UpdateLayout();
            this.Opacity = 1.0;
            CompositionTarget.Rendering += UpdateLoop;
        }

        private void Dismiss()
        {
            // 1. [冻结] 停止计算循环
            CompositionTarget.Rendering -= UpdateLoop;
            // 2. [隐身] 瞬间隐形
            this.Opacity = 0;
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

            // 5. [清理] 调用 ViewModel 清理逻辑
            _viewModel.ClearVisuals();
        }

        private async void RefreshThemeOnShow()
        {
            var config = await _configService.LoadAsync();
            ThemeManager.ApplyTheme(this, config.Settings.LauncherTheme);
        }

        // ==========================================
        // 🎮 游戏循环 & 坐标计算 (Game Loop & Math)
        // ==========================================

        private void UpdateLoop(object? sender, EventArgs e)
        {
            if (this.Opacity < 0.1) return;
            // 1. 获取全局物理坐标
            var screenPoint = Forms.Cursor.Position;
            // 2. 转换为相对于 Window 左上角的 WPF 逻辑坐标
            double logicalX = (screenPoint.X / _dpiScaleX) - this.Left;
            double logicalY = (screenPoint.Y / _dpiScaleY) - this.Top;

            // 3. 发送给 ViewModel
            _viewModel.HandleMouseMove(logicalX, logicalY);
        }

        private void UpdateDpiAndPosition()
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            _dpiScaleX = dpi.DpiScaleX;
            _dpiScaleY = dpi.DpiScaleY;

            var screenPoint = Forms.Cursor.Position;
            double wpfMouseX = screenPoint.X / _dpiScaleX;
            double wpfMouseY = screenPoint.Y / _dpiScaleY;

            double width = this.ActualWidth > 0 ? this.ActualWidth : 400;
            double height = this.ActualHeight > 0 ? this.ActualHeight : 400;

            this.Left = wpfMouseX - (width / 2);
            this.Top = wpfMouseY - (height / 2);
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