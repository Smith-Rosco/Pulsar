using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Core.Localization;
using Pulsar.Models;

namespace Pulsar.ViewModels.Settings
{
    public sealed partial class WheelSlotItem : ObservableObject, IDisposable
    {
        private readonly ILocalizationService _loc;

        public WheelSlotItem(PluginSlot? slot, int position, double x, double y, double size, ILocalizationService loc)
        {
            Slot = slot;
            Position = position;
            X = x;
            Y = y;
            Size = size;
            _loc = loc;

            // The wheel preview binds to WheelSlotItem's computed properties, which are
            // snapshots of the underlying slot. Without forwarding slot property changes,
            // live edits (e.g. picking a new icon) never reach the orb.
            if (slot != null)
            {
                slot.PropertyChanged += OnSlotPropertyChanged;
            }
        }

        private void OnSlotPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(PluginSlot.Label):
                    OnPropertyChanged(nameof(Label));
                    OnPropertyChanged(nameof(Tooltip));
                    break;
                case nameof(PluginSlot.IconKey):
                    OnPropertyChanged(nameof(IconKey));
                    break;
                case nameof(PluginSlot.Color):
                    OnPropertyChanged(nameof(ColorHex));
                    break;
            }
        }

        public PluginSlot? Slot { get; }

        public int Position { get; }

        public double X { get; }

        public double Y { get; }

        public double Size { get; }

        /// <summary>
        /// Bounding size that fully contains the orb and its largest ring (Size*1.35 + stroke).
        /// WPF Grids clip children to the cell bounds, so the slot container must be large
        /// enough for the rings instead of letting them overflow a Size×Size box.
        /// </summary>
        public double ContainerSize => Size * 1.5;

        public double ContainerX => X - (ContainerSize - Size) / 2;

        public double ContainerY => Y - (ContainerSize - Size) / 2;

        public bool IsEmpty => Slot == null;

        public string PositionLabel => string.Format(_loc["Settings.Slots.PositionFormat"], Position);

        [ObservableProperty]
        private bool _isHighlighted;

        [ObservableProperty]
        private bool _isDropTarget;

        [ObservableProperty]
        private bool _isDragging;

        public string? Label => Slot?.Label;

        public string? IconKey => Slot?.IconKey;

        public string? ColorHex => Slot?.Color;

        public string? Tooltip => IsEmpty
            ? string.Format(_loc["Settings.Slots.Wheel.EmptySlotTooltipFormat"], Position)
            : string.Format(_loc["Settings.Slots.PositionFormat"], Position) + " — " + Label;

        public void Dispose()
        {
            if (Slot != null)
            {
                Slot.PropertyChanged -= OnSlotPropertyChanged;
            }
        }
    }
}
