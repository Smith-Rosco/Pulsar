using System;

namespace Pulsar.Plugins.Core.Pki.Models.Execution
{
    public sealed record InjectionStep(
        InjectionStepType Type,
        string? Value = null,
        int DelayMilliseconds = 0,
        IntPtr TargetWindowHandle = default);
}
