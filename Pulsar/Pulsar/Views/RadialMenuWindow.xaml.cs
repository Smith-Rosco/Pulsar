using System;
using System.Windows;
using System.Windows.Media; // 用于 VisualTreeHelper
using System.Windows.Threading; // 用于 Timer
using Pulsar.ViewModels;
using Forms = System.Windows.Forms; // 使用别名简化鼠标获取

namespace Pulsar.Views
{
    public partial class RadialMenuWindow : Window
    {
        private readonly RadialMenuViewModel _viewModel;
        private readonly DispatcherTimer _updateTimer;

        // 缓存 DPI 缩放倍率
        private double _dpiScaleX = 1.0;
        private double _dpiScaleY = 1.0;

        public RadialMenuWindow(RadialMenuViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = _viewModel;

            // 1. 监听 ViewModel 的显隐变化 (MVVM 驱动 View)
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(RadialMenuViewModel.IsVisible))
                {
                    if (_viewModel.IsVisible) Summon();
                    else Dismiss();
                }
            };

            // 2. 初始化高频渲染/检测循环 (约 60FPS)
            _updateTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _updateTimer.Tick += UpdateLoop;
        }

        private void Summon()
        {
            // 每次显示前，刷新 DPI 和位置 (解决多屏/缩放变化问题)
            UpdateDpiAndPosition();

            this.Show();
            this.Activate();
            this.Focus();

            // 开启鼠标追踪循环
            _updateTimer.Start();
        }

        private void Dismiss()
        {
            this.Hide();
            _updateTimer.Stop();
        }

        private void UpdateLoop(object? sender, EventArgs e)
        {
            if (this.Visibility != Visibility.Visible) return;

            // 1. 获取物理屏幕坐标 (Pixels)
            var point = Forms.Cursor.Position;
            double physicalX = point.X;
            double physicalY = point.Y;

            // 2. 转换为逻辑坐标 (WPF Units)
            // 逻辑 = 物理 / DPI缩放
            double logicalMouseX = physicalX / _dpiScaleX;
            double logicalMouseY = physicalY / _dpiScaleY;

            // 3. 计算相对于窗口左上角的坐标
            // 这一步至关重要：ViewModel 需要知道鼠标相对于 "画布中心" 的位置
            // 但 ViewModel 里通常处理的是相对于 (0,0) 的偏移，或者相对于 Canvas 左上角的坐标
            // 这里我们传给 ViewModel "相对于窗口左上角" 的坐标
            double localX = logicalMouseX - this.Left;
            double localY = logicalMouseY - this.Top;

            _viewModel.HandleMouseMove(localX, localY);
        }

        private void UpdateDpiAndPosition()
        {
            // 1. 获取当前 DPI (兼容 .NET 8 / Per-Monitor DPI)
            var dpi = VisualTreeHelper.GetDpi(this);
            _dpiScaleX = dpi.DpiScaleX;
            _dpiScaleY = dpi.DpiScaleY;

            // 2. 获取鼠标物理位置
            var point = Forms.Cursor.Position;

            // 3. 计算鼠标的 WPF 逻辑坐标
            double wpfMouseX = point.X / _dpiScaleX;
            double wpfMouseY = point.Y / _dpiScaleY;

            // 4. 计算窗口尺寸 (防止首次加载时 ActualWidth 为 0)
            // 如果 ActualWidth 还是 0，使用 DesignWidth (400) 作为兜底
            double width = this.ActualWidth > 0 ? this.ActualWidth : 400;
            double height = this.ActualHeight > 0 ? this.ActualHeight : 400;

            // 5. 设置窗口位置 (使其中心对准鼠标)
            this.Left = wpfMouseX - (width / 2);
            this.Top = wpfMouseY - (height / 2);
        }
    }
}