using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Native;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Base;
using Pulsar.Views;
using Pulsar.Views.Dialogs;
using Wpf.Ui.Controls;

namespace Pulsar.Services
{
    public class DialogService : IDialogService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IThemeService _themeService;
        private readonly IWindowPlacementService _windowPlacementService;
        private readonly ILocalizationService _loc;

        public DialogService(
            IServiceProvider serviceProvider,
            IThemeService themeService,
            IWindowPlacementService windowPlacementService,
            ILocalizationService localizationService)
        {
            _serviceProvider = serviceProvider;
            _themeService = themeService;
            _windowPlacementService = windowPlacementService;
            _loc = localizationService;
        }

        public async Task<Pulsar.Models.Enums.DialogResult> ShowMessageAsync(
            string title, 
            string message, 
            DialogType type = DialogType.Info, 
            DialogButtons buttons = DialogButtons.Ok)
        {
            return await RunOnUi(() =>
            {
                var vm = new DialogHostViewModel(_loc)
                {
                    Title = title,
                    Content = message, // Simple string content
                    DialogType = type
                };
                vm.ConfigureButtons(buttons);

                // Use Small for SaveDontSaveCancel (3 buttons), XSmall for simple messages
                var sizeConstraints = buttons == DialogButtons.SaveDontSaveCancel 
                    ? DialogSizeConstraints.Small 
                    : DialogSizeConstraints.XSmall;

                return ShowDialogInternal(vm, DialogPlacement.CenterOwner, sizeConstraints);
            });
        }

        public async Task<Pulsar.Models.Enums.DialogResult> ShowCustomAsync<TViewModel>(
            string title, 
            TViewModel content, 
            DialogButtons buttons = DialogButtons.OkCancel)
        {
            return await ShowCustomAsync(title, content, buttons, DialogSizeConstraints.Medium);
        }

        public async Task<Pulsar.Models.Enums.DialogResult> ShowCustomAsync<TViewModel>(
            string title, 
            TViewModel content, 
            DialogButtons buttons,
            DialogSizeConstraints sizeConstraints)
        {
            return await ShowCustomAsync(title, content, buttons, sizeConstraints, null);
        }

        public async Task<Pulsar.Models.Enums.DialogResult> ShowCustomAsync<TViewModel>(
            string title, 
            TViewModel content, 
            DialogButtons buttons,
            DialogSizeConstraints sizeConstraints,
            AppTheme? themeOverride)
        {
            return await RunOnUi(() =>
            {
                var vm = new DialogHostViewModel(_loc)
                {
                    Title = title
                };
                
                if (content is IDialogViewModel dialogVm)
                {
                    dialogVm.RequestClose = (result) => vm.CloseCommand.Execute(result);
                }

                vm.ConfigureButtons(buttons);
                vm.Content = content;

                return ShowDialogInternal(vm, DialogPlacement.CenterOwner, sizeConstraints, WindowBackdropType.Mica, themeOverride);
            });
        }

        public async Task<string?> ShowInputAsync(string title, string message, string defaultValue = "")
        {
            return await RunOnUi(() =>
            {
                var inputVm = new ViewModels.Dialogs.InputDialogViewModel(message, defaultValue);
                
                var vm = new DialogHostViewModel(_loc)
                {
                    Title = title,
                    Content = inputVm,
                    IsPrimaryButtonVisible = true,
                    PrimaryButtonText = _loc["Dialog.Button.Ok"],
                    IsSecondaryButtonVisible = true,
                    SecondaryButtonText = _loc["Dialog.Button.Cancel"]
                };

                inputVm.RequestClose = (result) => vm.CloseCommand.Execute(result);


                var dialogResult = ShowDialogInternal(vm, DialogPlacement.CenterOwner, DialogSizeConstraints.Small);

                // Return the input text if user confirmed, otherwise null
                return dialogResult == Pulsar.Models.Enums.DialogResult.Confirmed 
                    ? inputVm.InputText 
                    : null;
            });
        }

        public async Task<Pulsar.Models.Enums.DialogResult> ShowConfirmationAsync(
            string title, 
            string message, 
            string? confirmText = null, 
            string? cancelText = null)
        {
            return await RunOnUi(() =>
            {
                var vm = new DialogHostViewModel(_loc)
                {
                    Title = title,
                    Content = message,
                    IsPrimaryButtonVisible = true,
                    PrimaryButtonText = confirmText ?? _loc["Dialog.Button.Confirm"],
                    IsSecondaryButtonVisible = true,
                    SecondaryButtonText = cancelText ?? _loc["Dialog.Button.Cancel"]
                };

                return ShowDialogInternal(vm, DialogPlacement.CenterOwner, DialogSizeConstraints.XSmall);
            });
        }

