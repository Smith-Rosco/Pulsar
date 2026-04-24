using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Messages;
using Pulsar.Helpers.Tutorial;
using Pulsar.Models;
using Pulsar.Models.Settings;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Settings;
using Wpf.Ui.Controls;

namespace Pulsar.Views
{
    public partial class SettingsWindow : FluentWindow
    {
        private readonly SettingsViewModel _viewModel;
        private readonly SettingsShellViewModel _shellViewModel;
        private readonly SettingsPageCatalog _pageCatalog;
        private readonly SettingsPageFactory _pageFactory;
        private readonly IThemeService _themeService;
        private readonly ILogger<SettingsWindow> _logger;
        private readonly Dictionary<string, Page> _pages = new(StringComparer.OrdinalIgnoreCase);
        private bool _isClosingProgrammatically;
        private bool _isApplyingSelection;

        public NavigationView GetNavigationView() => RootNavigation;

        public SettingsWindow(
            SettingsViewModel viewModel,
            SettingsShellViewModel shellViewModel,
            SettingsPageCatalog pageCatalog,
            SettingsPageFactory pageFactory,
            ISettingsNavigationGuard navigationGuard,
            IThemeService themeService,
            ILogger<SettingsWindow> logger)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _shellViewModel = shellViewModel;
            _pageCatalog = pageCatalog;
            _pageFactory = pageFactory;
            _themeService = themeService;
            _logger = logger;

            if (navigationGuard is SettingsNavigationGuard concreteNavigationGuard)
            {
                concreteNavigationGuard.AttachEditor(_viewModel);
            }

            DataContext = viewModel;

            BuildNavigationItems();

            _themeService.ThemeChanged += OnThemeChanged;
            _shellViewModel.PropertyChanged += ShellViewModel_PropertyChanged;

            WeakReferenceMessenger.Default.Register<SnackbarMessage>(this, (r, m) =>
            {
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

            _themeService.ApplyTheme(this, _viewModel.SettingsTheme, WindowBackdropType.Mica, updateGlobal: true);

            Loaded += OnLoaded;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void BuildNavigationItems()
        {
            RootNavigation.MenuItems.Clear();

            foreach (var registration in _pageCatalog.Pages)
            {
                var item = new NavigationViewItem
                {
                    Content = registration.Title,
                    Tag = registration.Id,
                    Icon = new SymbolIcon(registration.Icon)
                };

                item.PreviewMouseLeftButtonUp += NavigationItem_PreviewMouseLeftButtonUp;
                item.KeyUp += NavigationItem_KeyUp;

                if (!string.IsNullOrWhiteSpace(registration.TutorialMarkerId))
                {
                    TutorialMarker.SetId(item, registration.TutorialMarkerId);
                }

                RootNavigation.MenuItems.Add(item);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            NavigateToCurrentShellPage();
            DisableScrollViewers(RootNavigation);
        }

        private void ShellViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsShellViewModel.CurrentPageId))
            {
                NavigateToCurrentShellPage();
            }
        }

        private void NavigateToCurrentShellPage()
        {
            var pageId = _shellViewModel.CurrentPageId;
            if (!_pageCatalog.TryGetRegistration(pageId, out var registration))
            {
                _logger.LogWarning("[SettingsWindow] No registration found for shell page '{PageId}'", pageId);
                return;
            }

            if (!_pages.TryGetValue(registration.Id, out var page))
            {
                page = _pageFactory.CreatePage(registration.Id, _viewModel);
                _pages[registration.Id] = page;
                _themeService.ApplyTheme(page, _viewModel.SettingsTheme, updateGlobal: false);
            }

            NavigateWithAnimation(page);
            ApplySelectedNavigationItem(registration.Id);
        }

        private void ApplySelectedNavigationItem(string pageId)
        {
            _isApplyingSelection = true;
            try
            {
                foreach (var item in RootNavigation.MenuItems.OfType<NavigationViewItem>())
                {
                    item.IsActive = string.Equals(item.Tag?.ToString(), pageId, StringComparison.OrdinalIgnoreCase);
                    if (item.IsActive)
                    {
                    }
                }
            }
            finally
            {
                _isApplyingSelection = false;
            }
        }

        private void NavigateWithAnimation(Page page)
        {
            RootFrame.Navigate(page);

            page.Opacity = 0;
            page.RenderTransform = new TranslateTransform(0, 20);
            page.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

            var duration = new Duration(TimeSpan.FromMilliseconds(250));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var fadeIn = new DoubleAnimation(0, 1, duration);
            var slideUp = new DoubleAnimation(20, 0, duration) { EasingFunction = ease };

            page.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            ((TranslateTransform)page.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slideUp);
        }

        private void DisableScrollViewers(DependencyObject depObj)
        {
            if (depObj == null)
            {
                return;
            }

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is ScrollViewer scrollViewer)
                {
                    scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                    scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                }

                DisableScrollViewers(child);
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control && _viewModel.SaveCommand.CanExecute(null))
            {
                _viewModel.SaveCommand.Execute(null);
                e.Handled = true;
            }
        }

        private async void NavigationItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isApplyingSelection)
            {
                return;
            }

            if (sender is NavigationViewItem item)
            {
                await _shellViewModel.NavigateAsync(item.Tag?.ToString(), userInitiated: true);
            }
        }

        private async void NavigationItem_KeyUp(object sender, KeyEventArgs e)
        {
            if (_isApplyingSelection)
            {
                return;
            }

            if (e.Key != Key.Enter && e.Key != Key.Space)
            {
                return;
            }

            if (sender is NavigationViewItem item)
            {
                await _shellViewModel.NavigateAsync(item.Tag?.ToString(), userInitiated: true);
                e.Handled = true;
            }
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            foreach (var page in _pages.Values)
            {
                _themeService.ApplyTheme(page, theme, updateGlobal: false);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _themeService.ThemeChanged -= OnThemeChanged;
            _shellViewModel.PropertyChanged -= ShellViewModel_PropertyChanged;

            foreach (var item in RootNavigation.MenuItems.OfType<NavigationViewItem>())
            {
                item.PreviewMouseLeftButtonUp -= NavigationItem_PreviewMouseLeftButtonUp;
                item.KeyUp -= NavigationItem_KeyUp;
            }

            _pages.Clear();
            TrimMemory();
            base.OnClosed(e);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_isClosingProgrammatically)
            {
                base.OnClosing(e);
                return;
            }

            e.Cancel = true;
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (await _shellViewModel.CanCloseAsync())
                {
                    _isClosingProgrammatically = true;
                    Close();
                }
            });
        }

        private void TrimMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();

                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    PulsarNative.SetProcessWorkingSetSize(PulsarNative.GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
                }
            }
            catch
            {
            }
        }
    }
}
