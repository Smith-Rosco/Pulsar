// [File]: Pulsar/ViewModels/SlotViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace Pulsar.ViewModels
{
    public class SlotViewModel : ObservableObject
    {
        private int _slotIndex;
        private double _x;
        private double _y;
        private double _size;
        private string _label = string.Empty;
        private bool _isActive;

        // [New] 图标系统属性
        private string _iconGlyph = string.Empty;
        private bool _hasIcon;

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

        // [New] 图标字符 (Segoe Fluent Icons)
        public string IconGlyph
        {
            get => _iconGlyph;
            set
            {
                if (SetProperty(ref _iconGlyph, value))
                {
                    HasIcon = !string.IsNullOrEmpty(value);
                }
            }
        }

        // [New] 状态位：用于 UI 触发器切换 Label/Icon
        public bool HasIcon
        {
            get => _hasIcon;
            set => SetProperty(ref _hasIcon, value);
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
    }
}