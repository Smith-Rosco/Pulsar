using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Models;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Owns the Switch-mode inventory coherence: the short-lived
    /// <see cref="WindowSwitching.WindowInventoryCache"/>, invalidation on real
    /// foreground switches, menu-dismiss pre-warm, and the hit→use / miss→enumerate
    /// read path. Consumers that only need "the current desktop inventory" (radial
    /// menu seeding, process page providers) depend on this narrow seam instead of
    /// the full <see cref="IWindowService"/> facade.
    /// </summary>
    public interface IWindowInventoryCoordinator
    {
        /// <summary>
        /// Invalidate-on-real-switch: called when the foreground moved to a window.
        /// Ignores Pulsar's own activation (same pid) and a window that merely
        /// regains focus after the menu dismisses (same hwnd as last invalidation),
        /// so a peek→dismiss→reopen cycle keeps the warm cache.
        /// </summary>
        void OnForegroundChanged(IntPtr hwnd);

        /// <summary>
        /// Menu-dismiss pre-warm: forces a single-flight background enumeration so
        /// the next Switch-mode open finds a warm cache.
        /// </summary>
        void PrewarmOnMenuDismiss();

        /// <summary>
        /// Hit→use / miss→enumerate. Serves a fresh snapshot from the cache when
        /// available, otherwise enumerates, stores the result and returns it.
        /// </summary>
        Task<List<ProcessWindowInfo>> GetActiveWindowsAsync();

        /// <summary>
        /// Pure cache read: true + the cached snapshot when still fresh, otherwise
        /// false with a null out value. Does not enumerate.
        /// </summary>
        bool TryGetCached(out List<ProcessWindowInfo>? windows);

        /// <summary>Hands an externally enumerated snapshot back into the cache.</summary>
        void Store(List<ProcessWindowInfo> windows);
    }
}
