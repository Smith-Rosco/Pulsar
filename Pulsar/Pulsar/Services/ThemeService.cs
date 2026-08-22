using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Linq;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

namespace Pulsar.Services
{
    public class ThemeService : IThemeService
    {
        private readonly ILogger<ThemeService> _logger;

        /// <summary>
        /// Runtime theme. Defaults to Light because first-launch Profiles.json uses Light;
        /// startup must call <see cref="Initialize"/> before creating themed UI so persisted
        /// Dark configurations override this value immediately.
        /// </summary>
        public AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

        public event EventHandler<AppTheme>? ThemeChanged;

        public ThemeService(ILogger<ThemeService> logger)
        {
            _logger = logger;
        }

        public void Initialize(AppTheme theme)
        {
            if (CurrentTheme == theme)
            {
                _logger.LogDebug("[ThemeService] Theme already initialized to {Theme}", theme);
                return;
            }

            CurrentTheme = theme;
            _logger.LogInformation("[ThemeService] Runtime theme initialized from configuration: {Theme}", theme);

            // Normal startup calls this before any subscribers exist. Raising here also keeps
            // the method safe if it is ever used later in the app lifecycle.
            ThemeChanged?.Invoke(this, theme);
        }

        public void SetGlobalTheme(AppTheme theme)
        {
            if (CurrentTheme == theme)
            {
                return;
            }

            CurrentTheme = theme;
            _logger.LogInformation("[ThemeService] Runtime theme changed to {Theme}", theme);
            ThemeChanged?.Invoke(this, theme);
        }

        public void ApplyTheme(FrameworkElement element, AppTheme theme, WindowBackdropType backdrop = WindowBackdropType.None)
        {
            if (element == null) return;
            _logger.LogDebug("[ThemeService] ApplyTheme: Element={Element}, Theme={Theme}", element.GetType().Name, theme);

            // Branch Logic
            if (element is Pulsar.Views.RadialMenuWindow radWin)
            {
                 // Radial uses simple ResourceDictionaries, so we clear and re-add
                 ClearThemeResources(element);
                 ApplyRadialTheme(radWin, theme);
            }
            else
            {
                 // Standard (Settings, Dialogs)
                 // Logic refined: Do NOT blindly clear resources if we are just switching Light/Dark.
                 // ClearThemeResources is destructive and causes "NaN" animation crashes if Wpf.Ui dictionaries are removed.
                 
                 // ApplyStandardTheme now smartly updates existing dictionaries in place.
                 ApplyStandardTheme(element, theme, backdrop);
            }

        }

        private void ApplyRadialTheme(Window window, AppTheme theme)
        {
             string themePath = theme == AppTheme.Light
                ? "pack://application:,,,/Pulsar;component/Themes/Theme.Light.xaml"
                : "pack://application:,,,/Pulsar;component/Themes/Theme.Dark.xaml";

             window.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(themePath, UriKind.Absolute) });
             
             window.Background = System.Windows.Media.Brushes.Transparent;
             
             // Ensure no backdrop interference
             if (window is FluentWindow fw) 
             {
                 fw.WindowBackdropType = WindowBackdropType.None;
             }
        }

        public void ApplyContextMenuTheme(ContextMenu menu, AppTheme theme)
        {
            if (menu == null)
            {
                return;
            }

            var targetTheme = ToApplicationTheme(theme);

            // Update in place when possible. ContextMenus are popup visual trees that do not
            // inherit window resources, so both WPF-UI dictionaries must be present locally.
            var existingThemeDict = menu.Resources.MergedDictionaries.OfType<ThemesDictionary>().FirstOrDefault();
            if (existingThemeDict != null)
            {
                existingThemeDict.Theme = targetTheme;
            }
            else
            {
                menu.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = targetTheme });
            }

            if (!menu.Resources.MergedDictionaries.OfType<ControlsDictionary>().Any())
            {
                menu.Resources.MergedDictionaries.Add(new ControlsDictionary());
            }
        }

        private void ApplyStandardTheme(FrameworkElement element, AppTheme theme, WindowBackdropType backdrop)
        {
            var targetTheme = ToApplicationTheme(theme);

            // 1. Try to update existing ThemesDictionary to avoid "NaN" animation crashes
            var existingThemeDict = element.Resources.MergedDictionaries.OfType<ThemesDictionary>().FirstOrDefault();
            if (existingThemeDict != null)
            {
                existingThemeDict.Theme = targetTheme;
            }
            else
            {
                var newThemeDict = new ThemesDictionary { Theme = targetTheme };
                element.Resources.MergedDictionaries.Add(newThemeDict);
            }

            // 2. Ensure ControlsDictionary exists
            if (!element.Resources.MergedDictionaries.OfType<ControlsDictionary>().Any())
            {
                element.Resources.MergedDictionaries.Add(new ControlsDictionary());
            }

            // 3. Inject Pulsar Theme Resources (Theme.Dark/Light.xaml)
            // This ensures our custom keys (Theme.Orb.*, Theme.Accent.*) are available in Standard windows too.
            string pulsarThemePath = theme == AppTheme.Light
                ? "pack://application:,,,/Pulsar;component/Themes/Theme.Light.xaml"
                : "pack://application:,,,/Pulsar;component/Themes/Theme.Dark.xaml";

            // Remove existing Pulsar theme if present to avoid duplicates/conflicts
            var existingPulsarTheme = element.Resources.MergedDictionaries.FirstOrDefault(d => 
                d.Source != null && d.Source.ToString().Contains("/Themes/Theme."));
            
            if (existingPulsarTheme != null)
            {
                element.Resources.MergedDictionaries.Remove(existingPulsarTheme);
            }

            element.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(pulsarThemePath, UriKind.Absolute) });

            if (element is FluentWindow fw)
            {
                fw.WindowBackdropType = backdrop;
            }
        }

        private static ApplicationTheme ToApplicationTheme(AppTheme theme)
        {
            return theme == AppTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark;
        }

        private void ClearThemeResources(FrameworkElement element)
        {
            // Only clear if we really need to (e.g. switching from Standard to Radial)
            for (int i = element.Resources.MergedDictionaries.Count - 1; i >= 0; i--)
            {
                var dict = element.Resources.MergedDictionaries[i];
                
                // Remove Pulsar Themes (Radial)
                if (dict.Source != null && dict.Source.ToString().Contains("/Themes/Theme."))
                {
                    element.Resources.MergedDictionaries.RemoveAt(i);
                    continue;
                }

                // Remove WPF-UI Dictionaries (Standard)
                // We typically update them in place, but if we are forcing a clear:
                if (dict is ThemesDictionary || dict is ControlsDictionary)
                {
                    element.Resources.MergedDictionaries.RemoveAt(i);
                    continue;
                }
            }
        }

        public void EnforceTransparency(Window window)
        {
            if (window == null) return;
            
            if (window.Background != System.Windows.Media.Brushes.Transparent)
            {
                window.Background = System.Windows.Media.Brushes.Transparent;
            }
             if (window.WindowStyle != WindowStyle.None)
            {
                window.WindowStyle = WindowStyle.None;
            }
            
            // Remove DWM Backdrop
            var interop = new System.Windows.Interop.WindowInteropHelper(window);
            var hwnd = interop.Handle;
            if (hwnd != IntPtr.Zero)
            {
            int backdropType = PulsarNative.DWMSBT_NONE;
            PulsarNative.DwmSetWindowAttribute(
                    hwnd, 
                    PulsarNative.DWMWA_SYSTEMBACKDROP_TYPE,
                    ref backdropType, 
                    sizeof(int));
            }
        }
    }
}
