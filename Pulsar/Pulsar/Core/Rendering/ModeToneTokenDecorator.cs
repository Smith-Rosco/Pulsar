using System.Windows.Media;
using Pulsar.Models.Enums;

namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// Mode-aware token decorator: keeps the Task/Action cool-vs-warm tone flowing
    /// through the typed token seam. Task (Window Switcher) selects a cool accent
    /// (Blue/Cyan), Action (Command Toolbox) selects a warm one (Orange/Red). The
    /// <see cref="ActiveGlow"/> is deliberately NOT overridden — it delegates to the
    /// underlying theme/preset so the default look (e.g. Light's transparent glow)
    /// is preserved exactly; the mode tone lives on the accent/label surfaces.
    /// Because the decorator sits *above* the preset resolution and the renderer
    /// consumes the already-decorated tokens, changing renderer or preset can never
    /// drop the mode tone (visual-identity contract).
    /// </summary>
    public sealed class ModeToneTokenDecorator : IRadialThemeTokens
    {
        // Cool (Task): blue accent.
        private static readonly Brush CoolAccent = Frozen(Color.FromArgb(0xFF, 0x00, 0x78, 0xD7));
        private static readonly Brush CoolAccentHover = Frozen(Color.FromArgb(0xFF, 0x10, 0x84, 0xE3));

        // Warm (Action): red/orange accent.
        private static readonly Brush WarmAccent = Frozen(Color.FromArgb(0xFF, 0xC0, 0x41, 0x00));
        private static readonly Brush WarmAccentHover = Frozen(Color.FromArgb(0xFF, 0xFF, 0x9A, 0x3D));

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private readonly IRadialThemeTokens _inner;
        private readonly RadialMenuMode _mode;

        public ModeToneTokenDecorator(IRadialThemeTokens inner, RadialMenuMode mode)
        {
            _inner = inner;
            _mode = mode;
        }

        public Brush OrbFill => _inner.OrbFill;
        public Brush OrbStroke => _inner.OrbStroke;
        public Brush OrbText => _inner.OrbText;

        // Glow delegates to the theme/preset so default visuals are unchanged.
        public Brush ActiveGlow => _inner.ActiveGlow;

        public Brush LabelBackground => _inner.LabelBackground;
        public Brush LabelForeground => _inner.LabelForeground;

        public Brush Accent => _mode == RadialMenuMode.Task ? CoolAccent : WarmAccent;
        public Brush AccentHover => _mode == RadialMenuMode.Task ? CoolAccentHover : WarmAccentHover;
        public Brush AccentForeground => _inner.AccentForeground;

        public Brush RadialTitleForeground => _inner.RadialTitleForeground;
        public Brush RadialTitleShadow => _inner.RadialTitleShadow;
        public Brush RadialTitleScrim => _inner.RadialTitleScrim;
    }
}
