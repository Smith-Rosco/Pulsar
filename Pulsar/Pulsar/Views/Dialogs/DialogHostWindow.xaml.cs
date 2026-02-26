using System;
using System.Windows;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.Models.Enums;
using Wpf.Ui.Controls;

namespace Pulsar.Views.Dialogs
{
    public partial class DialogHostWindow : FluentWindow
    {
        private readonly IThemeService _themeService;

        public DialogHostWindow(IThemeService themeService)
        {
            _themeService = themeService;
            InitializeComponent();
            
            // Apply current theme with Mica backdrop
            // updateGlobal = false because we don't want to change the global theme just by opening a dialog
            _themeService.ApplyTheme(this, _themeService.CurrentTheme, WindowBackdropType.Mica, updateGlobal: false);
            
            // Fix Wpf.Ui scrollbars via reflection or helper if needed
            // Loaded += (s, e) => DisableScrollViewers(this); 
        }

        // Standard close pattern handled by DialogService via ViewModel
    }
}
