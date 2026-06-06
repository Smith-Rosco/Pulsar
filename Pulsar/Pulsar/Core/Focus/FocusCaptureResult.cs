using System;

namespace Pulsar.Core.Focus
{
    public sealed class FocusCaptureResult
    {
        public bool Success { get; init; }
        public IntPtr CapturedHandle { get; init; }
        public uint ProcessId { get; init; }
        public string ProcessName { get; init; } = string.Empty;
        public FocusStateSnapshot Snapshot { get; init; } = null!;
    }
}
