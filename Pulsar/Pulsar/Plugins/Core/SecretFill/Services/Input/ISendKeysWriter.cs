namespace Pulsar.Plugins.Core.SecretFill.Services.Input
{
    public interface ISendKeysWriter
    {
        void SendWait(string keys);
        string SanitizeInput(string? input);
        void SendKeyCombination(string key);
    }
}
