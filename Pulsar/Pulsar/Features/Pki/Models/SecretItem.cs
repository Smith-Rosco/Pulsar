using Pulsar.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Pulsar.Features.Pki.Models
{
    /// <summary>
    /// 代表一个加密的凭据项
    /// </summary>
    public class SecretItem : GridItemBase
    {
        private string _account = string.Empty;
        public string Account
        {
            get => _account;
            set => SetProperty(ref _account, value);
        }

        // 注意：在实际序列化时，这里应该存储加密后的 Base64 字符串
        // 运行时通过 CredentialsManager 解密
        private string _encryptedData = string.Empty;
        public string EncryptedData
        {
            get => _encryptedData;
            set => SetProperty(ref _encryptedData, value);
        }

        private bool _autoEnter = true;
        public bool AutoEnter
        {
            get => _autoEnter;
            set => SetProperty(ref _autoEnter, value);
        }
    }
}