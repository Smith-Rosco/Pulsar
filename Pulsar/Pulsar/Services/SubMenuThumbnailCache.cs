using Pulsar.Native;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Pulsar.Services
{
    internal sealed class SubMenuThumbnailCache : ISubMenuThumbnailCache, IDisposable
    {
        private readonly IWindowService _windowService;
        private readonly ConcurrentDictionary<IntPtr, CachedThumbnail> _cache = new();
        private readonly LinkedList<IntPtr> _accessOrder = new();
        private readonly object _lruLock = new();
        private const int MaxEntries = 50;

        public SubMenuThumbnailCache(IWindowService windowService)
        {
            _windowService = windowService;
        }

        public ImageSource? Get(IntPtr hWnd)
        {
            if (_cache.TryGetValue(hWnd, out var entry))
            {
                if (!IsWindowValid(hWnd, entry.WindowTitle))
                {
                    Invalidate(hWnd);
                    return null;
                }
                Touch(hWnd);
                return entry.Snapshot;
            }
            return null;
        }

        public async Task<ImageSource?> GetOrCaptureAsync(IntPtr hWnd, string windowTitle)
        {
            var hit = Get(hWnd);
            if (hit != null) return hit;

            EvictIfNeeded();

            var snapshot = await _windowService.CaptureWindowAsync(hWnd);
            if (snapshot == null) return null;

            var cached = new CachedThumbnail(snapshot, windowTitle);
            _cache[hWnd] = cached;
            Touch(hWnd);
            return snapshot;
        }

        public void Invalidate(IntPtr hWnd)
        {
            if (_cache.TryRemove(hWnd, out _))
            {
                lock (_lruLock) _accessOrder.Remove(hWnd);
            }
        }

        public void Clear()
        {
            _cache.Clear();
            lock (_lruLock) _accessOrder.Clear();
        }

        private bool IsWindowValid(IntPtr hWnd, string expectedTitle)
        {
            if (hWnd == IntPtr.Zero) return false;
            try
            {
                var sb = new StringBuilder(256);
                int len = PulsarNative.GetWindowText(hWnd, sb, sb.Capacity);
                return len > 0 && sb.ToString(0, len) == expectedTitle;
            }
            catch
            {
                return false;
            }
        }

        private void Touch(IntPtr hWnd)
        {
            lock (_lruLock)
            {
                _accessOrder.Remove(hWnd);
                _accessOrder.AddFirst(hWnd);
            }
        }

        private void EvictIfNeeded()
        {
            if (_cache.Count < MaxEntries) return;

            lock (_lruLock)
            {
                while (_cache.Count >= MaxEntries && _accessOrder.Last != null)
                {
                    var last = _accessOrder.Last.Value;
                    _accessOrder.RemoveLast();
                    _cache.TryRemove(last, out _);
                }
            }
        }

        public void Dispose()
        {
            Clear();
        }

        private readonly record struct CachedThumbnail(ImageSource Snapshot, string WindowTitle);
    }
}
