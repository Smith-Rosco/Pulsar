using Pulsar.Helpers;       // ThemeManager
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;
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
            // 1. [定位] 瞬间移动位置 & 刷新主题
            // 由于窗口是驻留的，没有 Loaded 开销，这是纯数学计算
            UpdateDpiAndPosition();
            RefreshThemeOnShow();

            // 2. [唤醒] 开启交互与焦点抢占
            this.IsHitTestVisible = true;
            this.Activate();
            this.Focus();

            // 3. [同步] 强制刷新布局
            // 在 Opacity 变为 1 之前，确保所有 UI 元素已经在新位置排列完毕
            this.UpdateLayout();

            // 4. [显形] 瞬间显示 (GPU Opacity 调整，零延迟)
            this.Opacity = 1.0;

            // 5. [循环] 开启物理追踪
            CompositionTarget.Rendering += UpdateLoop;
        }

        private void Dismiss()
        {
            // 1. [冻结] 停止计算循环
            CompositionTarget.Rendering -= UpdateLoop;

            // 2. [隐身] 瞬间隐形
            // 注意：不调用 Hide()，保持窗口句柄和显存资源
            this.Opacity = 0;

            // 3. [穿透] 关闭交互，让鼠标点击穿透到后方窗口
            this.IsHitTestVisible = false;

            // 4. [清理] 调用 ViewModel 清理逻辑 (防止下次打开瞬间显示旧图标)
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