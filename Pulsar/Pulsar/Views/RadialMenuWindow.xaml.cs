// [Path]: Pulsar/Pulsar/Views/RadialMenuWindow.xaml.cs

using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.Native;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Wpf.Ui.Controls;
using System.Windows.Input; // For Mouse.Capture

namespace Pulsar.Views
{
    public partial class RadialMenuWindow : Window
    {
        private readonly RadialMenuViewModel _viewModel;
        private readonly IThemeService _themeService;
        private readonly ILogger<RadialMenuWindow> _logger;

        // [Fix] 添加 WindowService 字段以解决报错
        private readonly IWindowService _windowService;
        private readonly IFocusManager _focusManager;
        private readonly IMenuViewportService _menuViewportService;

        // Full visual extent of the 500x500 menu canvas, including title and slot
        // overshoot. The viewport service keeps this extent inside the work area.
        private const double MenuVisualExtentDip = 260;

        public RadialMenuWindow(
            RadialMenuViewModel vm,
            IWindowService windowService,
            IThemeService themeService,
            ILogger<RadialMenuWindow> logger,
            IFocusManager focusManager,
            IMenuViewportService menuViewportService)
        {
            // Initialize Fields First
            _viewModel = vm;
            _windowService = windowService;
            _themeService = themeService;
            _logger = logger;
            _focusManager = focusManager;
            _menuViewportService = menuViewportService;

            InitializeComponent();
            DataContext = vm;

            // 1. 监听 ViewModel 的属性变化 (Show/Hide 信号)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // 2. 窗口加载时初始化主题 (Loads user config)
            InitializeTheme();
            
            // Listen for theme changes from other windows
            _themeService.ThemeChanged += (s, theme) =>
            {
                _themeService.ApplyTheme(this, theme, WindowBackdropType.None);
                _themeService.EnforceTransparency(this);
            };

            // 3. [Fix] 注册隐藏自身的能力
            _windowService.RegisterHideAction(() =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    Dismiss();
                });
            });

            // Keyboard affordances: Escape cancels, Left/Right page through slots.
            PreviewKeyDown += OnPreviewKeyDown;

            _viewModel.OnPagingBoundaryFeedbackRequested += HandlePagingBoundaryFeedbackRequested;

            // ====================================================
            // 👻 [驻留模式初始化] (Resident Mode Init)
            // ====================================================
            // Idle surface is 1x1. PrepareViewport expands it to the current monitor
            // work area only while the radial menu is visible.
            this.Width = 1;
            this.Height = 1;
            this.Opacity = 0;
            this.Visibility = Visibility.Visible;
            this.IsHitTestVisible = false;
            this.ShowInTaskbar = false;
        }

        // [新增] 窗口句柄创建后的初始化钩子
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // 1. 获取窗口句柄
            var hwnd = new WindowInteropHelper(this).Handle;
            _viewModel.SetWindowHandle(hwnd);
            // 2. 获取当前扩展样式
            long currentStyle = PulsarNative.GetWindowLong(hwnd, PulsarNative.GWL_EXSTYLE);
            // 3. 注入 ToolWindow 样式 (使其在 Alt+Tab 中不可见)
            PulsarNative.SetWindowLong(hwnd, PulsarNative.GWL_EXSTYLE, currentStyle | PulsarNative.WS_EX_TOOLWINDOW);

            // 4. [New] Activate "Self-Healing" Transparency
            _themeService.EnforceTransparency(this);
        }

        private void InitializeTheme()
        {
            // ThemeService is bootstrapped from Profiles.json before this window is created.
            _themeService.ApplyTheme(this, _themeService.CurrentTheme, WindowBackdropType.None);
        }



        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RadialMenuViewModel.IsVisible))
            {
                if (_viewModel.IsVisible)
                {
                    Summon();
                }
                else
                {
                    _logger?.LogInformation("[RadialMenu] IsVisible=false, calling Dismiss...");
                    Dismiss();
                }
            }
        }

        // ==========================================
        // 🚀 核心交互逻辑 (Core Interaction)
        // ==========================================

        private void Summon()
        {
            // [Fix] Removed redundant SetPreviousWindow call.
            // The ViewModel captures PulsarContext BEFORE this window becomes visible.

            // 1. Expand the resident 1x1 window to the work area of the monitor under
            //    the cursor, then place the 500x500 menu canvas around the clamped
            //    pointer position.
            var viewport = _menuViewportService.PrepareViewport(this, MenuVisualExtentDip);
            _viewModel.SetMenuCenter(viewport.MenuCenterDip);

            // Kando-style pointer correction: when the menu center had to move away
            // from the cursor near a screen edge, warp the pointer onto the center so
            // the "menu follows pointer" invariant is restored.
            if (viewport.PointerWarpRequired)
            {
                int physicalX = (int)Math.Round(viewport.MenuCenterDip.X * viewport.DpiScaleX);
                int physicalY = (int)Math.Round(viewport.MenuCenterDip.Y * viewport.DpiScaleY);
                PulsarNative.SetCursorPos(physicalX, physicalY);
            }
            
            // [Refactor] Resident Mode: Window is always Visible.
            // Ensure Visibility is Visible (just in case)
            if (this.Visibility != Visibility.Visible)
            {
                this.Visibility = Visibility.Visible;
            }

            // Bring to foreground and Activate via FocusManager
            _focusManager.ActivateMenu(this);
            
            // Clear any HoldEnd animations from Dismiss to prevent "flicker" from old values
            this.BeginAnimation(UIElement.OpacityProperty, null);
            this.Opacity = 0;

            // Prepare Animations
            var scaleAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(150));
            scaleAnim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            
            var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(100));

            var trans = new ScaleTransform(1, 1);
            MenuCanvas.RenderTransform = trans;
            MenuCanvas.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            
            trans.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            trans.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            
            // Start Fade In
            this.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            this.Focus();
            this.UpdateLayout();
        }

        private void Dismiss()
        {
            _logger?.LogInformation("[RadialMenu] Dismiss START: actionExecuted={ActionExecuted} restoreMode={RestoreMode}",
                _viewModel.ActionExecuted, _focusManager.RestoreMode);

            this.IsHitTestVisible = false;

            _viewModel.ClearPreviewPresentation();

            // Fade is eased and slightly slower than the slot release (320ms) so the
            // window and the active slot's exit animation close out in one rhythm
            // instead of the fade racing ahead and cutting the animation mid-flight.
            var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            };
            
            fadeOut.Completed += async (s, e) =>
            {
                _logger?.LogInformation("[RadialMenu] Dismiss: fade complete, calling ReleaseAsync (mode={RestoreMode})",
                    _focusManager.RestoreMode);
                _viewModel.ClearVisuals();
                _menuViewportService.CollapseViewport(this);
                await _focusManager.ReleaseAsync();
                _logger?.LogInformation("[RadialMenu] Dismiss: ReleaseAsync complete");
            };
            
            this.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.OnPagingBoundaryFeedbackRequested -= HandlePagingBoundaryFeedbackRequested;
            }
            base.OnClosed(e);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_viewModel.IsVisible)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.Escape:
                    _viewModel.CancelActiveMenu();
                    e.Handled = true;
                    break;

                case Key.Left:
                    if (_viewModel.HandlePagingKey(-1))
                    {
                        e.Handled = true;
                    }
                    break;

                case Key.Right:
                    if (_viewModel.HandlePagingKey(1))
                    {
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void HandlePagingBoundaryFeedbackRequested(BoundaryDirection direction)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => HandlePagingBoundaryFeedbackRequested(direction));
                return;
            }

            ScaleTransform scaleTransform;
            TranslateTransform translateTransform;

            if (MenuCanvas.RenderTransform is TransformGroup existingTransformGroup)
            {
                scaleTransform = existingTransformGroup.Children.OfType<ScaleTransform>().FirstOrDefault() ?? new ScaleTransform(1, 1);
                translateTransform = existingTransformGroup.Children.OfType<TranslateTransform>().FirstOrDefault() ?? new TranslateTransform(0, 0);

                if (!existingTransformGroup.Children.Contains(scaleTransform))
                {
                    existingTransformGroup.Children.Insert(0, scaleTransform);
                }

                if (!existingTransformGroup.Children.Contains(translateTransform))
                {
                    existingTransformGroup.Children.Add(translateTransform);
                }
            }
            else
            {
                scaleTransform = new ScaleTransform(1, 1);
                translateTransform = new TranslateTransform(0, 0);
                var newTransformGroup = new TransformGroup();
                newTransformGroup.Children.Add(scaleTransform);
                newTransformGroup.Children.Add(translateTransform);
                MenuCanvas.RenderTransform = newTransformGroup;
                MenuCanvas.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);
            }

            double offset = direction == BoundaryDirection.FirstPage ? 14 : -14;
            var duration = TimeSpan.FromMilliseconds(260);
            var nudgeEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            var settleEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };

            var scaleXAnimation = new DoubleAnimationUsingKeyFrames();
            scaleXAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0.985, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
            scaleXAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(duration)) { EasingFunction = settleEase });

            var scaleYAnimation = new DoubleAnimationUsingKeyFrames();
            scaleYAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0.985, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
            scaleYAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(duration)) { EasingFunction = settleEase });

            var translateAnimation = new DoubleAnimationUsingKeyFrames();
            translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(offset, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))) { EasingFunction = nudgeEase });
            translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(duration)) { EasingFunction = settleEase });

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
            translateTransform.BeginAnimation(TranslateTransform.YProperty, translateAnimation);
        }
    }
}
