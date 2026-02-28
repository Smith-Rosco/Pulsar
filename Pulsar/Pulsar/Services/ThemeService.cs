using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Linq;
using Microsoft.Extensions.Logging;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

namespace Pulsar.Services
{
    public class ThemeService : IThemeService
    {
        private readonly ILogger<ThemeService> _logger;

        public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

        public event EventHandler<AppTheme>? ThemeChanged;

        public ThemeService(ILogger<ThemeService> logger)
        {
            _logger = logger;
        }

        public void ApplyTheme(FrameworkElement element, AppTheme theme, WindowBackdropType backdrop = WindowBackdropType.None, bool updateGlobal = true)
        {
            if (updateGlobal)
            {
                CurrentTheme = theme;
            }

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

            // 3. Notify subscribers
            if (updateGlobal)
            {
                ThemeChanged?.Invoke(this, theme);
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

        private void ApplyStandardTheme(FrameworkElement element, AppTheme theme, WindowBackdropType backdrop)
        {
            var targetTheme = theme == AppTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark;

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
                int backdropType = Pulsar.Native.WindowHelper.DWMSBT_NONE;
                Pulsar.Native.WindowHelper.DwmSetWindowAttribute(
                    hwnd, 
                    Pulsar.Native.WindowHelper.DWMWA_SYSTEMBACKDROP_TYPE, 
                    ref backdropType, 
                    sizeof(int));
            }
        }
    }
}
