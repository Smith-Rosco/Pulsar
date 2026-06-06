namespace Pulsar.Core.Focus
{
    public enum FocusActivationFailureReason
    {
        None,
        InvalidHandle,
        AttachThreadInputFailed,
        ForegroundSwitchFailed,
        VerificationFailed,
        TargetThreadHung
    }
}
