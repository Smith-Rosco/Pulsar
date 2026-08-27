using System;
using System.Collections.Generic;
using Pulsar.Models;

namespace Pulsar.Services.WindowSwitching
{
    /// <summary>
    /// Short-lived, thread-safe cache for the desktop window inventory snapshot
    /// consumed by Switch-mode radial menu loads and the process picker. A hit skips
    /// the ~200ms full desktop enumeration that would otherwise run on every menu
    /// open. The snapshot is invalidated when the foreground moves to a non-Pulsar
    /// window (i.e. the desktop changed) and bounded by a short TTL so stale data is
    /// never served for long.
    /// </summary>
    public sealed class WindowInventoryCache
    {
        private readonly TimeSpan _ttl;
        private readonly object _lock = new();
        private List<ProcessWindowInfo>? _snapshot;
        private DateTime _snapshotUtc;
        private bool _isValid;

        public WindowInventoryCache(TimeSpan? ttl = null)
        {
            _ttl = ttl ?? TimeSpan.FromSeconds(2);
        }

        /// <summary>
        /// Marks the snapshot stale so the next read falls through to a fresh
        /// enumeration. Idempotent.
        /// </summary>
        public void Invalidate()
        {
            lock (_lock)
            {
                _isValid = false;
            }
        }

        /// <summary>
        /// Returns a shallow copy of the snapshot when it is still fresh, otherwise
        /// null. The copy keeps callers from mutating the cached list.
        /// </summary>
        public bool TryGet(out List<ProcessWindowInfo>? snapshot)
        {
            lock (_lock)
            {
                if (_isValid && _snapshot != null && DateTime.UtcNow - _snapshotUtc < _ttl)
                {
                    snapshot = new List<ProcessWindowInfo>(_snapshot);
                    return true;
                }

                snapshot = null;
                return false;
            }
        }

        /// <summary>
        /// Stores a fresh enumeration result as the current snapshot.
        /// </summary>
        public void Store(List<ProcessWindowInfo> snapshot)
        {
            lock (_lock)
            {
                _snapshot = snapshot;
                _snapshotUtc = DateTime.UtcNow;
                _isValid = true;
            }
        }
    }
}