        /// <summary>
        /// Finds the best owner window for a dialog based on placement strategy.
        /// Priority: Active Window > SettingsWindow > MainWindow
        /// </summary>
        private Window? FindBestOwner(DialogPlacement placement)
        {
            if (placement == DialogPlacement.CenterScreen || placement == DialogPlacement.NearMouse)
            {
                // These placements don't need an owner
                return null;
            }

            var allWindows = System.Windows.Application.Current.Windows.OfType<Window>().ToList();

            // Priority 1: Currently active window (if visible and not minimized)
            var activeWindow = allWindows.FirstOrDefault(w => 
                w.IsActive && 
                w.IsVisible && 
                w.WindowState != WindowState.Minimized);
            
            if (activeWindow != null)
                return activeWindow;

            // Priority 2: SettingsWindow (if open)
            var settingsWindow = allWindows.OfType<SettingsWindow>().FirstOrDefault(w => 
                w.IsVisible && 
                w.WindowState != WindowState.Minimized);
            
            if (settingsWindow != null)
                return settingsWindow;

            // Priority 3: MainWindow (if visible)
            if (System.Windows.Application.Current.MainWindow != null &&
                System.Windows.Application.Current.MainWindow.IsVisible)
            {
                return System.Windows.Application.Current.MainWindow;
            }

            // Fallback: Any visible window
            return allWindows.FirstOrDefault(w => 
                w.IsVisible && 
                w.WindowState != WindowState.Minimized);
        }

        private AppTheme InferThemeFromContext()
        {
            return _themeService.CurrentTheme;
        }

        /// <summary>
        /// Applies placement strategy to the dialog window.
        /// </summary>
        private void ApplyPlacement(Window window, DialogPlacement placement, Window? owner)
        {
            switch (placement)
            {
                case DialogPlacement.CenterOwner:
                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    window.Owner = owner;
                    break;

                case DialogPlacement.CenterScreen:
                    window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    break;

                case DialogPlacement.NearMouse:
                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                    PositionNearMouse(window);
                    break;

                case DialogPlacement.CenterActiveWindow:
                    window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    window.Owner = System.Windows.Application.Current.Windows
                        .OfType<Window>()
                        .FirstOrDefault(w => w.IsActive);
                    break;
            }
        }

        /// <summary>
        /// Positions the window near the mouse cursor with screen boundary checks.
        /// </summary>
        private void PositionNearMouse(Window window)
        {
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
            var cursor = _windowPlacementService.ToDip(
                0, 0, dpi.DpiScaleX, dpi.DpiScaleY);

            PulsarNative.GetCursorPos(out var pt);
            cursor = _windowPlacementService.ToDip(pt.X, pt.Y, dpi.DpiScaleX, dpi.DpiScaleY);

            // Offset from cursor (avoid covering cursor), converted to DIP.
            const double offsetDip = 20;
            double left = cursor.X + offsetDip;
            double top = cursor.Y + offsetDip;

            var monitor = PulsarNative.MonitorFromPoint(pt, PulsarNative.MONITOR_DEFAULTTONEAREST);
            var monitorInfo = new PulsarNative.MONITORINFO
            {
                cbSize = Marshal.SizeOf<PulsarNative.MONITORINFO>()
            };

            if (monitor == IntPtr.Zero || !PulsarNative.GetMonitorInfo(monitor, ref monitorInfo))
            {
                window.Left = left;
                window.Top = top;
                return;
            }

            double workLeft = monitorInfo.rcWork.Left / dpi.DpiScaleX;
            double workTop = monitorInfo.rcWork.Top / dpi.DpiScaleY;
            double workRight = monitorInfo.rcWork.Right / dpi.DpiScaleX;
            double workBottom = monitorInfo.rcWork.Bottom / dpi.DpiScaleY;
            double workWidth = workRight - workLeft;
            double workHeight = workBottom - workTop;

            // Ensure window stays within monitor working area (excludes taskbar).
            left = window.Width >= workWidth
                ? workLeft
                : Math.Clamp(left, workLeft, workRight - window.Width);
            top = window.Height >= workHeight
                ? workTop
                : Math.Clamp(top, workTop, workBottom - window.Height);

            window.Left = left;
            window.Top = top;
        }

