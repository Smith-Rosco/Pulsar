using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Native;

namespace Pulsar.Services.WindowSwitching
{
    internal sealed class WindowTrackingSnapshot
    {
        public DateTime FirstSeenTime { get; init; }

        public DateTime ActivationTime { get; init; }
    }

    internal sealed class WindowTrackingService
    {
        private sealed class WindowRegistryEntry
        {
            public DateTime FirstSeenTime { get; set; }

            public DateTime LastActivationTime { get; set; }
        }

        private readonly ConcurrentDictionary<IntPtr, WindowRegistryEntry> _windowRegistry = new();

        public IntPtr PreviousWindowHandle { get; private set; }

        public void SetPreviousWindow(IntPtr handle)
        {
            PreviousWindowHandle = handle;
        }

        public WindowTrackingSnapshot SnapshotWindow(IntPtr hwnd)
        {
            if (_windowRegistry.TryGetValue(hwnd, out var existing))
            {
                return new WindowTrackingSnapshot
                {
                    FirstSeenTime = existing.FirstSeenTime,
                    ActivationTime = existing.LastActivationTime
                };
            }

            var entry = _windowRegistry.GetOrAdd(
                hwnd,
                _ => new WindowRegistryEntry
                {
                    FirstSeenTime = DateTime.Now,
                    LastActivationTime = DateTime.MinValue
                });

            return new WindowTrackingSnapshot
            {
                FirstSeenTime = entry.FirstSeenTime,
                ActivationTime = entry.LastActivationTime
            };
        }

        public WindowTrackingSnapshot RegisterOrUpdateWindow(IntPtr hwnd)
        {
            var entry = _windowRegistry.AddOrUpdate(
                hwnd,
                _ => new WindowRegistryEntry
                {
                    FirstSeenTime = DateTime.Now,
                    LastActivationTime = DateTime.Now
                },
                (_, existing) =>
                {
                    existing.LastActivationTime = DateTime.Now;
                    return existing;
                });

            return new WindowTrackingSnapshot
            {
                FirstSeenTime = entry.FirstSeenTime,
                ActivationTime = entry.LastActivationTime
            };
        }

        public int CleanupDeadEntries()
        {
            List<IntPtr> deadHandles = _windowRegistry
                .Where(kvp => !PulsarNative.IsWindow(kvp.Key))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (IntPtr handle in deadHandles)
            {
                _windowRegistry.TryRemove(handle, out _);
            }

            return deadHandles.Count;
        }
    }
}
