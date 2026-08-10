using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Models;

namespace Pulsar.ViewModels.Settings
{
    public sealed partial class WheelSlotItem : ObservableObject
    {
        public WheelSlotItem(PluginSlot? slot, int position, double x, double y, double size)
        {
            Slot = slot;
            Position = position;
            X = x;
            Y = y;
            Size = size;
        }

        public PluginSlot? Slot { get; }

        public int Position { get; }

        public double X { get; }

        public double Y { get; }

        public double Size { get; }

        public bool IsEmpty => Slot == null;

        [ObservableProperty]
        private bool _isHighlighted;

        [ObservableProperty]
        private bool _isDropTarget;

        [ObservableProperty]
        private bool _isDragging;

        public string? Label => Slot?.Label;

        public string? IconKey => Slot?.IconKey;

        public string? ColorHex => Slot?.Color;

        public string? Tooltip => IsEmpty ? null : $"#{Slot?.Slot} {Label}";
    }
}
