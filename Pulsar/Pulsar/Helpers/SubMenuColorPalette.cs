using System.Windows.Media;
using Pulsar.ViewModels;

namespace Pulsar.Helpers
{
    internal static class SubMenuColorPalette
    {
        private static readonly string[] HexColors =
        [
            "#FF6B6B", "#4ECDC4", "#FFD93D", "#6C5CE7",
            "#A8E6CF", "#FF8A5C", "#45B7D1", "#F78FB3"
        ];

        internal static void Apply(SlotViewModel slot, int sortIndex)
        {
            if (sortIndex < 0) return;
            var hex = HexColors[sortIndex % HexColors.Length];
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color) { Opacity = 0.9 };
            brush.Freeze();
            slot.CustomStrokeBrush = brush;
        }

        internal static void Clear(SlotViewModel slot)
        {
            slot.CustomStrokeBrush = null;
        }
    }
}
