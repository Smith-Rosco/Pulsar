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
        private readonly Action<string>? _logDebug;

        public WindowSelectionEngine(Action<string>? logDebug = null)
        {
            _logDebug = logDebug;
        }

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
                _logDebug?.Invoke($"[WindowSelectionEngine] No valid candidates. Intent={request.Intent}, SkipMode={request.SkipMode}, CurrentForeground={request.CurrentForegroundHandle}, PreviousWindow={request.PreviousWindowHandle}");

                return new WindowSelectionResult
                {
                    Request = request,
                    DecisionReason = "No valid candidates",
                    RankedHandles = Array.Empty<IntPtr>()
                };
            }

            IntPtr skippedHandle = ResolveSkippedHandle(orderedCandidates, request);

            _logDebug?.Invoke($"[WindowSelectionEngine] Request Intent={request.Intent}, SkipMode={request.SkipMode}, CurrentForeground={request.CurrentForegroundHandle}, PreviousWindow={request.PreviousWindowHandle}, SkippedHandle={skippedHandle}, CandidateCount={orderedCandidates.Count}");

            for (int i = 0; i < orderedCandidates.Count; i++)
            {
                var candidate = orderedCandidates[i];
                _logDebug?.Invoke($"[WindowSelectionEngine] Candidate[{i}] Hwnd={candidate.Handle} Title='{candidate.Title}' Process='{candidate.ProcessName}' RealActivation={candidate.RealActivationTime:O} LastActivation={candidate.LastActivationTime:O} FirstSeen={candidate.FirstSeenTime:O}");
            }

            var selected = orderedCandidates.FirstOrDefault(candidate => candidate.Handle != skippedHandle)
                ?? orderedCandidates[0];

            string decisionReason = selected.RealActivationTime > DateTime.MinValue
                ? "Selected by tracked activation recency"
                : selected.LastActivationTime > DateTime.MinValue
                    ? "Selected by fallback Z-order recency"
                    : "Selected by stable display order";

            if (request.Intent == WindowSelectionIntent.GroupedRootDirectTrigger)
            {
                decisionReason = skippedHandle != IntPtr.Zero
                    ? $"{decisionReason}; grouped root direct trigger skipped current in-process foreground"
                    : $"{decisionReason}; grouped root direct trigger returned MRU target";
            }
            else if (skippedHandle != IntPtr.Zero && selected.Handle != skippedHandle)
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

        private static IntPtr ResolveSkippedHandle(
            IReadOnlyCollection<ProcessWindowInfo> orderedCandidates,
            WindowSelectionRequest request)
        {
            if (request.Intent == WindowSelectionIntent.GroupedRootDirectTrigger)
            {
                return orderedCandidates.Any(candidate => candidate.Handle == request.CurrentForegroundHandle)
                    ? request.CurrentForegroundHandle
                    : IntPtr.Zero;
            }

            return request.SkipMode switch
            {
                WindowSelectionSkipMode.SkipCurrentForeground => request.CurrentForegroundHandle,
                WindowSelectionSkipMode.SkipPreviousWindow => request.PreviousWindowHandle,
                _ => IntPtr.Zero
            };
        }
    }
}
