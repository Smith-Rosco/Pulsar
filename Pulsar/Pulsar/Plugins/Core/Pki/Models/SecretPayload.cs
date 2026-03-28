namespace Pulsar.Plugins.Core.Pki.Models
{
    /// <summary>
    /// 存储在 secrets.json 中的敏感数据载体
    /// </summary>
    public class SecretPayload
    {
        public string Label { get; set; } = string.Empty;

        public string Account { get; set; } = string.Empty;
        public string EncryptedData { get; set; } = string.Empty;
    }
}
