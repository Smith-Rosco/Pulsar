using System;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using Pulsar.ViewModels;
using Pulsar.Views.Pages;
using Pulsar.Services.Interfaces;
using Pulsar.Models;
using CommunityToolkit.Mvvm.Messaging;
using Pulsar.Core.Messages;

namespace Pulsar.Views
{
    public partial class SettingsWindow : FluentWindow
    {
        private readonly SettingsViewModel _viewModel;
        private readonly IThemeService _themeService;
        
        // Manual Page Cache
        private SettingsGeneralPage? _generalPage;
        private SettingsSlotsPage? _slotsPage;

        public SettingsWindow(SettingsViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _themeService = themeService;
            DataContext = viewModel;

            // Subscribe to theme changes
            _themeService.ThemeChanged += OnThemeChanged;

            // Subscribe to Snackbar messages
            WeakReferenceMessenger.Default.Register<SnackbarMessage>(this, (r, m) =>
            {
                // Ensure UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() => 
                {
                    var snackbar = new Snackbar(MainSnackbarPresenter)
                    {
                         Title = m.Title,
                         Content = m.Content,
                         Appearance = m.Appearance,
                         Icon = new SymbolIcon(m.Icon)
                    };
                    snackbar.Show();
                });
            });

            // [Refactor] Apply Theme immediately to ensure isolation
            _themeService.ApplyTheme(this, _viewModel.SettingsTheme, WindowBackdropType.Mica, updateGlobal: true);

            // Initialize Pages with the shared ViewModel
            _generalPage = new SettingsGeneralPage(viewModel);
            _slotsPage = new SettingsSlotsPage(viewModel);

            // [Fix] Apply theme explicitly to pages to fix inheritance issues
            _themeService.ApplyTheme(_generalPage, _viewModel.SettingsTheme, updateGlobal: false);
            _themeService.ApplyTheme(_slotsPage, _viewModel.SettingsTheme, updateGlobal: false);

            this.Loaded += (s, e) =>
            {
                // Navigate to the first page by default
                RootFrame.Navigate(_generalPage);
                _viewModel.CurrentView = "Settings";
            };
            
            RootNavigation.SelectionChanged += RootNavigation_SelectionChanged;
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            // Re-apply theme to pages when global theme changes
            if (_generalPage != null) _themeService.ApplyTheme(_generalPage, theme, updateGlobal: false);
            if (_slotsPage != null) _themeService.ApplyTheme(_slotsPage, theme, updateGlobal: false);
        }

        private void RootNavigation_SelectionChanged(NavigationView sender, RoutedEventArgs args)
        {
             if (sender.SelectedItem is NavigationViewItem item)
             {
                 if (item.Tag?.ToString() == "General")
                 {
                     RootFrame.Navigate(_generalPage);
                     _viewModel.CurrentView = "Settings";
                 }
                 else if (item.Tag?.ToString() == "Slots")
                 {
                     RootFrame.Navigate(_slotsPage);
                     _viewModel.CurrentView = "Slots";
                 }
             }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveCommand.Execute(null);
        }
    }
}