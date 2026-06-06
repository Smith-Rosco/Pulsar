using System;

namespace Pulsar.Core.Focus
{
    public sealed class FocusActivationResult
    {
        public bool Success { get; init; }
        public FocusActivationFailureReason FailureReason { get; init; }
        public IntPtr TargetHandle { get; init; }
        public bool VerificationPassed { get; init; }
        public IntPtr ActualForegroundAfterActivation { get; init; }
    }
}