        /// <summary>
        /// Applies size constraints to the dialog window.
        /// </summary>
        private void ApplySizeConstraints(Window window, DialogSizeConstraints? constraints)
        {
            if (constraints == null)
                return;

            if (constraints.SizeToContent)
            {
                window.SizeToContent = SizeToContent.WidthAndHeight;
            }
            else
            {
                if (constraints.Width.HasValue)
                    window.Width = constraints.Width.Value;
                
                if (constraints.Height.HasValue)
                    window.Height = constraints.Height.Value;
            }

            if (constraints.MinWidth.HasValue)
                window.MinWidth = constraints.MinWidth.Value;
            
            if (constraints.MinHeight.HasValue)
                window.MinHeight = constraints.MinHeight.Value;
            
            if (constraints.MaxWidth.HasValue)
                window.MaxWidth = constraints.MaxWidth.Value;
            
            if (constraints.MaxHeight.HasValue)
                window.MaxHeight = constraints.MaxHeight.Value;
        }

        /// <summary>
        /// Prepares the dialog window with theme, placement, and size constraints.
        /// </summary>
        private void PrepareWindow(
            DialogHostWindow window, 
            DialogPlacement placement = DialogPlacement.CenterOwner,
            DialogSizeConstraints? sizeConstraints = null,
            WindowBackdropType backdrop = WindowBackdropType.Mica,
            AppTheme? themeOverride = null)
        {
            // Use default constraints if none provided
            var constraints = sizeConstraints ?? DialogSizeConstraints.Default;

            // 1. Apply theme (use override if provided, otherwise infer from context)
            var theme = themeOverride ?? InferThemeFromContext();
            _themeService.ApplyTheme(window, theme, backdrop);

            // 2. Apply size constraints
            ApplySizeConstraints(window, constraints);

            // 3. Configure resize behavior and title bar buttons
            window.ConfigureResizeBehavior(constraints.AllowResize, constraints.ShowMaximizeButton);

            // 4. Find best owner and apply placement
            var owner = FindBestOwner(placement);
            ApplyPlacement(window, placement, owner);
        }

        private Pulsar.Models.Enums.DialogResult ShowDialogInternal(
            DialogHostViewModel viewModel,
            DialogPlacement placement = DialogPlacement.CenterOwner,
            DialogSizeConstraints? sizeConstraints = null,
            WindowBackdropType backdrop = WindowBackdropType.Mica,
            AppTheme? themeOverride = null)
        {
            // Create window without DI (no longer needs IThemeService injection)
            var window = new DialogHostWindow();
            
            window.DataContext = viewModel;

            // Handle Close Request from VM
            Pulsar.Models.Enums.DialogResult result = Pulsar.Models.Enums.DialogResult.None;
            viewModel.RequestClose = (r) =>
            {
                result = r;
                window.Close();
            };

            // Apply all window configurations (theme, placement, size, resize behavior)
            PrepareWindow(window, placement, sizeConstraints, backdrop, themeOverride);

            window.ShowDialog();

            return result;
        }

        public async Task<string?> ShowColorPickerAsync(string title, string initialColor = "#FF0000")
        {
            return await RunOnUi(() =>
            {
                var colorPickerVm = new ViewModels.Dialogs.ColorPickerViewModel(initialColor);
                
                var vm = new DialogHostViewModel(_loc)
                {
                    Title = title,
                    Content = colorPickerVm,
                    IsPrimaryButtonVisible = true,
                    PrimaryButtonText = _loc["Dialog.Button.Select"],
                    IsSecondaryButtonVisible = true,
                    SecondaryButtonText = _loc["Dialog.Button.Cancel"]
                };

                colorPickerVm.RequestClose = (result) => vm.CloseCommand.Execute(result);


                var dialogResult = ShowDialogInternal(vm, DialogPlacement.CenterOwner, DialogSizeConstraints.Medium);

                // Return the selected hex color if user confirmed, otherwise null
                return dialogResult == Pulsar.Models.Enums.DialogResult.Confirmed 
                    ? colorPickerVm.SelectedHex 
                    : null;
            });
        }

        private async Task<T> RunOnUi<T>(Func<T> action)
        {
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                return action();
            }
            else
            {
                return await System.Windows.Application.Current.Dispatcher.InvokeAsync(action);
            }
        }
    }
}
