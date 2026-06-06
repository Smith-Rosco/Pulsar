using System;

namespace Pulsar.Core.Focus
{
    public sealed class FocusReleaseResult
    {
        public bool Success { get; init; }
        public IntPtr ReleasedToHandle { get; init; }
    }
}
