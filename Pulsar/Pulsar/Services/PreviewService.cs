using Pulsar.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Pulsar.Services
{
    public class PreviewService : IPreviewService
    {
        private readonly IWindowService _windowService;
        private readonly Dictionary<IntPtr, BitmapSource> _cache = new();

        public PreviewService(IWindowService windowService)
        {
            _windowService = windowService;
        }

        public async Task<BitmapSource?> CaptureAsync(IntPtr hWnd)
        {
            if (_cache.TryGetValue(hWnd, out var cached))
            {
                return cached;
            }

            var snapshot = await _windowService.CaptureWindowAsync(hWnd);
            if (snapshot is BitmapSource source)
            {
                _cache[hWnd] = source;
                return source;
            }

            return null;
        }

        public void InvalidateCache(IntPtr hWnd)
        {
            _cache.Remove(hWnd);
        }

        public void ClearCache()
        {
            _cache.Clear();
        }
    }
}
