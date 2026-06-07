namespace Pulsar.Plugins.Core.Pki.Services.Input
{
    public interface ISendKeysWriter
    {
        void SendWait(string keys);
        string SanitizeInput(string? input);
        void SendKeyCombination(string key);
    }
}
