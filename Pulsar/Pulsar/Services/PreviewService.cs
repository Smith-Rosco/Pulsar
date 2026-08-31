using Pulsar.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pulsar.Native;

namespace Pulsar.Services
{
    public class PreviewService : IPreviewService
    {
        private readonly IWindowCaptureService _captureService;
        private readonly ILiveWindowPreviewHost _liveWindowPreviewHost;
        private readonly Func<IntPtr, bool> _isWindow;
        private readonly Func<IntPtr, bool> _isIconic;
        private readonly Func<IntPtr, bool> _isCloaked;
        private readonly Dictionary<IntPtr, BitmapSource> _cache = new();

        public PreviewService(IWindowCaptureService captureService)
            : this(captureService, new LiveWindowPreviewHost(), PulsarNative.IsWindow, PulsarNative.IsIconic, IsCloaked)
        {
        }

        internal PreviewService(
            IWindowCaptureService captureService,
            ILiveWindowPreviewHost liveWindowPreviewHost,
            Func<IntPtr, bool> isWindow,
            Func<IntPtr, bool> isIconic,
            Func<IntPtr, bool> isCloaked)
        {
            _captureService = captureService;
            _liveWindowPreviewHost = liveWindowPreviewHost;
            _isWindow = isWindow;
            _isIconic = isIconic;
            _isCloaked = isCloaked;
        }

        public async Task<ResolvedWindowPreview> ResolvePreviewAsync(IntPtr hWnd, ImageSource? icon, PreviewHostContext hostContext)
        {
            if (hWnd == IntPtr.Zero || !_isWindow(hWnd))
            {
                InvalidateCache(hWnd);
                ClearLivePreview();
                return ResolvedWindowPreview.Icon(icon);
            }

            if (_liveWindowPreviewHost.TryShowPreview(hWnd, hostContext))
            {
                return ResolvedWindowPreview.Live();
            }

            if (_cache.TryGetValue(hWnd, out var cachedSnapshot))
            {
                ClearLivePreview();
                return ResolvedWindowPreview.Snapshot(cachedSnapshot);
            }

            if (_isIconic(hWnd) || _isCloaked(hWnd))
            {
                ClearLivePreview();
                return ResolvedWindowPreview.Icon(icon);
            }

            var snapshot = await _captureService.CaptureWindowAsync(hWnd);
            if (snapshot is BitmapSource bitmap)
            {
                _cache[hWnd] = bitmap;
                ClearLivePreview();
                return ResolvedWindowPreview.Snapshot(bitmap);
            }

            ClearLivePreview();
            return ResolvedWindowPreview.Icon(icon);
        }

        public void InvalidateCache(IntPtr hWnd)
        {
            _cache.Remove(hWnd);

            if (hWnd == IntPtr.Zero || !_isWindow(hWnd))
            {
                ClearLivePreview();
            }
        }

        public void ClearLivePreview()
        {
            _liveWindowPreviewHost.Clear();
        }

        public void ClearCache()
        {
            ClearLivePreview();
            _cache.Clear();
        }

        private static bool IsCloaked(IntPtr hWnd)
        {
            return PulsarNative.DwmGetWindowAttribute(hWnd, PulsarNative.DWMWA_CLOAKED, out int isCloaked, sizeof(int)) == 0 && isCloaked != 0;
        }
    }
}
