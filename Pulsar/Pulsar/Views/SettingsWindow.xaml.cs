using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Automation;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly ITransientPageService _transientPageService;
        private readonly DataTemplate _transientNavItemTemplate;
        private NavigationViewItem? _previousActiveItem;
        private string? _activePageId;
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
            ITransientPageService transientPageService,
            IThemeService themeService,
            ILogger<SettingsWindow> logger,
            ILocalizationService localizationService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _shellViewModel = shellViewModel;
            _pageCatalog = pageCatalog;
            _pageFactory = pageFactory;
            _transientPageService = transientPageService;
            _themeService = themeService;
            _logger = logger;
            _localizationService = localizationService;
            _transientNavItemTemplate = (DataTemplate)Resources["TransientNavItemContentTemplate"];

            if (navigationGuard is SettingsNavigationGuard concreteNavigationGuard)
            {
                concreteNavigationGuard.AttachEditor(_viewModel);
            }

            DataContext = viewModel;

            BuildNavigationItems();

            _pageCatalog.TransientPageRegistered += OnTransientPageRegistered;
            _pageCatalog.TransientPageUnregistered += OnTransientPageUnregistered;
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
            _themeService.ApplyTheme(this, _themeService.CurrentTheme, WindowBackdropType.Mica);

            Loaded += OnLoaded;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private void BuildNavigationItems()
        {
            RootNavigation.MenuItems.Clear();
            _navItemMap.Clear();

            // 目录顺序即导航顺序（工作台组在前、系统组在后）。
            // 分组交界处插入分隔线，让系统/支持组在视觉上退居背景。
            string? previousGroupId = null;

            foreach (var registration in _pageCatalog.Pages)
            {
                if (previousGroupId != null &&
                    !string.Equals(previousGroupId, registration.GroupId, StringComparison.Ordinal))
                {
                    RootNavigation.MenuItems.Add(new NavigationViewItemSeparator());
                }

                previousGroupId = registration.GroupId;

                var item = new NavigationViewItem
                {
                    Content = registration.Title,
                    Tag = registration.Id,
                    Icon = new SymbolIcon(registration.Icon)
                };

                // 折叠窗格时标题列被压到 0 宽，WPF-UI 的 LeftCompactNavigationViewItemTemplate
                // 对行高只给了 MinHeight，行高是"内容测量"出来的 —— 本地化标题越长，测量出的行高
                // 越高，于是每个 Tab 被上下拉伸、且拉伸量随标题长度变化。
                // ① 标题显式 NoWrap + 省略号：行高恒为单行，不会因换行而变高；
                // ② MaxHeight 钉死为 WPF-UI 的紧凑行高 40：即使上游模板/本地化再变化，
                //    折叠与展开下行高也保持一致。
                // 临时页（动态标签页）用斜体标题 + hover 关闭钮模板（openspec
                // 2026-09-08-dynamic-settings-tabs），行高约束同上。
                // HorizontalContentAlignment=Stretch 让内容模板 Grid 铺满整行行宽，
                // 关闭钮因此贴住整个 Tab 的最右缘（默认 Left 会导致按钮悬在文本旁）。
                if (registration.IsTransient)
                {
                    item.ContentTemplate = _transientNavItemTemplate;
                    item.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                }
                else
                {
                    item.ContentTemplate = NavigationItemContentTemplate;
                }
                item.MaxHeight = CompactNavItemHeight;

                // Stable UIA identity for E2E settings-page workflows; localized
                // display text is never used for lookup (Pulsar is bilingual).
                AutomationProperties.SetAutomationId(item, "Pulsar.Settings.Nav." + registration.Id);

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
            HookNavIndicatorReposition();
        }

        /// <summary>
        /// 自绘指示器在 DPI 变化 / 窗格折叠展开 / 面板尺寸变化时会错位（提案 P2 已知缺陷）。
        /// 在这些场景重排后重新定位，保持指示器贴紧选中项。
        /// </summary>
        private void HookNavIndicatorReposition()
        {
            RootNavigation.PaneOpened += OnNavPaneStateChanged;
            RootNavigation.PaneClosed += OnNavPaneStateChanged;
            NavPaneGrid.SizeChanged += OnNavPaneSizeChanged;
            DpiChanged += OnWindowDpiChanged;
            // 自愈兜底：条目重建（临时页注册/注销、语言切换）后，无论内部容器何时
            // 完成生成/布局，每次布局稳定都会重新校准指示器位置——不再依赖
            // "UpdateLayout 后一次测量即可用"的假设（该假设在 WPF-UI 4.3 的
            // NavigationView 内部 ListView 容器延迟生成下不成立）。
            RootNavigation.LayoutUpdated += OnNavPaneLayoutUpdated;
        }

        private void UnhookNavIndicatorReposition()
        {
            RootNavigation.PaneOpened -= OnNavPaneStateChanged;
            RootNavigation.PaneClosed -= OnNavPaneStateChanged;
            NavPaneGrid.SizeChanged -= OnNavPaneSizeChanged;
            DpiChanged -= OnWindowDpiChanged;
            RootNavigation.LayoutUpdated -= OnNavPaneLayoutUpdated;
        }

        private void OnNavPaneLayoutUpdated(object? sender, EventArgs e)
        {
            // 动画期间由动画自身管理指示器；动画结束后由 finally 落定基础值。
            if (_isNavAnimating) return;
            RepositionNavIndicatorImmediate(clearHeldAnimations: true);
        }

        private void OnNavPaneStateChanged(NavigationView sender, RoutedEventArgs e)
        {
            RepositionNavIndicator();
        }

        private void OnNavPaneSizeChanged(object sender, SizeChangedEventArgs e)
        {
            RepositionNavIndicator();
        }

        private void OnWindowDpiChanged(object? sender, DpiChangedEventArgs e)
        {
            RepositionNavIndicator();
        }

        private void RepositionNavIndicator()
        {
            // 等待本次布局/缩放完成后再读取最新坐标，避免用旧 bounds 定位
            _ = Dispatcher.InvokeAsync(() =>
            {
                // 动画期间指示器归动画独占：事件驱动的重定位（DPI 变化 / 窗格开合 /
                // 面板尺寸 / 导航项重建）必须让路。否则 InitializeNavIndicator 的
                // clearHeldAnimations=true 会把正在跑的 stretch 动画整段摘掉，指示器
                // 直接瞬移到目标。2026-09-10 追踪现场（切换 Appearance → Slots）：
                //   24.469 [ANIM] PHASE1 baseTop=207.0 → stretchTop=69.7 stretchH=159.3
                //   24.470 [SNAP] caller=InitializeNavIndicator clearAnim=True animating=True
                //   24.599 [ANIM] PHASE2 atTop=69.7 atH=22.0  ← 已在终点：零可见位移
                // 让路后由动画收尾（finally 落定基础值）+ LayoutUpdated 自愈接管。
                if (_isNavAnimating) return;

                InitializeNavIndicator();
            }, System.Windows.Threading.DispatcherPriority.Render);
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
            var previousPageId = _activePageId;
            _activePageId = pageId;

            // 导航离开钩子（openspec 2026-09-08-dynamic-settings-tabs）：来源页为干净
            // 临时页时自动回收（注销注册 → 移除导航项与页面缓存）；脏临时页保留。
            if (!string.Equals(previousPageId, pageId, StringComparison.OrdinalIgnoreCase))
            {
                _transientPageService.NotifyNavigatedAwayFrom(previousPageId);
            }

            if (!_pageCatalog.TryGetRegistration(pageId, out var registration))
            {
                _logger.LogWarning("[SettingsWindow] No registration found for shell page '{PageId}'", pageId);
                return;
            }

            if (!_pages.TryGetValue(registration.Id, out var page))
            {
                page = _pageFactory.CreatePage(registration.Id, _viewModel);
                _pages[registration.Id] = page;
                _themeService.ApplyTheme(page, _themeService.CurrentTheme);
            }

            NavigateWithAnimation(page);
            ApplySelectedNavigationItem(registration.Id);
        }

        private void OnTransientPageRegistered(SettingsPageRegistration registration)
        {
            RebuildNavigationPreservingSelection();
        }

        private void OnTransientPageUnregistered(string pageId)
        {
            // 页面实例随回收销毁（重开时按当前配置重建）。
            _pages.Remove(pageId);

            // 关闭的是当前页（Tab 关闭钮）：回落到默认页，避免悬空指向已注销页面。
            if (string.Equals(_shellViewModel.CurrentPageId, pageId, StringComparison.OrdinalIgnoreCase))
            {
                _activePageId = null;
                _ = _shellViewModel.NavigateAsync(_pageCatalog.DefaultPageId, userInitiated: false);
            }

            RebuildNavigationPreservingSelection();
        }

        /// <summary>
        /// 目录变更（临时页注册/注销、语言切换）共用的导航项重建入口：
        /// 从目录（含临时注册）整体重建并恢复选中态，随后重排自绘指示器。
        /// 重建后强制走一轮同步布局——否则指示器测量到的还是旧条目 bounds（高度 0
        /// 或旧坐标），表现为回收/增删后指示器停在原地。
        /// </summary>
        private void RebuildNavigationPreservingSelection()
        {
            var activePageId = _shellViewModel.CurrentPageId;
            _previousActiveItem = null;
            BuildNavigationItems();
            ApplySelectedNavigationItem(activePageId);
            RootNavigation.UpdateLayout();
            RepositionNavIndicator();
        }

        /// <summary>
        /// 临时页 Tab 的关闭钮：干净页直接回收；脏页经守卫确认（保存/放弃/取消）后回收。
        /// </summary>
        private async void TransientTabClose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button { Tag: string pageId })
            {
                await _transientPageService.CloseTransientPageAsync(pageId);
            }
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

            var duration = new Duration(TimeSpan.FromMilliseconds(260));
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var fadeIn = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
            var slideUp = new DoubleAnimation(20, 0, duration) { EasingFunction = ease };

            page.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            ((TranslateTransform)page.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slideUp);
        }

        private const double IndicatorWidth = 3;
        private const double IndicatorHeight = 22;

        /// <summary>
        /// WPF-UI 紧凑导航行的标准行高（<c>LeftCompactNavigationViewItemTemplate</c> 的 MinHeight）。
        /// 用作 <c>NavigationViewItem.MaxHeight</c>，使折叠/展开窗格时行高保持恒定。
        /// </summary>
        private const double CompactNavItemHeight = 40;

        /// <summary>
        /// 导航项标题模板。WPF-UI 把字符串内容交给隐式生成的 TextBlock 渲染；折叠窗格后标题列
        /// 宽度归零，行高由内容测量决定，本地化标题越长行越高（Tab 被上下拉伸）。
        /// 这里显式指定 NoWrap + 字符省略号，把行高锁死为单行。
        /// </summary>
        private static readonly DataTemplate NavigationItemContentTemplate = CreateNavigationItemContentTemplate();

        private static DataTemplate CreateNavigationItemContentTemplate()
        {
            var textBlock = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
            textBlock.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding());
            textBlock.SetValue(System.Windows.Controls.TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
            textBlock.SetValue(System.Windows.Controls.TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            textBlock.SetValue(System.Windows.Controls.TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textBlock.SetValue(System.Windows.Controls.TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            return new DataTemplate { VisualTree = textBlock };
        }

        private void InitializeNavIndicator()
        {
            RepositionNavIndicatorImmediate(clearHeldAnimations: true);
        }

        /// <summary>
        /// 活动条目解析：优先按 shell 的 CurrentPageId（单一事实来源，条目重建后
        /// 依然成立），回退到 IsActive 扫描。
        /// </summary>
        private NavigationViewItem? FindActiveNavItem()
        {
            var currentId = _shellViewModel.CurrentPageId;
            if (!string.IsNullOrEmpty(currentId)
                && _navItemMap.TryGetValue(currentId, out var byId))
            {
                return byId;
            }

            return _navItemMap.Values.FirstOrDefault(i => i.IsActive);
        }

        /// <summary>
        /// 同步重定位指示器（不等待布局）。调用方保证不在动画期间：布局回调
        /// （<see cref="OnNavPaneLayoutUpdated"/>）与事件驱动的延迟重定位
        /// （<see cref="RepositionNavIndicator"/>）都已用 <c>_isNavAnimating</c> 过滤。
        /// </summary>
        /// <param name="clearHeldAnimations">
        /// true 时先摘除 Canvas.Top/Height 上的残留动画：上一轮动画以 HoldEnd
        /// 挂起时，<see cref="Canvas.SetTop"/> 写入的基础值会被活动动画覆盖，
        /// 表现为指示器"停在原地"。动画收尾阶段（finally）用 false，避免
        /// 掐断正在收尾的 snap 动画。
        /// </param>
        private void RepositionNavIndicatorImmediate(bool clearHeldAnimations)
        {
            var activeItem = FindActiveNavItem();
            if (activeItem == null) return;

            var bounds = GetItemRelativeBounds(activeItem);
            if (bounds.Height <= 0)
            {
                // 条目尚未完成容器生成/布局：不写旧值；LayoutUpdated 稳定后会再次进入。
                return;
            }

            if (clearHeldAnimations)
            {
                NavIndicator.BeginAnimation(Canvas.TopProperty, null);
                NavIndicator.BeginAnimation(FrameworkElement.HeightProperty, null);
            }

            var centerY = bounds.Top + (bounds.Height - IndicatorHeight) / 2;
            var currentTop = Canvas.GetTop(NavIndicator);
            var currentLeft = Canvas.GetLeft(NavIndicator);
            var settled = NavIndicator.Visibility == Visibility.Visible
                && !double.IsNaN(currentTop) && Math.Abs(currentTop - centerY) < 0.5
                && !double.IsNaN(currentLeft) && Math.Abs(currentLeft - bounds.Left) < 0.5;
            if (settled)
            {
                // 已贴合目标：跳过写入，终止 LayoutUpdated 自愈回环。
                return;
            }

            Canvas.SetLeft(NavIndicator, bounds.Left);
            Canvas.SetTop(NavIndicator, centerY);
            NavIndicator.Height = IndicatorHeight;
            NavIndicator.Width = IndicatorWidth;
            NavIndicator.Visibility = Visibility.Visible;
        }

        private async Task AnimateNavIndicatorAsync(string? oldPageId, string? newPageId)
        {
            if (_isNavAnimating || string.IsNullOrEmpty(newPageId)) return;
            if (!_navItemMap.TryGetValue(newPageId, out var newItem))
            {
                // 新条目不在位（如旧临时项被回收重建期间）：退化为直接重定位。
                RepositionNavIndicator();
                return;
            }

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

                if (oldBounds.Height <= 0 || newBounds.Height <= 0)
                {
                    // 任一端 bounds 无效（布局未完成/条目已被回收移除）：退化为直接重定位。
                    // 同时放弃动画独占——否则紧随其后的延迟重定位会被 RepositionNavIndicator
                    // 的动画守卫拦下，指示器停在旧位置不动。
                    _isNavAnimating = false;
                    RepositionNavIndicator();
                    return;
                }

                var oldCenterY = oldBounds.Top + (oldBounds.Height - IndicatorHeight) / 2;
                var newCenterY = newBounds.Top + (newBounds.Height - IndicatorHeight) / 2;
                var oldBottomCenter = oldCenterY + IndicatorHeight;
                var newBottomCenter = newCenterY + IndicatorHeight;

                var stretchTop = Math.Min(oldCenterY, newCenterY);
                var stretchBottom = Math.Max(oldBottomCenter, newBottomCenter);
                var stretchHeight = stretchBottom - stretchTop;

                // [FIX 2026-09-09] 崩溃防御（17:36 FTL 现场）：指示器尚未被布局回调写入
                // 基础值时 Canvas.Top/Left 为 NaN——渲染 tick 以 NaN 取值抛
                // AnimationException（未处理 → 进程崩溃）。动画前先把基础值落好；
                // 落不了（无活动条目，如页注册竞态下选区未应用）就放弃动画退化为重定位。
                // [2026-09-10] 动画起点已改为显式 From（见下），隐式 origin 取值那条崩溃
                // 通道不复存在；此处保留为「基础值必须可读」的前置条件。
                if (double.IsNaN(oldCenterY) || double.IsNaN(newCenterY) || double.IsNaN(stretchHeight))
                {
                    _isNavAnimating = false;
                    RepositionNavIndicator();
                    return;
                }

                if (double.IsNaN(Canvas.GetTop(NavIndicator)) || double.IsNaN(Canvas.GetLeft(NavIndicator)))
                {
                    RepositionNavIndicatorImmediate(clearHeldAnimations: true);
                    if (double.IsNaN(Canvas.GetTop(NavIndicator)) || double.IsNaN(Canvas.GetLeft(NavIndicator)))
                    {
                        return; // 无活动条目可定位：无从动画，交回 LayoutUpdated 自愈
                    }
                }

                // 动画起点显式取「当前生效值」，不依赖 From=null 的隐式 origin：隐式 origin
                // 会被并发的延迟重定位写入的基础值改写（2026-09-10 追踪：切到 Slots 时
                // [SNAP] 抢在 PHASE1 前后写入基础值 → phase 2 起点错位，指示器先跳后滑）。
                // 显式 From 后 phase 1 从当前位置起步、phase 2 从 phase 1 的落点续跑，
                // 与并发写入彻底解耦，并顺带覆盖 Height 为 NaN 的取值崩溃。
                var startTop = Canvas.GetTop(NavIndicator);
                var startHeight = NavIndicator.Height;
                if (double.IsNaN(startTop) || double.IsNaN(startHeight))
                {
                    RepositionNavIndicatorImmediate(clearHeldAnimations: true);
                    return;
                }

                var stretchDuration = TimeSpan.FromMilliseconds(120);
                var snapDuration = TimeSpan.FromMilliseconds(130);
                var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

                // Phase 1: Stretch
                var stretchTopAnim = new DoubleAnimation(startTop, stretchTop, stretchDuration) { EasingFunction = easing };
                var stretchHeightAnim = new DoubleAnimation(startHeight, stretchHeight, stretchDuration) { EasingFunction = easing };

                stretchTopAnim.FillBehavior = FillBehavior.HoldEnd;
                stretchHeightAnim.FillBehavior = FillBehavior.HoldEnd;

                NavIndicator.BeginAnimation(Canvas.TopProperty, stretchTopAnim);
                NavIndicator.BeginAnimation(FrameworkElement.HeightProperty, stretchHeightAnim);

                await Task.Delay(stretchDuration);

                // Phase 2: Snap to new position（起点 = phase 1 的落点，保证帧间连续）
                var snapTopAnim = new DoubleAnimation(stretchTop, newCenterY, snapDuration) { EasingFunction = easing };
                var snapHeightAnim = new DoubleAnimation(stretchHeight, IndicatorHeight, snapDuration) { EasingFunction = easing };
                NavIndicator.BeginAnimation(Canvas.TopProperty, snapTopAnim);
                NavIndicator.BeginAnimation(FrameworkElement.HeightProperty, snapHeightAnim);

                await Task.Delay(snapDuration);
            }
            finally
            {
                // 先把目标位置写入基础值（不清动画，让 snap 平滑收尾后精确落在
                // base 上），再解除动画标志——解除后 LayoutUpdated 自愈才会生效，
                // 顺序保证自愈不会在动画进行中清除动画。
                RepositionNavIndicatorImmediate(clearHeldAnimations: false);
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

            // 点在临时页 Tab 的关闭钮上：交给按钮的 Click 处理，不触发导航。
            if (IsTransientCloseClick(e.OriginalSource as DependencyObject))
            {
                return;
            }

            if (sender is NavigationViewItem item)
            {
                await _shellViewModel.NavigateAsync(item.Tag?.ToString(), userInitiated: true);
            }
        }

        /// <summary>
        /// 判定鼠标事件源是否落在临时页 Tab 的关闭钮内（命中检测沿可视树上行，
        /// 在到达所属 NavigationViewItem 前先碰到关闭钮即视为关闭意图）。
        /// </summary>
        private static bool IsTransientCloseClick(DependencyObject? source)
        {
            for (var node = source; node != null; node = VisualTreeHelper.GetParent(node))
            {
                if (node is NavigationViewItem)
                {
                    return false;
                }

                if (node is System.Windows.Controls.Button button &&
                    string.Equals(AutomationProperties.GetAutomationId(button), "Pulsar.Settings.NavCloseTab", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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
                // 目录（含临时注册）整体重建并恢复选中：已打开的临时页条目保留。
                RebuildNavigationPreservingSelection();
            });
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            InitializeNavIndicator();
        }

        private void OnThemeChanged(object? sender, AppTheme theme)
        {
            var backdrop = this is FluentWindow fw ? fw.WindowBackdropType : WindowBackdropType.None;
            _themeService.ApplyTheme(this, theme, backdrop);
            foreach (var page in _pages.Values)
            {
                _themeService.ApplyTheme(page, theme);
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
            _pageCatalog.TransientPageRegistered -= OnTransientPageRegistered;
            _pageCatalog.TransientPageUnregistered -= OnTransientPageUnregistered;
            _themeService.ThemeChanged -= OnThemeChanged;
            _shellViewModel.PropertyChanged -= ShellViewModel_PropertyChanged;
            _localizationService.LanguageChanged -= OnLanguageChanged;
            UnhookNavIndicatorReposition();

            foreach (var item in RootNavigation.MenuItems.OfType<NavigationViewItem>())
            {
                item.PreviewMouseLeftButtonUp -= NavigationItem_PreviewMouseLeftButtonUp;
                item.KeyUp -= NavigationItem_KeyUp;
            }

            // 临时页是会话级的（settings-transient-pages spec）：先摘除目录事件再注销，
            // 避免注销事件触发窗口重建导航；下次打开设置窗口侧边栏只有常驻条目。
            foreach (var transientId in _pageCatalog.Pages
                         .Where(p => p.IsTransient)
                         .Select(p => p.Id)
                         .ToList())
            {
                _pageCatalog.UnregisterTransient(transientId);
            }

            _pages.Clear();
            _activePageId = null;

            // 实体级临时页构造器闭包持有本窗口会话的 live slot 与 SettingsViewModel
            // （unify-slot-editor-transient-pages D2）：窗口关闭即清空，防止跨会话悬挂。
            App.Current.Services.GetRequiredService<SettingsEntityPageStore>().Clear();

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
