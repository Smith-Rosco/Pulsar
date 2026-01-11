using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media; // CompositionTarget
using Pulsar.ViewModels;
using Forms = System.Windows.Forms;

// [核心修复] 强制指定 Point 为 WPF 类型
using Point = System.Windows.Point;

namespace Pulsar.Views
{
    public partial class RadialMenuWindow : Window
    {
        private readonly RadialMenuViewModel _viewModel;

        private double _dpiScaleX = 1.0;
        private double _dpiScaleY = 1.0;

        public RadialMenuWindow(RadialMenuViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = _viewModel;

            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(RadialMenuViewModel.IsVisible))
                {
                    if (_viewModel.IsVisible) Summon();
                    else Dismiss();
                }
            };
        }

        private void Summon()
        {
            UpdateDpiAndPosition();
            this.Show();
            this.Activate();
            this.Focus();

            CompositionTarget.Rendering += UpdateLoop;
        }

        private void Dismiss()
        {
            CompositionTarget.Rendering -= UpdateLoop;
            this.Hide();
        }

        private void UpdateLoop(object? sender, EventArgs e)
        {
            if (this.Visibility != Visibility.Visible) return;

            // 1. 获取全局物理坐标
            var screenPoint = Forms.Cursor.Position;

            // 2. 转换为相对于 Window 左上角的 WPF 逻辑坐标
            double logicalX = (screenPoint.X / _dpiScaleX) - this.Left;
            double logicalY = (screenPoint.Y / _dpiScaleY) - this.Top;

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
    }
}