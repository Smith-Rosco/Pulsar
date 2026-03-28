namespace Pulsar.Plugins.Core.Pki.Contracts
{
    public interface ISecretProtector
    {
        string Encrypt(string plainText);
        string Decrypt(string encryptedBase64);
    }
}
