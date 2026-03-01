using System;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Settings; // Added
using Pulsar.Views.Pages;
using Pulsar.Services.Interfaces;
using Pulsar.Models;
using Microsoft.Extensions.Logging;

using CommunityToolkit.Mvvm.Messaging;
using Pulsar.Core.Messages;
using System.Windows.Input;
using Pulsar.Native;

namespace Pulsar.Views
{
    public partial class SettingsWindow : FluentWindow
    {
        private readonly SettingsViewModel _viewModel;
        private readonly PluginManagerViewModel _pluginManager; // [New]
        private readonly IThemeService _themeService;
        private readonly ILogger<SettingsWindow> _logger;
        
        // Manual Page Cache
        private SettingsGeneralPage? _generalPage;
        private SettingsSlotsPage? _slotsPage;
        private SettingsPluginsPage? _pluginsPage; // [New]
        private SettingsAboutPage? _aboutPage; // [New]

        public SettingsWindow(
            SettingsViewModel viewModel,
            PluginManagerViewModel pluginManager,
            IThemeService themeService,
            ILogger<SettingsWindow> logger)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _pluginManager = pluginManager;
            _themeService = themeService;
            _logger = logger;
            DataContext = viewModel;

            // Subscribe to theme changes
            _themeService.ThemeChanged += OnThemeChanged;

            // Subscribe to ViewModel changes for Navigation
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;


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
            _pluginsPage = new SettingsPluginsPage(_pluginManager, _themeService); // [New]
            _aboutPage = new SettingsAboutPage(new AboutViewModel()); // [New]

            // [Fix] Apply theme explicitly to pages to fix inheritance issues
            _themeService.ApplyTheme(_generalPage, _viewModel.SettingsTheme, updateGlobal: false);
            _themeService.ApplyTheme(_slotsPage, _viewModel.SettingsTheme, updateGlobal: false);
            _themeService.ApplyTheme(_pluginsPage, _viewModel.SettingsTheme, updateGlobal: false);
            _themeService.ApplyTheme(_aboutPage, _viewModel.SettingsTheme, updateGlobal: false); // [New]

            this.Loaded += (s, e) =>
            {
                // [Fix] Respect ViewModel state on load instead of forcing General
                // If Frame is empty, perform initial navigation based on current VM state
                if (RootFrame.Content == null)
                {
                    if (_viewModel.CurrentView == "Slots")
                    {
                        RootFrame.Navigate(_slotsPage);
                        if (RootNavigation.MenuItems[1] is NavigationViewItem navItem) navItem.IsActive = true;
                    }
                    else if (_viewModel.CurrentView == "Plugins")
                    {
                        RootFrame.Navigate(_pluginsPage);
                        if (RootNavigation.MenuItems[2] is NavigationViewItem navItem) navItem.IsActive = true;
                    }
                    else if (_viewModel.CurrentView == "About")
                    {
                        RootFrame.Navigate(_aboutPage);
                        if (RootNavigation.MenuItems[3] is NavigationViewItem navItem) navItem.IsActive = true;
                    }
                    else
                    {
                        RootFrame.Navigate(_generalPage);
                        if (RootNavigation.MenuItems[0] is NavigationViewItem navItem) navItem.IsActive = true;
                    }
                }

                // [Fix] Force hide scrollbars in NavigationView using VisualTreeHelper

                DisableScrollViewers(RootNavigation);
            };
            
            RootNavigation.SelectionChanged += RootNavigation_SelectionChanged;
        }

        private void DisableScrollViewers(DependencyObject depObj)
        {
            if (depObj == null) return;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child is ScrollViewer scrollViewer)
                {
                    scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                    scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                }
                DisableScrollViewers(child);
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
             if (e.PropertyName == nameof(SettingsViewModel.CurrentView))
             {
                 // [Fix] Robust Navigation State Synchronization
                 // Map ViewModel ViewName to UI Tag
                 // ViewModel: "Settings", "Slots", "Plugins", "About"
                 // UI Tags: "General", "Slots", "Plugins", "About"
                 
                 string targetTag = _viewModel.CurrentView;
                 if (targetTag == "Settings") targetTag = "General";

                 bool found = false;

                 // 1. Iterate Main Menu Items
                 foreach (var item in RootNavigation.MenuItems)
                 {
                     if (item is NavigationViewItem navItem)
                     {
                         bool isMatch = navItem.Tag?.ToString() == targetTag;
                         if (isMatch)
                         {
                             // Activate visual state
                             navItem.IsActive = true; 
                             // Perform actual navigation
                             if (targetTag == "General") RootFrame.Navigate(_generalPage);
                             else if (targetTag == "Slots") RootFrame.Navigate(_slotsPage);
                             else if (targetTag == "Plugins") RootFrame.Navigate(_pluginsPage);
                             else if (targetTag == "About") RootFrame.Navigate(_aboutPage);
                             found = true;
                         }

                         else
                         {
                             // Deactivate others to prevent "double selection" ghosting
                             navItem.IsActive = false;
                         }
                     }
                 }
                 
                   if (!found)
                  {
                      // Fallback safety
                      _logger.LogWarning("[SettingsWindow] Target view '{TargetTag}' not found in menu items.", targetTag);
                  }
              }
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            // Re-apply theme to pages when global theme changes
            if (_generalPage != null) _themeService.ApplyTheme(_generalPage, theme, updateGlobal: false);
            if (_slotsPage != null) _themeService.ApplyTheme(_slotsPage, theme, updateGlobal: false);
            if (_pluginsPage != null) _themeService.ApplyTheme(_pluginsPage, theme, updateGlobal: false); // [New]
            if (_aboutPage != null) _themeService.ApplyTheme(_aboutPage, theme, updateGlobal: false); // [New]
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
                 else if (item.Tag?.ToString() == "Plugins")
                 {
                     RootFrame.Navigate(_pluginsPage);
                     _viewModel.CurrentView = "Plugins";
                 }
                 else if (item.Tag?.ToString() == "About")
                 {
                     RootFrame.Navigate(_aboutPage);
                     _viewModel.CurrentView = "About";
                 }
             }
        }

        // [Fix] Lifecycle Management: Allow Close to reset state (Transient behavior)
        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe from events to prevent memory leaks in Singleton services
            if (_themeService != null)
            {
                _themeService.ThemeChanged -= OnThemeChanged;
            }
            
            // Clean up resources
            _generalPage = null;
            _slotsPage = null;
            _pluginsPage = null;
            _aboutPage = null;
            
            // Trigger GC to clean up the Transient ViewModel and Pages
            TrimMemory();
            
            base.OnClosed(e);
        }

        // Removed OnClosing override that forced Hide()
        // protected override void OnClosing(System.ComponentModel.CancelEventArgs e) ...

        private void TrimMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
                // -1, -1 tells the OS to swap out the process memory to disk
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    WindowHelper.SetProcessWorkingSetSize(WindowHelper.GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
                }
            }
            catch 
            {
                // Optimization only - ignore failures
            }
        }
    }
}
