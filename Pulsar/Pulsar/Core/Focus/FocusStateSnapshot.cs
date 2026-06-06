using System;

namespace Pulsar.Core.Focus
{
    public sealed class FocusStateSnapshot
    {
        public IntPtr WindowHandle { get; init; }
        public uint ProcessId { get; init; }
        public uint ThreadId { get; init; }
        public string WindowTitle { get; init; } = string.Empty;
        public DateTime CapturedAt { get; init; } = DateTime.UtcNow;
    }
}
