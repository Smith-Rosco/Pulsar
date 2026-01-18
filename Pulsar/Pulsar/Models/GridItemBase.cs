// [Path]: Pulsar/Pulsar/Models/GridItemBase.cs

using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
// 引用 SecretItem 所在的命名空间
using Pulsar.Features.Pki.Models;

namespace Pulsar.Models
{
    // 配置 JSON 多态识别器
    [JsonDerivedType(typeof(LauncherItem), typeDiscriminator: "launcher")]
    [JsonDerivedType(typeof(CommandItem), typeDiscriminator: "command")]
    // 注册 SecretItem
    [JsonDerivedType(typeof(SecretItem), typeDiscriminator: "secret")]
    public abstract class GridItemBase : ObservableObject
    {
        private int _slot;
        private string _label = string.Empty;
        private string _iconKey = string.Empty;

        // [New] 修复 WPF Binding Error 40
        // 虽然业务逻辑主要在 SlotViewModel 使用，但在 SettingsWindow 中
        // DataTemplate 直接绑定了 GridItemBase，因此基类必须有此属性。
        private bool _isRecommended;

        public int Slot
        {
            get => _slot;
            set => SetProperty(ref _slot, value);
        }

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public string IconKey
        {
            get => _iconKey;
            set => SetProperty(ref _iconKey, value);
        }

        // [New] 添加属性并忽略序列化
        [JsonIgnore]
        public bool IsRecommended
        {
            get => _isRecommended;
            set => SetProperty(ref _isRecommended, value);
        }
    }
}