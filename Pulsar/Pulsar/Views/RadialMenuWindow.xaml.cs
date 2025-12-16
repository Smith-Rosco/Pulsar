using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Pulsar.ViewModels;

namespace Pulsar.Views
{
    public partial class RadialMenuWindow : Window
    {
        private readonly RadialMenuViewModel _viewModel;
        private readonly DispatcherTimer _updateTimer;
        
        // DPI & 坐标缓存
        private double _cachedDpiScale = 1.0;

        public RadialMenuWindow(RadialMenuViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            this.DataContext = _viewModel;

            // 监听 ViewModel 的显隐变化来控制窗口显示
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(RadialMenuViewModel.IsVisible))
                {
                    if (_viewModel.IsVisible) Summon();
                    else Dismiss();
                }
            };

            // 初始化渲染循环 (60FPS)
            _updateTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _updateTimer.Tick += UpdateLoop;
        }

        private void Summon()
        {
            // 1. 获取鼠标位置
            GetCursorPos(out POINT p);
            
            // 2. 计算 DPI 和逻辑坐标
            UpdateDpiAndPosition(p.X, p.Y);

            // 3. 显示窗口
            this.Show();
            this.Activate();
            this.Focus();
            
            // 4. 开启循环
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

            GetCursorPos(out POINT p);
            
            // 转换为相对于窗口中心的坐标
            double mouseLogX = p.X / _cachedDpiScale;
            double mouseLogY = p.Y / _cachedDpiScale;

            // 将绝对鼠标位置传给 ViewModel (ViewModel 内部会减去 CenterX/Y 计算相对偏移)
            // 注意：这里我们传的是“相对于窗口左上角”的逻辑坐标
            // ViewModel 的 HandleMouseMove 需要的是相对于 Canvas 的坐标？
            // RadialMenuViewModel 中定义 CenterX = 200, CenterY = 200。
            // 我们的窗口是 400x400。
            // 所以我们应该传入相对于窗口左上角的坐标。
            
            // 修正：UpdateDpiAndPosition 已经把窗口移到了鼠标中心。
            // 所以此时鼠标在窗口内的坐标应该是 (Width/2, Height/2) 附近。
            
            // 计算鼠标在窗口内的相对坐标
            double localX = mouseLogX - this.Left;
            double localY = mouseLogY - this.Top;

            _viewModel.HandleMouseMove(localX, localY);
        }

        private void UpdateDpiAndPosition(int physicalX, int physicalY)
        {
            // 简单的 DPI 获取逻辑 (生产环境建议更严谨的 MonitorAware)
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                _cachedDpiScale = source.CompositionTarget.TransformToDevice.M11;
            }
            else
            {
                // Fallback (通常 96 DPI = 1.0)
                _cachedDpiScale = 1.0; 
            }

            double logicalX = physicalX / _cachedDpiScale;
            double logicalY = physicalY / _cachedDpiScale;

            // 将窗口中心对准鼠标
            this.Left = logicalX - (this.Width / 2);
            this.Top = logicalY - (this.Height / 2);
        }

        // --- Win32 Helpers ---
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);
    }
}