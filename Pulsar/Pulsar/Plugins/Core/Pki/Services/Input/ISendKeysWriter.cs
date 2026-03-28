namespace Pulsar.Plugins.Core.Pki.Services.Input
{
    public interface ISendKeysWriter
    {
        void SendWait(string keys);
        string EscapeForSendKeys(string? input);
    }
}
