namespace Pulsar.Plugins.Core.SecretFill.Models.Execution
{
    public enum SecretFillExecutionStage
    {
        Validation,
        SecretLookup,
        Decryption,
        HideLauncher,
        FocusRestore,
        Injection,
        Completed
    }
}
