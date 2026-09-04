// [Path]: Pulsar/Pulsar.E2E/Driver/Capture.cs

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Pulsar.E2E.Driver
{
    /// <summary>
    /// Screen-level capture via GDI <c>CopyFromScreen</c>. Screen-level (not
    /// window-level) so popup content — including the borderless radial menu and
    /// its popups — is always included, which window-scoped capture would miss.
    /// </summary>
    public static class Capture
    {
        public static void CaptureScreen(string outputPngPath)
        {
            var bounds = GetVirtualScreenBounds();
            using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            }
            bitmap.Save(outputPngPath, ImageFormat.Png);
        }

        /// <summary>
        /// Captures the virtual screen (all monitors) in physical pixels. Returns
        /// the rectangle actually captured.
        /// </summary>
        public static Rectangle CaptureScreenToBitmap(out Bitmap bitmap)
        {
            var bounds = GetVirtualScreenBounds();
            bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            return bounds;
        }

        private static Rectangle GetVirtualScreenBounds()
        {
            SetProcessDpiAware();
            var left = GetSystemMetrics(SM_XVIRTUALSCREEN);
            var top = GetSystemMetrics(SM_YVIRTUALSCREEN);
            var width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            var height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            if (width <= 0 || height <= 0)
            {
                // Fallback to primary screen metrics.
                width = GetSystemMetrics(SM_CXSCREEN);
                height = GetSystemMetrics(SM_CYSCREEN);
                left = 0;
                top = 0;
            }
            return new Rectangle(left, top, width, height);
        }

        private const int SM_XVIRTUALSCREEN = 76;
        private const int SM_YVIRTUALSCREEN = 77;
        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        private static void SetProcessDpiAware()
        {
            // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
            try
            {
                SetProcessDpiAwarenessContext(new IntPtr(-4));
            }
            catch
            {
                // Best effort; capture still works with system DPI.
            }
        }
    }
}
