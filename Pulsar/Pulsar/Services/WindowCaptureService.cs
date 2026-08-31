using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// Window snapshot capture and executable icon extraction backed by native
    /// GDI/Shell APIs. Owns the icon cache so enumeration paths never re-extract
    /// icons for the same path. Registered as a singleton via DI.
    /// </summary>
    public sealed class WindowCaptureService : IWindowCaptureService
    {
        private readonly ILogger<WindowCaptureService> _logger;

        // [New] Icon Cache to prevent redundant IO/GDI operations
        // Key: ExePath, Value: ImageSource
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource?> _iconCache = new();

        public WindowCaptureService(ILogger<WindowCaptureService> logger)
        {
            _logger = logger;
        }

        public async Task<ImageSource?> CaptureWindowAsync(IntPtr hWnd)
        {
            return await Task.Run(() =>
            {
                if (hWnd == IntPtr.Zero || !PulsarNative.IsWindow(hWnd))
                {
                    _logger.LogWarning("[CaptureWindow] Invalid handle: {Hwnd}", hWnd);
                    return null;
                }

                try
                {
                    if (!PulsarNative.GetWindowRect(hWnd, out var rect))
                    {
                        _logger.LogWarning("[CaptureWindow] GetWindowRect failed for {Hwnd}", hWnd);
                        return null;
                    }
                    int w = rect.Right - rect.Left, h = rect.Bottom - rect.Top;
                    if (w <= 0 || h <= 0)
                    {
                        _logger.LogWarning("[CaptureWindow] Invalid dimensions {W}x{H} for {Hwnd}", w, h, hWnd);
                        return null;
                    }

                    var bmp = CaptureViaPrintWindow(hWnd, w, h);
                    if (bmp == null)
                    {
                        _logger.LogWarning("[CaptureWindow] PrintWindow failed for {Hwnd}", hWnd);
                        return null;
                    }

                    return DownscaleAndFreeze(bmp);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[CaptureWindow] Exception for {Hwnd}", hWnd);
                    return null;
                }
            });
        }

        public ImageSource? ExtractIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            // 1. Check Cache
            if (_iconCache.TryGetValue(path, out var cachedIcon))
            {
                return cachedIcon;
            }

            try
            {
                var shinfo = new PulsarNative.SHFILEINFO();
                IntPtr hIcon = PulsarNative.SHGetFileInfo(
                    path,
                    0,
                    ref shinfo,
                    (uint)System.Runtime.InteropServices.Marshal.SizeOf(shinfo),
                    PulsarNative.SHGFI_ICON | PulsarNative.SHGFI_LARGEICON);
                if (shinfo.hIcon != IntPtr.Zero)
                {
                    var image = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze();
                    PulsarNative.DestroyIcon(shinfo.hIcon);

                    // 2. Add to Cache
                    _iconCache.TryAdd(path, image);
                    return image;
                }
            }
            catch { }

            // Cache null result to prevent retrying bad paths
            _iconCache.TryAdd(path, null);
            return null;
        }

        private static System.Drawing.Bitmap? CaptureViaPrintWindow(IntPtr hWnd, int w, int h)
        {
            var bmp = new System.Drawing.Bitmap(w, h);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            IntPtr hdc = g.GetHdc();
            bool ok = false;
            try
            {
                ok = PulsarNative.PrintWindow(hWnd, hdc, 0x00000002)
                    || PulsarNative.PrintWindow(hWnd, hdc, 0);
            }
            finally
            {
                g.ReleaseHdc(hdc);
            }
            if (!ok) { bmp.Dispose(); return null; }
            return bmp;
        }

        private static ImageSource DownscaleAndFreeze(System.Drawing.Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height, maxDim = 400;
            if (w > maxDim || h > maxDim)
            {
                double ratio = (double)w / h;
                if (w > h) { w = maxDim; h = (int)(maxDim / ratio); }
                else { h = maxDim; w = (int)(maxDim * ratio); }
                using var scaled = new System.Drawing.Bitmap(w, h);
                using (var g = System.Drawing.Graphics.FromImage(scaled))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bmp, 0, 0, w, h);
                }
                bmp.Dispose();
                return BmpToSource(scaled);
            }
            return BmpToSource(bmp);
        }

        private static ImageSource BmpToSource(System.Drawing.Bitmap bmp)
        {
            IntPtr hBitmap = bmp.GetHbitmap();
            bmp.Dispose();
            try
            {
                var wpfBitmap = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                wpfBitmap.Freeze();
                return wpfBitmap;
            }
            finally
            {
                PulsarNative.DeleteObject(hBitmap);
            }
        }
    }
}
