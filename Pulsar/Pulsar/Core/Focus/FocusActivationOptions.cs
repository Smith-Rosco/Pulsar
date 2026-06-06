namespace Pulsar.Core.Focus
{
    public sealed class FocusActivationOptions
    {
        public bool FlashAfterActivation { get; init; } = false;
        public bool VerifyAfterActivation { get; init; }
        public int VerifyDelayMs { get; init; } = 50;
        public int MaxRetries { get; init; } = 1;
    }
}
