using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.WindowSwitching
{
    public sealed class WindowSelectionEngine
    {
        public WindowSelectionResult SelectTargetWindow(
            IEnumerable<ProcessWindowInfo> windows,
            WindowSelectionRequest request,
            Func<IntPtr, bool>? isWindow = null)
        {
            isWindow ??= PulsarNative.IsWindow;

            var orderedCandidates = windows
                .Where(w => w != null && w.Handle != IntPtr.Zero && isWindow(w.Handle))
                .OrderByDescending(w => w.RealActivationTime > DateTime.MinValue)
                .ThenByDescending(w => w.RealActivationTime)
                .ThenByDescending(w => w.LastActivationTime)
                .ThenBy(w => w.FirstSeenTime)
                .ToList();

            if (orderedCandidates.Count == 0)
            {
                return new WindowSelectionResult
                {
                    Request = request,
                    DecisionReason = "No valid candidates",
                    RankedHandles = Array.Empty<IntPtr>()
                };
            }

            IntPtr skippedHandle = request.SkipMode switch
            {
                WindowSelectionSkipMode.SkipCurrentForeground => request.CurrentForegroundHandle,
                WindowSelectionSkipMode.SkipPreviousWindow => request.PreviousWindowHandle,
                _ => IntPtr.Zero
            };

            var selected = orderedCandidates.FirstOrDefault(candidate => candidate.Handle != skippedHandle)
                ?? orderedCandidates[0];

            string decisionReason = selected.RealActivationTime > DateTime.MinValue
                ? "Selected by tracked activation recency"
                : selected.LastActivationTime > DateTime.MinValue
                    ? "Selected by fallback Z-order recency"
                    : "Selected by stable display order";

            if (skippedHandle != IntPtr.Zero && selected.Handle != skippedHandle)
            {
                decisionReason = $"{decisionReason}; applied {request.SkipMode}";
            }

            return new WindowSelectionResult
            {
                Request = request,
                SelectedWindow = selected,
                DecisionReason = decisionReason,
                SkippedHandle = skippedHandle,
                RankedHandles = orderedCandidates.Select(candidate => candidate.Handle).ToList()
            };
        }
    }
}
