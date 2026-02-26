using System;
using System;
using System.Windows;
using Pulsar.Models;
using Wpf.Ui.Controls;

namespace Pulsar.Services.Interfaces
{
    public interface IThemeService
    {
        AppTheme CurrentTheme { get; }
        event EventHandler<AppTheme> ThemeChanged;
        void ApplyTheme(FrameworkElement element, AppTheme theme, WindowBackdropType backdrop = WindowBackdropType.None, bool updateGlobal = true);
        void EnforceTransparency(Window window);
    }
}
