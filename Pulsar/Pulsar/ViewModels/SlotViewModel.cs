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
        private string _iconKey = string.Empty;

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

        public string IconKey
        {
            get => _iconKey;
            set => SetProperty(ref _iconKey, value);
        }

        public void LoadIconData(string iconKey)
        {
            IconKey = iconKey;
        }

        [ObservableProperty]
        private bool _isRecommended;
    }
}