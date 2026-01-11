using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Pulsar.Models
{
    // 配置 JSON 多态识别器
    [JsonDerivedType(typeof(LauncherItem), typeDiscriminator: "launcher")]
    [JsonDerivedType(typeof(CommandItem), typeDiscriminator: "command")]
    public abstract class GridItemBase : ObservableObject
    {
        private int _slot;
        private string _label = string.Empty;
        private string _iconKey = string.Empty;

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

        // 图标系统核心字段 (支持路径或字体编码)
        public string IconKey
        {
            get => _iconKey;
            set => SetProperty(ref _iconKey, value);
        }
    }
}