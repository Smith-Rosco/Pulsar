using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Models;
using Pulsar.Models.Enums;
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

        public DialogService(IServiceProvider serviceProvider, IThemeService themeService)
        {
            _serviceProvider = serviceProvider;
            _themeService = themeService;
        }

        public async Task<Pulsar.Models.Enums.DialogResult> ShowMessageAsync(
            string title, 
            string message, 
            DialogType type = DialogType.Info, 
            DialogButtons buttons = DialogButtons.Ok)
        {
            return await RunOnUi(() =>
            {
                var vm = new DialogHostViewModel
                {
                    Title = title,
                    Content = message // Simple string content
                };
                vm.ConfigureButtons(buttons);

                return ShowDialogInternal(vm, DialogPlacement.CenterOwner, DialogSizeConstraints.Small);
            });
        }

        public async Task<Pulsar.Models.Enums.DialogResult> ShowCustomAsync<TViewModel>(
            string title, 
            TViewModel content, 
            DialogButtons buttons = DialogButtons.OkCancel)
        {
            return await RunOnUi(() =>
            {
                var vm = new DialogHostViewModel
                {
                    Title = title,
                    Content = content
                };
                
                if (content is IDialogViewModel dialogVm)
                {
                    dialogVm.RequestClose = (result) => vm.CloseCommand.Execute(result);
                    vm.IsScrollable = dialogVm.IsScrollable;
                }

                vm.ConfigureButtons(buttons);

                return ShowDialogInternal(vm, DialogPlacement.CenterOwner, DialogSizeConstraints.Medium);
            });
        }

        public async Task<string?> ShowInputAsync(string title, string message, string defaultValue = "")
        {
            return await RunOnUi(() =>
            {
                var inputVm = new ViewModels.Dialogs.InputDialogViewModel(message, defaultValue);
                
                var vm = new DialogHostViewModel
                {
                    Title = title,
                    Content = inputVm,
                    IsPrimaryButtonVisible = true,
                    PrimaryButtonText = "OK",
                    IsSecondaryButtonVisible = true,
                    SecondaryButtonText = "Cancel"
                };

                inputVm.RequestClose = (result) => vm.CloseCommand.Execute(result);
                vm.IsScrollable = false;

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
            string confirmText = "Confirm", 
            string cancelText = "Cancel")
        {
            return await RunOnUi(() =>
            {
                var vm = new DialogHostViewModel
                {
                    Title = title,
                    Content = message,
                    IsPrimaryButtonVisible = true,
                    PrimaryButtonText = confirmText,
                    IsSecondaryButtonVisible = true,
                    SecondaryButtonText = cancelText
                };

                return ShowDialogInternal(vm, DialogPlacement.CenterOwner, DialogSizeConstraints.Small);
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

        /// <summary>
        /// Infers the appropriate theme from the calling context.
        /// Priority: Active SettingsWindow theme > Global theme
        /// </summary>
        private AppTheme InferThemeFromContext()
        {
            // Check if SettingsWindow is active and use its theme
            var settingsWindow = System.Windows.Application.Current.Windows
                .OfType<SettingsWindow>()
                .FirstOrDefault(w => w.IsActive || w.IsVisible);

            if (settingsWindow?.DataContext is SettingsViewModel settingsVm)
            {
                return settingsVm.SettingsTheme;
            }

            // Fallback to global theme
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
            // Get mouse position
            var mousePos = System.Windows.Forms.Control.MousePosition;
            
            // Get screen working area (excludes taskbar)
            var screen = Screen.FromPoint(mousePos);
            var workingArea = screen.WorkingArea;

            // Offset from cursor (avoid covering cursor)
            const int offsetX = 20;
            const int offsetY = 20;

            double left = mousePos.X + offsetX;
            double top = mousePos.Y + offsetY;

            // Ensure window stays within screen bounds
            if (left + window.Width > workingArea.Right)
                left = workingArea.Right - window.Width;
            
            if (top + window.Height > workingArea.Bottom)
                top = workingArea.Bottom - window.Height;

            if (left < workingArea.Left)
                left = workingArea.Left;
            
            if (top < workingArea.Top)
                top = workingArea.Top;

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
            WindowBackdropType backdrop = WindowBackdropType.Mica)
        {
            // 1. Apply theme (inferred from context)
            var theme = InferThemeFromContext();
            _themeService.ApplyTheme(window, theme, backdrop, updateGlobal: false);

            // 2. Apply size constraints
            ApplySizeConstraints(window, sizeConstraints ?? DialogSizeConstraints.Default);

            // 3. Find best owner and apply placement
            var owner = FindBestOwner(placement);
            ApplyPlacement(window, placement, owner);
        }

        private Pulsar.Models.Enums.DialogResult ShowDialogInternal(
            DialogHostViewModel viewModel,
            DialogPlacement placement = DialogPlacement.CenterOwner,
            DialogSizeConstraints? sizeConstraints = null,
            WindowBackdropType backdrop = WindowBackdropType.Mica)
        {
            // Create window via DI to inject dependencies like IThemeService
            var window = ActivatorUtilities.CreateInstance<DialogHostWindow>(_serviceProvider);
            
            window.DataContext = viewModel;

            // Handle Close Request from VM
            Pulsar.Models.Enums.DialogResult result = Pulsar.Models.Enums.DialogResult.None;
            viewModel.RequestClose = (r) =>
            {
                result = r;
                window.Close();
            };

            // Apply all window configurations (theme, placement, size)
            PrepareWindow(window, placement, sizeConstraints, backdrop);

            window.ShowDialog();

            return result;
        }

        public async Task<string?> ShowColorPickerAsync(string title, string initialColor = "#FF0000")
        {
            return await RunOnUi(() =>
            {
                var colorPickerVm = new ViewModels.Dialogs.ColorPickerViewModel(initialColor);
                
                var vm = new DialogHostViewModel
                {
                    Title = title,
                    Content = colorPickerVm,
                    IsPrimaryButtonVisible = true,
                    PrimaryButtonText = "Select",
                    IsSecondaryButtonVisible = true,
                    SecondaryButtonText = "Cancel"
                };

                colorPickerVm.RequestClose = (result) => vm.CloseCommand.Execute(result);
                vm.IsScrollable = false;

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
