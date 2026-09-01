using System.Windows.Media;

namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// Typed projection of the radial menu's theme brushes. Renderers and preset
    /// resolution consume this contract instead of raw resource-key strings, so a
    /// future theme preset or plugin renderer never has to reference "Theme.Orb.*".
    /// </summary>
    public interface IRadialThemeTokens
    {
        Brush OrbFill { get; }
        Brush OrbStroke { get; }
        Brush OrbText { get; }
        Brush ActiveGlow { get; }
        Brush LabelBackground { get; }
        Brush LabelForeground { get; }
        Brush Accent { get; }
        Brush AccentHover { get; }
        Brush AccentForeground { get; }
        Brush RadialTitleForeground { get; }
        Brush RadialTitleShadow { get; }
        Brush RadialTitleScrim { get; }
    }
}
