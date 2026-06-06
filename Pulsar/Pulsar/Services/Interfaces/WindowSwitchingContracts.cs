using System;
using System.Collections.Generic;
using Pulsar.Models;
using Pulsar.Native;

namespace Pulsar.Services.Interfaces
{
    public enum WindowSelectionSkipMode
    {
        None,
        SkipCurrentForeground,
        SkipPreviousWindow
    }

    public enum WindowSelectionIntent
    {
        ProcessActivation,
        GroupedSwitch,
        GroupedRootDirectTrigger,
        SubMenuDefault,
        QuickSwitch
    }

    public sealed class WindowSelectionRequest
    {
        public WindowSelectionIntent Intent { get; init; } = WindowSelectionIntent.ProcessActivation;

        public WindowSelectionSkipMode SkipMode { get; init; } = WindowSelectionSkipMode.None;

        public IntPtr CurrentForegroundHandle { get; init; }

        public IntPtr PreviousWindowHandle { get; init; }

        public PulsarNative.RECT? PreferredMonitorRect { get; init; }
    }

    public sealed class WindowSelectionResult
    {
        public required WindowSelectionRequest Request { get; init; }

        public ProcessWindowInfo? SelectedWindow { get; init; }

        public string DecisionReason { get; init; } = string.Empty;

        public IntPtr SkippedHandle { get; init; }

        public IReadOnlyList<IntPtr> RankedHandles { get; init; } = Array.Empty<IntPtr>();

        public bool HasSelection => SelectedWindow != null;
    }

    public enum WindowActivationFailureReason
    {
        None,
        InvalidHandle,
        ForegroundSwitchFailed
    }

    public sealed class WindowActivationResult
    {
        public required ProcessWindowInfo Window { get; init; }

        public bool Success { get; init; }

        public WindowActivationFailureReason FailureReason { get; init; }
    }
}
