// [Path]: Pulsar/Pulsar/ViewModels/MenuTiming.cs
//
// Cross-module timing contract for the radial menu gesture release → dismiss chain.
//
// History: previously these values lived as naked literals across two modules,
// coupled only by a comment. The 180ms await in MenuSession.HandleGestureRightReleaseAsync
// must be ≥ the 160ms Dismiss fade started by RadialMenuWindow.Dismiss; if the await
// is shorter than the fade, the selection strategy runs while the fade is still on
// screen, blocking the UI thread during window activation and leaving a visible
// ghost of the menu. The 20ms grace explicitly names that margin.
//
// Out of scope: SlotOrb's hover 300/320ms (paired enter/release), and the submenu
// morph timings — those have their own seams (GetSubMenuEnterDuration, Tokens.xaml)
// and are not on the dismiss chain.

using System;

namespace Pulsar.ViewModels
{
    internal static class MenuTiming
    {
        /// <summary>
        /// Dismiss fade duration started by <see cref="Views.RadialMenuWindow.Dismiss"/>.
        /// Must be ≤ <see cref="DismissAwait"/>.
        /// </summary>
        public static readonly TimeSpan DismissFade = TimeSpan.FromMilliseconds(160);

        /// <summary>
        /// Margin added on top of <see cref="DismissFade"/> to absorb dispatcher jitter
        /// before the selection strategy runs.
        /// </summary>
        public const int DismissGraceMs = 20;

        /// <summary>
        /// Total time the gesture-release path waits before running the slot strategy.
        /// Equal to <see cref="DismissFade"/> + <see cref="DismissGraceMs"/>.
        /// </summary>
        public static TimeSpan DismissAwait => TimeSpan.FromMilliseconds(160 + DismissGraceMs);
    }
}