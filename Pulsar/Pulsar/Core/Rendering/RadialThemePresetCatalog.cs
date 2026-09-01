using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// Static catalog of named radial theme presets. Hex token sets are ported from
    /// the StarPie reference (<c>BaseStyleRenderer.Initialize</c>) and mapped onto the
    /// Pulsar token model (orb fill/stroke/text, glow, label, accent, radial title).
    /// Presets are opt-in only; the default configuration stays on the built-in
    /// Dark/Light token sets.
    /// </summary>
    public static class RadialThemePresetCatalog
    {
        public const string MatchaForest = "MatchaForest";
        public const string GlacialIce = "GlacialIce";
        public const string MorandiMuted = "MorandiMuted";

        private static readonly IReadOnlyDictionary<string, IRadialThemeTokens> _presets =
            new Dictionary<string, IRadialThemeTokens>(StringComparer.OrdinalIgnoreCase)
            {
                [MatchaForest] = Build(
                    "E6142E1F", "4034D399", "FF10B981", "FF6EE7B7", "FFF0FDF4"),
                [GlacialIce] = Build(
                    "E0E0F2FE", "6038BDF8", "FF0284C7", "FFBAE6FD", "FF0C4A6E"),
                [MorandiMuted] = Build(
                    "E62C302E", "409CA3AF", "FF78716C", "FFD6D3D1", "FFF5F5F4"),
            };

        /// <summary>
        /// Case-insensitive lookup. Returns false for unknown ids so the resolver can
        /// fall back safely instead of throwing.
        /// </summary>
        public static bool TryGet(string id, out IRadialThemeTokens tokens)
        {
            return _presets.TryGetValue(id, out tokens!);
        }

        public static IEnumerable<string> Ids => _presets.Keys;

        private static IRadialThemeTokens Build(string sectorBgHex, string sectorBorderHex, string highlightBgHex, string highlightBorderHex, string textHex)
        {
            var orbFill = Brush(sectorBgHex);
            var orbStroke = Brush(sectorBorderHex);
            var orbText = Brush(textHex);
            var activeGlow = Brush(highlightBgHex);
            var accent = Brush(highlightBgHex);
            var accentHover = Brush(highlightBorderHex);

            return new RadialThemeTokenSet(
                orbFill: orbFill,
                orbStroke: orbStroke,
                orbText: orbText,
                activeGlow: activeGlow,
                labelBackground: Brush("802C302E"),
                labelForeground: orbText,
                accent: accent,
                accentHover: accentHover,
                accentForeground: orbText,
                radialTitleForeground: orbText,
                radialTitleShadow: Brush("B2000000"),
                radialTitleScrim: Brush("66000000"));
        }

        private static Brush Brush(string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString("#" + hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
