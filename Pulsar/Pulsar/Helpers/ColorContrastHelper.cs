using System;
using System.Windows.Media;

namespace Pulsar.Helpers
{
    /// <summary>
    /// WCAG-based contrast helpers for slot orb foreground colors.
    /// Slot fills are translucent, so readability is evaluated against the
    /// worst-case desktop backdrops (black and white) rather than only the
    /// raw custom color.
    /// </summary>
    public static class ColorContrastHelper
    {
        private static readonly Color NearBlack = Color.FromRgb(0x1A, 0x1A, 0x1A);

        /// <summary>
        /// Picks white or near-black, whichever keeps the higher minimum WCAG
        /// contrast ratio when the translucent source color is composited over
        /// both black and white backdrops.
        /// </summary>
        public static Color PickForegroundColor(Color source, double sourceOpacity = 0.25)
        {
            double opacity = Math.Clamp(sourceOpacity, 0.0, 1.0);

            Color overBlack = Blend(source, Colors.Black, opacity);
            Color overWhite = Blend(source, Colors.White, opacity);

            double whiteMinContrast = Math.Min(
                ContrastRatio(Colors.White, overBlack),
                ContrastRatio(Colors.White, overWhite));
            double blackMinContrast = Math.Min(
                ContrastRatio(NearBlack, overBlack),
                ContrastRatio(NearBlack, overWhite));

            return whiteMinContrast >= blackMinContrast ? Colors.White : NearBlack;
        }

        public static double ContrastRatio(Color first, Color second)
        {
            double firstLuminance = RelativeLuminance(first);
            double secondLuminance = RelativeLuminance(second);

            double lighter = Math.Max(firstLuminance, secondLuminance);
            double darker = Math.Min(firstLuminance, secondLuminance);

            return (lighter + 0.05) / (darker + 0.05);
        }

        public static double RelativeLuminance(Color color)
        {
            double red = LinearizeChannel(color.R / 255.0);
            double green = LinearizeChannel(color.G / 255.0);
            double blue = LinearizeChannel(color.B / 255.0);

            return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
        }

        private static Color Blend(Color source, Color backdrop, double sourceOpacity)
        {
            double inverseOpacity = 1.0 - sourceOpacity;

            byte red = ToByte(source.R * sourceOpacity + backdrop.R * inverseOpacity);
            byte green = ToByte(source.G * sourceOpacity + backdrop.G * inverseOpacity);
            byte blue = ToByte(source.B * sourceOpacity + backdrop.B * inverseOpacity);

            return Color.FromRgb(red, green, blue);
        }

        private static double LinearizeChannel(double value)
        {
            return value <= 0.04045
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        private static byte ToByte(double value)
        {
            return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
        }
    }
}
