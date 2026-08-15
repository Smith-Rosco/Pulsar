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
using Pulsar.Core.Localization;
using Pulsar.Core.Messages;
using Pulsar.Features.Tutorial.Helpers;
using Pulsar.Models;
using Pulsar.Models.Settings;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

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
        private readonly ILocalizationService _localizationService;
        private readonly Dictionary<string, Page> _pages = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, NavigationViewItem> _navItemMap = new(StringComparer.OrdinalIgnoreCase);
        private NavigationViewItem? _previousActiveItem;
        private bool _isClosingProgrammatically;
        private bool _isApplyingSelection;
        private bool _isNavAnimating;

        public NavigationView GetNavigationView() => RootNavigation;

        public SettingsWindow(
            SettingsViewModel viewModel,
            SettingsShellViewModel shellViewModel,
            SettingsPageCatalog pageCatalog,
            SettingsPageFactory pageFactory,
            ISettingsNavigationGuard navigationGuard,
            IThemeService themeService,
            ILogger<SettingsWindow> logger,
            ILocalizationService localizationService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _shellViewModel = shellViewModel;
            _pageCatalog = pageCatalog;
            _pageFactory = pageFactory;
            _themeService = themeService;
            _logger = logger;
            _localizationService = localizationService;

            if (navigationGuard is SettingsNavigationGuard concreteNavigationGuard)
            {
                concreteNavigationGuard.AttachEditor(_viewModel);
            }

            DataContext = viewModel;

            BuildNavigationItems();

            _themeService.ThemeChanged += OnThemeChanged;
            _shellViewModel.PropertyChanged += ShellViewModel_PropertyChanged;
            _localizationService.LanguageChanged += OnLanguageChanged;

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

            // The ViewModel has not loaded Profiles.json yet at this point, so its
            // CurrentTheme is only an in-memory default and must not become the global
            // theme. Use the already-bootstrapped ThemeService value for the first paint;
            // LoadSettings() will reconcile and publish any real difference later.
            _themeService.ApplyTheme(this, _themeService.CurrentTheme, WindowBackdropType.Mica, updateGlobal: false);

            Loaded += OnLoaded;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void BuildNavigationItems()
        {
            RootNavigation.MenuItems.Clear();
            _navItemMap.Clear();

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
                _navItemMap[registration.Id] = item;
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadSettings();
            NavigateToCurrentShellPage();
            DisableScrollViewers(RootNavigation);
            DisableScrollViewers(NavPaneGrid);
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            InitializeNavIndicator();
        }

        private async void ShellViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsShellViewModel.CurrentPageId))
            {
                var oldPageId = _previousActiveItem?.Tag?.ToString();
                var newPageId = _shellViewModel.CurrentPageId;
                // Run indicator animation and page navigation concurrently
                var indicatorTask = AnimateNavIndicatorAsync(oldPageId, newPageId);
                NavigateToCurrentShellPage();
                await indicatorTask;
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
                _themeService.ApplyTheme(page, _themeService.CurrentTheme, updateGlobal: false);
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
                        _previousActiveItem = item;
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

        private const double IndicatorWidth = 3;
        private const double IndicatorHeight = 22;

        private void InitializeNavIndicator()
        {
            var activeItem = _navItemMap.Values.FirstOrDefault(i => i.IsActive);
            if (activeItem == null) return;

            var bounds = GetItemRelativeBounds(activeItem);
            if (bounds.Height <= 0) return;

            var centerY = bounds.Top + (bounds.Height - IndicatorHeight) / 2;
            Canvas.SetLeft(NavIndicator, bounds.Left);
            Canvas.SetTop(NavIndicator, centerY);
            NavIndicator.Height = IndicatorHeight;
            NavIndicator.Width = IndicatorWidth;
            NavIndicator.Visibility = Visibility.Visible;
        }

        private async Task AnimateNavIndicatorAsync(string? oldPageId, string? newPageId)
        {
            if (_isNavAnimating || string.IsNullOrEmpty(newPageId)) return;
            if (!_navItemMap.TryGetValue(newPageId, out var newItem)) return;

            _isNavAnimating = true;

            try
            {
                if (!_navItemMap.TryGetValue(oldPageId ?? string.Empty, out var oldItem))
                    oldItem = _previousActiveItem;
                oldItem ??= newItem;

                NavIndicator.Visibility = Visibility.Visible;

                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                await Task.Delay(16);

                var oldBounds = GetItemRelativeBounds(oldItem);
                var newBounds = GetItemRelativeBounds(newItem);

                if (oldBounds.Height <= 0 || newBounds.Height <= 0) return;

                var oldCenterY = oldBounds.Top + (oldBounds.Height - IndicatorHeight) / 2;
                var newCenterY = newBounds.Top + (newBounds.Height - IndicatorHeight) / 2;
                var oldBottomCenter = oldCenterY + IndicatorHeight;
                var newBottomCenter = newCenterY + IndicatorHeight;

                var stretchTop = Math.Min(oldCenterY, newCenterY);
                var stretchBottom = Math.Max(oldBottomCenter, newBottomCenter);
                var stretchHeight = stretchBottom - stretchTop;

                var stretchDuration = TimeSpan.FromMilliseconds(120);
                var snapDuration = TimeSpan.FromMilliseconds(130);
                var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

                // Phase 1: Stretch
                var stretchTopAnim = new DoubleAnimation(stretchTop, stretchDuration) { EasingFunction = easing };
                var stretchHeightAnim = new DoubleAnimation(stretchHeight, stretchDuration) { EasingFunction = easing };

                stretchTopAnim.FillBehavior = FillBehavior.HoldEnd;
                stretchHeightAnim.FillBehavior = FillBehavior.HoldEnd;

                NavIndicator.BeginAnimation(Canvas.TopProperty, stretchTopAnim);
                NavIndicator.BeginAnimation(FrameworkElement.HeightProperty, stretchHeightAnim);

                await Task.Delay(stretchDuration);

                // Phase 2: Snap to new position
                var snapTopAnim = new DoubleAnimation(newCenterY, snapDuration) { EasingFunction = easing };
                var snapHeightAnim = new DoubleAnimation(IndicatorHeight, snapDuration) { EasingFunction = easing };
                NavIndicator.BeginAnimation(Canvas.TopProperty, snapTopAnim);
                NavIndicator.BeginAnimation(FrameworkElement.HeightProperty, snapHeightAnim);

                await Task.Delay(snapDuration);
            }
            finally
            {
                _isNavAnimating = false;
            }
        }

        private Rect GetItemRelativeBounds(NavigationViewItem item)
        {
            try
            {
                var transform = item.TransformToAncestor(NavPaneGrid);
                var topLeft = transform.Transform(new Point(0, 0));
                if (double.IsNaN(topLeft.X) || double.IsNaN(topLeft.Y)) return Rect.Empty;
                return new Rect(topLeft.X, topLeft.Y,
                    Math.Max(0, item.ActualWidth), Math.Max(0, item.ActualHeight));
            }
            catch
            {
                return Rect.Empty;
            }
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

        private async void OnLanguageChanged(object? sender, string e)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                var activePageId = _shellViewModel.CurrentPageId;
                _previousActiveItem = null;
                BuildNavigationItems();
                ApplySelectedNavigationItem(activePageId);
            });
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            InitializeNavIndicator();
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            var backdrop = this is FluentWindow fw ? fw.WindowBackdropType : WindowBackdropType.None;
            _themeService.ApplyTheme(this, theme, backdrop, updateGlobal: false);
            foreach (var page in _pages.Values)
            {
                _themeService.ApplyTheme(page, theme, updateGlobal: false);
            }
            RefreshNavigationTheme(theme);
        }

        private void RefreshNavigationTheme(AppTheme theme)
        {
            if (!RootNavigation.IsLoaded) return;
            // ponytail: avoid ApplyTheme (triggers resource remove/re-add that causes NaN animations)
            var targetTheme = theme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
            var existing = RootNavigation.Resources.MergedDictionaries.OfType<ThemesDictionary>().FirstOrDefault();
            if (existing != null)
                existing.Theme = targetTheme;
            else
                RootNavigation.Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = targetTheme });
            foreach (var item in _navItemMap.Values)
            {
                item.InvalidateProperty(Control.BackgroundProperty);
                item.InvalidateProperty(Control.ForegroundProperty);
                item.InvalidateProperty(FrameworkElement.StyleProperty);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _themeService.ThemeChanged -= OnThemeChanged;
            _shellViewModel.PropertyChanged -= ShellViewModel_PropertyChanged;
            _localizationService.LanguageChanged -= OnLanguageChanged;

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
