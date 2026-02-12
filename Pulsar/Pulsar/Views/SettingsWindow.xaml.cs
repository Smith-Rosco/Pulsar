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
using System.Windows.Input;
using Pulsar.Native;

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

            // [Fix] Apply theme explicitly to pages to fix inheritance issues
            _themeService.ApplyTheme(_generalPage, _viewModel.SettingsTheme, updateGlobal: false);
            _themeService.ApplyTheme(_slotsPage, _viewModel.SettingsTheme, updateGlobal: false);

            this.Loaded += (s, e) =>
            {
                // [Fix] Respect ViewModel state on load instead of forcing General
                // If Frame is empty, perform initial navigation based on current VM state
                if (RootFrame.Content == null)
                {
                    if (_viewModel.CurrentView == "Slots")
                    {
                        RootFrame.Navigate(_slotsPage);
                        // Ensure visual state matches
                        if (RootNavigation.MenuItems[1] is NavigationViewItem navItem) navItem.IsActive = true;
                    }
                    else
                    {
                        RootFrame.Navigate(_generalPage);
                         // Ensure visual state matches
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
                 // ViewModel: "Settings", "Slots"
                 // UI Tags: "General", "Slots"
                 
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
                             found = true;
                         }
                         else
                         {
                             // Deactivate others to prevent "double selection" ghosting
                             navItem.IsActive = false;
                         }
                     }
                 }
                 
                 // 2. Iterate Footer Items (if any future pages are there)
                 foreach (var item in RootNavigation.FooterMenuItems)
                 {
                     if (item is NavigationViewItem navItem)
                     {
                         // Currently we only have "Save" which is a button, but good practice
                         if (navItem.Tag?.ToString() == targetTag)
                         {
                             navItem.IsActive = true;
                             found = true;
                         }
                         else
                         {
                             navItem.IsActive = false;
                         }
                     }
                 }

                 if (!found)
                 {
                     // Fallback safety
                     System.Diagnostics.Debug.WriteLine($"[SettingsWindow] Target view '{targetTag}' not found in menu items.");
                 }
             }
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

        // [Fix] Lifecycle Management: Hide instead of Close to prevent memory leaks and keep Singleton alive
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
            
            // [Optimization] Aggressive Memory Trimming
            // This forces the OS to page out unused memory, significantly reducing
            // the "perceived" memory footprint in Task Manager when the window is hidden.
            TrimMemory();
            
            base.OnClosing(e);
        }

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