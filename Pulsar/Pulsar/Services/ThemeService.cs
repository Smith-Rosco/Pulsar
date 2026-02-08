using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Linq;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

namespace Pulsar.Services
{
    public class ThemeService : IThemeService
    {
        public event EventHandler<AppTheme> ThemeChanged;

        public void ApplyTheme(FrameworkElement element, AppTheme theme, WindowBackdropType backdrop = WindowBackdropType.None, bool updateGlobal = true)
        {
            if (element == null) return;
            System.Diagnostics.Debug.WriteLine($"[ThemeService] ApplyTheme: Element={element.GetType().Name}, Theme={theme}");

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
                 // If we are switching FROM Radial style (unlikely for same window), clear first
                 bool hasRadial = element.Resources.MergedDictionaries.OfType<ResourceDictionary>().Any(d => d.Source != null && d.Source.ToString().Contains("/Themes/Theme."));
                 if (hasRadial)
                 {
                     ClearThemeResources(element);
                 }

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
