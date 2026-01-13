using Pulsar.Helpers;       // ThemeManager
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.Native;        // [修复] 添加此行以识别 WindowHelper
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Windows.Interop; // [新增] 用于 WindowInteropHelper 获取句柄
// 引用 WinForms 获取全局鼠标坐标
using Forms = System.Windows.Forms;
// 消除 Point 类型二义性
using Point = System.Windows.Point;

namespace Pulsar.Views
{
    public partial class RadialMenuWindow : Window
    {
        private readonly RadialMenuViewModel _viewModel;
        private readonly IConfigService _configService;

        // DPI 缩放比例缓存
        private double _dpiScaleX = 1.0;
        private double _dpiScaleY = 1.0;
        // [新增] 用于存储唤起 Pulsar 之前的那个窗口句柄
        private IntPtr _previousForegroundWindow = IntPtr.Zero;
        public RadialMenuWindow(RadialMenuViewModel vm, IConfigService configService)
        {
            InitializeComponent();
            DataContext = vm;
            _viewModel = vm;
            _configService = configService;

            // 1. 监听 ViewModel 的属性变化 (Show/Hide 信号)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            // 2. 窗口加载时初始化主题
            InitializeTheme();
            // ====================================================
            // 👻 [驻留模式初始化] (Resident Mode Init)
            // 窗口启动即 "Visible" 但完全透明、不可点击、不在任务栏显示
            // ====================================================
            this.Opacity = 0;
            this.Visibility = Visibility.Visible;
            this.IsHitTestVisible = false;
            this.ShowInTaskbar = false;
        }

        // [新增] 窗口句柄创建后的初始化钩子
        // 在这里注入 WS_EX_TOOLWINDOW 样式，将窗口从 Alt+Tab 中移除
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 1. 获取窗口句柄
            var hwnd = new WindowInteropHelper(this).Handle;

            // 2. 获取当前扩展样式
            long currentStyle = WindowHelper.GetWindowLong(hwnd, WindowHelper.GWL_EXSTYLE);

            // 3. 注入 ToolWindow 样式 (使其在 Alt+Tab 中不可见)
            // 注意：如果你在 WindowHelper 中还没定义 SetWindowLong，请查看下方的补充代码
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
            // [新增] 1. 在抢占焦点前，记录当前是谁拥有焦点
            // 如果当前焦点是 Pulsar 自己（极其罕见），则不更新，防止覆盖正确的历史句柄
            var current = WindowHelper.GetForegroundWindow();
            var selfHandle = new WindowInteropHelper(this).Handle;

            if (current != IntPtr.Zero && current != selfHandle)
            {
                _previousForegroundWindow = current;
            }

            // 2. [定位] 瞬间移动位置 & 刷新主题
            UpdateDpiAndPosition();
            RefreshThemeOnShow();

            // ... 后续逻辑保持不变 (Activate, Focus, Opacity=1) ...
            this.IsHitTestVisible = true;
            this.Activate(); // 这里发生了焦点抢占 
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

            // [新增] 4. 显式归还焦点
            // 如果这是一个取消操作（用户没选任何东西），必须把焦点还给之前的窗口
            // 这样 Alt+Tab 的历史堆栈才会保持 "Pulsar 不存在" 的状态
            if (_previousForegroundWindow != IntPtr.Zero)
            {
                WindowHelper.SetForegroundWindow(_previousForegroundWindow);
                // 归还后清空，避免逻辑污染
                // 注意：这里不清空也可以，视具体需求而定，但清空更安全
                _previousForegroundWindow = IntPtr.Zero;
            }

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
            // [Check] 驻留模式下窗口一直是 Visible，所以要改为检查 Opacity
            if (this.Opacity < 0.1) return;
            // 1. 获取全局物理坐标 (屏幕绝对像素)
            var screenPoint = Forms.Cursor.Position;
            // 2. 转换为相对于 Window 左上角的 WPF 逻辑坐标
            double logicalX = (screenPoint.X / _dpiScaleX) - this.Left;
            double logicalY = (screenPoint.Y / _dpiScaleY) - this.Top;

            // 3. 将相对坐标发送给 ViewModel 进行极坐标判定
            _viewModel.HandleMouseMove(logicalX, logicalY);
        }

        private void UpdateDpiAndPosition()
        {
            // 获取当前 DPI (防止多屏 DPI 不同导致偏移)
            var dpi = VisualTreeHelper.GetDpi(this);
            _dpiScaleX = dpi.DpiScaleX;
            _dpiScaleY = dpi.DpiScaleY;

            // 获取鼠标位置并转换为 WPF 逻辑单位
            var screenPoint = Forms.Cursor.Position;
            double wpfMouseX = screenPoint.X / _dpiScaleX;
            double wpfMouseY = screenPoint.Y / _dpiScaleY;
            // 计算窗口尺寸 (如果未加载则默认 400)
            double width = this.ActualWidth > 0 ? this.ActualWidth : 400;
            double height = this.ActualHeight > 0 ? this.ActualHeight : 400;
            // 设定窗口位置：鼠标中心
            this.Left = wpfMouseX - (width / 2);
            this.Top = wpfMouseY - (height / 2);
        }

        // 窗口关闭清理
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