// [Path]: Pulsar/Pulsar/ViewModels/SlotViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace Pulsar.ViewModels
{
    public partial class SlotViewModel : ObservableObject
    {
        private int _slotIndex;
        private double _x;
        private double _y;
        private double _size;
        private string _label = string.Empty;
        private bool _isActive;
        private string _iconKey = string.Empty; // [新增] 直接透传 Key

        public SlotViewModel(int index, double x, double y, double size)
        {
            SlotIndex = index;
            X = x;
            Y = y;
            Size = size;
        }

        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        public double Size
        {
            get => _size;
            set => SetProperty(ref _size, value);
        }

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public int SlotIndex
        {
            get => _slotIndex;
            set => SetProperty(ref _slotIndex, value);
        }

        // [New] 仅仅是数据持有者，不再负责解析逻辑
        public string IconKey
        {
            get => _iconKey;
            set => SetProperty(ref _iconKey, value);
        }

        // 兼容旧代码调用，但逻辑已清空，改为直接赋值
        public void LoadIconData(string iconKey)
        {
            IconKey = iconKey;
        }

        // [New] 是否为上下文推荐项
        [ObservableProperty]
        private bool _isRecommended;

    }
}