namespace Pulsar.Plugins.Core.Pki.Models.Execution
{
    public enum PkiExecutionStage
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
