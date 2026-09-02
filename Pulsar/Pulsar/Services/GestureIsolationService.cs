using System;
using System.Collections.Generic;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    /// <summary>
    /// Pure isolation decision for the right-drag summon gesture. Operates only on
    /// <see cref="ForegroundWindowFacts"/> + <see cref="ProfileSettings"/> — all
    /// native reads live behind the injected <see cref="IGestureIsolationNative"/>,
    /// so this logic is fully unit-testable with no OS coupling.
    /// </summary>
    public sealed class GestureIsolationService : IGestureIsolationService
    {
        /// <summary>Pixel slack allowed when comparing the window rect to the monitor bounds.</summary>
        private const int FullscreenTolerance = 2;

        private readonly IGestureIsolationNative _native;

        public GestureIsolationService(IGestureIsolationNative native)
        {
            _native = native;
        }

        public bool IsGestureAllowed(ProfileSettings settings)
        {
            return IsGestureAllowed(_native.GetForegroundWindowFacts(), settings);
        }

        public bool IsGestureAllowed(ForegroundWindowFacts facts, ProfileSettings settings)
        {
            if (settings == null || !settings.GestureIsolationEnabled)
            {
                // Filter disabled preserves current behavior: every press is eligible.
                return true;
            }

            if (settings.GestureIsolationBlockFullscreen
                && !_native.IsFullscreenShellClass(facts.ClassName)
                && IsFullscreen(facts.WindowRect, facts.MonitorBounds))
            {
                return false;
            }

            bool onList = IsListed(facts.ProcessName, settings.GestureIsolationProcesses);
            return settings.GestureIsolationMode switch
            {
                GestureIsolationMode.Allowlist => onList,
                GestureIsolationMode.Blocklist => !onList,
                _ => true
            };
        }

        /// <summary>
        /// True when the window rect covers the monitor bounds under the cursor,
        /// compared against <c>rcMonitor</c> (not the work area) with a small tolerance.
        /// </summary>
        private static bool IsFullscreen(PulsarNative.RECT window, PulsarNative.RECT monitor)
        {
            return window.Left <= monitor.Left + FullscreenTolerance
                && window.Top <= monitor.Top + FullscreenTolerance
                && window.Right >= monitor.Right - FullscreenTolerance
                && window.Bottom >= monitor.Bottom - FullscreenTolerance;
        }

        /// <summary>
        /// Case-insensitive membership of <paramref name="processName"/> in the
        /// configured list. Entries are trimmed; malformed (blank) entries are
        /// ignored and never throw. An empty list yields <c>false</c>, which means:
        /// allow-list mode denies all gestures, block-list mode denies none.
        /// </summary>
        private static bool IsListed(string processName, IReadOnlyList<string>? processes)
        {
            if (processes == null || processes.Count == 0 || string.IsNullOrWhiteSpace(processName))
            {
                return false;
            }

            foreach (var entry in processes)
            {
                var trimmed = entry?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (string.Equals(trimmed, processName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
