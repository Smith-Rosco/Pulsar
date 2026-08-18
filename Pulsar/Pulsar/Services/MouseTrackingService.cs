using Pulsar.Services.Interfaces;
using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Pulsar.Services
{
    /// <summary>
    /// Pure cursor sampler: captures the global cursor position on the WPF rendering
    /// loop and raises <see cref="MousePositionChanged"/> with the window-relative DIP
    /// point. Hit-testing and hover decisions belong to the Menu Session.
    /// </summary>
    public class MouseTrackingService : IMouseTrackingService, IDisposable
    {
        private IntPtr _windowHandle;
        private bool _isTracking;
        private DateTime _lastUpdate = DateTime.MinValue;
        private const int MinUpdateIntervalMs = 16;

        public event EventHandler<Vector>? MousePositionChanged;

        public Vector ToRelative(int screenX, int screenY)
        {
            return ScreenToRelative(new System.Windows.Point(screenX, screenY));
        }

        public void SetWindowHandle(IntPtr handle)
        {
            _windowHandle = handle;
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
            MousePositionChanged?.Invoke(this, relativePos);
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
