namespace Pulsar.Plugins.Core.SecretFill.Contracts
{
    public interface ISecretProtector
    {
        string Encrypt(string plainText);
        string Decrypt(string encryptedBase64);
    }
}
