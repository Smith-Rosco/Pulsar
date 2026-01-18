using System;
using System.Text.Json.Serialization;
using Pulsar.Models;

namespace Pulsar.Features.Pki.Models
{
    public class SecretItem : GridItemBase
    {
        // [New] 唯一标识符，用于关联 secrets.json 中的加密数据
        public Guid Id { get; set; } = Guid.NewGuid();

        // [New] 上下文感知：目标进程名 (例如 "chrome")
        // 如果当前窗口匹配此名称，Pulsar 会高亮推荐此槽位
        private string _targetProcessName = string.Empty;
        public string TargetProcessName
        {
            get => _targetProcessName;
            set => SetProperty(ref _targetProcessName, value);
        }

        private string _account = string.Empty;
        // [Key] 敏感数据不序列化到 appsettings.json
        [JsonIgnore]
        public string Account
        {
            get => _account;
            set => SetProperty(ref _account, value);
        }

        private string _encryptedData = string.Empty;
        // [Key] 敏感数据不序列化到 appsettings.json
        [JsonIgnore]
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