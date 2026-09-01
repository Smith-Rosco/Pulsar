using System;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Wpf.Ui.Appearance;

namespace Pulsar.Core.Rendering
{
    /// <summary>
    /// Resolves a configured theme/preset value (<c>System</c> / <c>Dark</c> /
    /// <c>Light</c> / named preset) to a concrete token set, with a safe fallback when
    /// the value is unknown. Mirrors StarPie's <c>BaseStyleRenderer.Initialize</c>
    /// layering adapted to Pulsar's two built-in dictionaries.
    /// </summary>
    public class RadialThemePresetResolver
    {
        private readonly ILogger<RadialThemePresetResolver>? _logger;
        private readonly Func<AppTheme> _systemThemeProvider;
        private readonly Func<AppTheme, IRadialThemeTokens> _builtInFactory;

        public RadialThemePresetResolver(
            ILogger<RadialThemePresetResolver>? logger = null,
            Func<AppTheme>? systemThemeProvider = null,
            Func<AppTheme, IRadialThemeTokens>? builtInFactory = null)
        {
            _logger = logger;
            _systemThemeProvider = systemThemeProvider ?? ResolveSystemTheme;
            _builtInFactory = builtInFactory ?? RadialThemeTokenSet.FromTheme;
        }

        /// <summary>
        /// Resolves the configured preset to a token set.
        /// </summary>
        /// <param name="preset">Configured value: "System", "Dark", "Light", or a named
        /// preset id (case-insensitive). Null/empty is treated as System.</param>
        /// <param name="activeTheme">The currently active app theme, used only as the
        /// fallback for unknown values.</param>
        public IRadialThemeTokens Resolve(string? preset, AppTheme activeTheme)
        {
            var value = string.IsNullOrWhiteSpace(preset) ? "System" : preset.Trim();

            if (string.Equals(value, "System", StringComparison.OrdinalIgnoreCase))
            {
                return _builtInFactory(_systemThemeProvider());
            }

            if (string.Equals(value, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                return _builtInFactory(AppTheme.Dark);
            }

            if (string.Equals(value, "Light", StringComparison.OrdinalIgnoreCase))
            {
                return _builtInFactory(AppTheme.Light);
            }

            if (RadialThemePresetCatalog.TryGet(value, out var presetTokens))
            {
                return presetTokens;
            }

            // Unknown → fall back to the active theme default, never throw.
            _logger?.LogWarning(
                "[RadialThemePreset] Unknown preset '{Preset}' — falling back to active theme {Theme}",
                value, activeTheme);
            return _builtInFactory(activeTheme);
        }

        private static AppTheme ResolveSystemTheme()
        {
            return ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark
                ? AppTheme.Dark
                : AppTheme.Light;
        }
    }
}
