using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media; // 必须引用: CompositionTarget
using Pulsar.Helpers;       // 必须引用: ThemeManager
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;

// [重要] 引用 WinForms 获取全局鼠标坐标
using Forms = System.Windows.Forms;
// [重要] 消除 Point 类型二义性
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
            // [恢复] 1. 计算正确的位置 (居中于鼠标)
            UpdateDpiAndPosition();

            // [新增] 2. 每次唤起时刷新主题 (防止设置页修改后这里没变)
            RefreshThemeOnShow();

            // 3. 显示并激活窗口
            this.Show();
            this.Activate();
            this.Focus();

            // [恢复] 4. 开启渲染循环 (高性能鼠标追踪)
            CompositionTarget.Rendering += UpdateLoop;
        }

        private void Dismiss()
        {
            // [恢复] 停止渲染循环
            CompositionTarget.Rendering -= UpdateLoop;
            this.Hide();
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
            if (this.Visibility != Visibility.Visible) return;

            // 1. 获取全局物理坐标 (屏幕绝对像素)
            var screenPoint = Forms.Cursor.Position;

            // 2. 转换为相对于 Window 左上角的 WPF 逻辑坐标
            // 公式：(全局物理坐标 / DPI) - 窗口逻辑左上角
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