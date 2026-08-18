using System;
using System.Windows;
using System.Windows.Controls;
using Pulsar.Models;
using Wpf.Ui.Controls;

namespace Pulsar.Services.Interfaces
{
    public interface IThemeService
    {
        AppTheme CurrentTheme { get; }

        event EventHandler<AppTheme> ThemeChanged;

        /// <summary>
        /// Establishes the runtime theme from persisted configuration before any themed UI
        /// (tray context menu, windows, pages) is created. This keeps <see cref="CurrentTheme"/>
        /// consistent with Profiles.json from the very first rendered element.
        /// </summary>
        void Initialize(AppTheme theme);

        /// <summary>
        /// Changes the runtime theme and notifies subscribers. Painting individual
        /// elements remains the responsibility of <see cref="ApplyTheme"/>.
        /// </summary>
        void SetGlobalTheme(AppTheme theme);

        /// <summary>
        /// Applies WPF-UI theme dictionaries to a ContextMenu. ContextMenus render in a
        /// separate visual tree and do not inherit window/page resources.
        /// </summary>
        void ApplyContextMenuTheme(ContextMenu menu, AppTheme theme);

        void ApplyTheme(FrameworkElement element, AppTheme theme, WindowBackdropType backdrop = WindowBackdropType.None);
        void EnforceTransparency(Window window);
    }
}
