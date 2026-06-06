using System;

namespace Pulsar.Core.Focus
{
    public sealed class QuickSwitchResult
    {
        public bool Success { get; init; }
        public IntPtr SwitchedToHandle { get; init; }
        public bool NoValidHistory { get; init; }
    }
}
