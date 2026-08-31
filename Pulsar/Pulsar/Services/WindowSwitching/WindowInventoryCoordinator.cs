using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.WindowSwitching
{
    /// <summary>
    /// Owns the Switch-mode inventory coherence end to end: the short-lived cache,
    /// invalidation on real foreground switches (<see cref="OnForegroundChanged"/>),
    /// menu-dismiss pre-warm (<see cref="PrewarmOnMenuDismiss"/>), and the
    /// hit→use / miss→enumerate read path (<see cref="GetActiveWindowsAsync"/>).
    /// Registered as a singleton via DI; a test fake is the second adapter.
    /// </summary>
    public sealed class WindowInventoryCoordinator : IWindowInventoryCoordinator
    {
        private readonly IWindowInventoryService _inventoryService;
        private readonly IWindowEligibilityEvaluator _eligibilityEvaluator;
        private readonly WindowTrackingService _trackingService;
        private readonly IWindowCaptureService _captureService;
        private readonly WindowInventoryCache _cache;
        private readonly ILogger<WindowInventoryCoordinator> _logger;
        private readonly int _currentProcessId;

        private int _inventoryRefreshInFlight;
        private IntPtr _lastInventoryInvalidationHwnd;

        public WindowInventoryCoordinator(
            IWindowInventoryService inventoryService,
            IWindowEligibilityEvaluator eligibilityEvaluator,
            WindowTrackingService trackingService,
            IWindowCaptureService captureService,
            WindowInventoryCache cache,
            ILogger<WindowInventoryCoordinator> logger,
            int currentProcessId)
        {
            _inventoryService = inventoryService;
            _eligibilityEvaluator = eligibilityEvaluator;
            _trackingService = trackingService;
            _captureService = captureService;
            _cache = cache;
            _logger = logger;
            _currentProcessId = currentProcessId;
        }

        /// <summary>
        /// Invalidate the Switch-mode inventory snapshot only on a real switch:
        /// the foreground moved to a *new* non-Pulsar window. The radial menu's own
        /// activation (same process) is ignored, and a window simply regaining focus
        /// after the menu dismisses is not a desktop change, so a peek→dismiss→reopen
        /// cycle keeps the warm cache instead of re-enumerating.
        /// </summary>
        public void OnForegroundChanged(IntPtr hwnd)
        {
            PulsarNative.GetWindowThreadProcessId(hwnd, out uint pid);
            if ((int)pid == _currentProcessId || hwnd == _lastInventoryInvalidationHwnd)
            {
                return;
            }

            _lastInventoryInvalidationHwnd = hwnd;
            _cache.Invalidate();
            RefreshInBackground(force: false);
        }

        /// <summary>
        /// Menu-dismiss pre-warm: forces a single-flight background enumeration so
        /// the next Switch-mode open finds a warm cache.
        /// </summary>
        public void PrewarmOnMenuDismiss()
        {
            RefreshInBackground(force: true);
        }

        public async Task<List<ProcessWindowInfo>> GetActiveWindowsAsync()
        {
            // Serve a fresh snapshot from the cache when available (the Switch-mode
            // menu and process picker open far more often than the desktop changes),
            // otherwise enumerate and cache the result for the next caller.
            if (_cache.TryGet(out var cached))
            {
                return cached!;
            }

            var windows = await _inventoryService.GetActiveWindowsAsync(
                _eligibilityEvaluator.IsDiscoveryBlacklisted,
                _trackingService.SnapshotWindow,
                _captureService.ExtractIcon,
                null);

            _cache.Store(windows);
            return windows;
        }

        public bool TryGetCached(out List<ProcessWindowInfo>? windows)
            => _cache.TryGet(out windows);

        public void Store(List<ProcessWindowInfo> windows)
            => _cache.Store(windows);

        private void RefreshInBackground(bool force)
        {
            // Single-flight: at most one background enumeration at a time. A menu open
            // that misses the cache enumerates inline and repopulates it, so this is
            // only a pre-warm, never a correctness requirement.
            if (System.Threading.Interlocked.Exchange(ref _inventoryRefreshInFlight, 1) != 0)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    // A menu open (or an earlier refresh) may have already repopulated
                    // the cache while we queued; nothing to do then unless the caller
                    // asked for a forced refresh (menu-dismiss pre-warm) to keep the
                    // next Switch-mode open on a warm cache.
                    if (!force && _cache.TryGet(out _))
                    {
                        return;
                    }

                    var windows = await _inventoryService.GetActiveWindowsAsync(
                        _eligibilityEvaluator.IsDiscoveryBlacklisted,
                        _trackingService.SnapshotWindow,
                        _captureService.ExtractIcon,
                        null);

                    _cache.Store(windows);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[WindowInventoryCache] Background refresh failed");
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref _inventoryRefreshInFlight, 0);
                }
            });
        }
    }
}
