// [File]: Pulsar/ViewModels/SlotViewModel.cs

using CommunityToolkit.Mvvm.ComponentModel;

namespace Pulsar.ViewModels
{
    // 确保没有 duplicate 错误，且继承 ObservableObject
    public class SlotViewModel : ObservableObject
    {
        private int _slotIndex;
        private double _x;
        private double _y;
        private double _size;
        private string _label = string.Empty;
        private bool _isActive;

        public SlotViewModel(int index, double x, double y, double size)
        {
            SlotIndex = index;
            X = x;
            Y = y;
            Size = size;
        }

        // 手动实现属性，确保 WPF Binding 绝对能找到它们
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
    }
}