using Pulsar.Services.Interfaces;
using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Pulsar.Services
{
    public class MouseTrackingService : IMouseTrackingService, IDisposable
    {
        private readonly IWindowService _windowService;
        private readonly ISlotLayoutEngine _layoutEngine;
        private bool _isTracking;
        private IntPtr _windowHandle;
        private LayoutParameters _layoutParameters;
        private DateTime _lastUpdate = DateTime.MinValue;
        private const int MinUpdateIntervalMs = 16;

        public event EventHandler<Vector>? MousePositionChanged;

        public MouseTrackingService(IWindowService windowService, ISlotLayoutEngine layoutEngine)
        {
            _windowService = windowService;
            _layoutEngine = layoutEngine;
        }

        public Vector RelativePosition { get; private set; }

        public bool IsInDeadZone { get; private set; }

        public int HoveredSlotIndex { get; private set; }

        public int HitTest(int screenX, int screenY)
        {
            var relative = ScreenToRelative(new System.Windows.Point(screenX, screenY));
            return HitTestRelative(relative);
        }

        public void SetWindowHandle(IntPtr handle)
        {
            _windowHandle = handle;
        }

        public void SetLayoutParameters(LayoutParameters parameters)
        {
            _layoutParameters = parameters;
        }

        public void StartTracking()
        {
            if (_isTracking) return;
            _isTracking = true;
            CompositionTarget.Rendering += OnRender;
        }

        public void StopTracking()
        {
            if (!_isTracking) return;
            _isTracking = false;
            CompositionTarget.Rendering -= OnRender;
        }

        private void OnRender(object? sender, EventArgs e)
        {
            if (!_isTracking) return;

            var now = DateTime.Now;
            if ((now - _lastUpdate).TotalMilliseconds < MinUpdateIntervalMs) return;
            _lastUpdate = now;

            var screenPos = GetGlobalCursorPosition();
            var relativePos = ScreenToRelative(screenPos);
            RelativePosition = relativePos;

            HoveredSlotIndex = HitTestRelative(relativePos);
            MousePositionChanged?.Invoke(this, relativePos);
        }

        private int HitTestRelative(Vector relativePos)
        {
            var dx = relativePos.X - _layoutParameters.CenterX;
            var dy = relativePos.Y - _layoutParameters.CenterY;
            IsInDeadZone = Math.Sqrt(dx * dx + dy * dy) < _layoutParameters.DeadZoneRadius;
            return IsInDeadZone ? 0 : _layoutEngine.HitTest(relativePos, _layoutParameters);
        }

        private System.Windows.Point GetGlobalCursorPosition()
        {
            Pulsar.Native.PulsarNative.GetCursorPos(out var pt);
            return new System.Windows.Point(pt.X, pt.Y);
        }

        private Vector ScreenToRelative(System.Windows.Point screenPoint)
        {
            if (_windowHandle == IntPtr.Zero)
            {
                return new Vector();
            }

            var windowRect = GetWindowRect(_windowHandle);
            var devicePoint = new System.Windows.Point(
                screenPoint.X - windowRect.Left,
                screenPoint.Y - windowRect.Top);

            var source = HwndSource.FromHwnd(_windowHandle);
            var transform = source?.CompositionTarget?.TransformFromDevice;

            if (transform.HasValue)
            {
                var logicalPoint = transform.Value.Transform(devicePoint);
                return new Vector(logicalPoint.X, logicalPoint.Y);
            }

            return new Vector(devicePoint.X, devicePoint.Y);
        }

        private Rect GetWindowRect(IntPtr hwnd)
        {
            Pulsar.Native.PulsarNative.GetWindowRect(hwnd, out var rect);
            return new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        public void Dispose()
        {
            StopTracking();
        }
    }
}
