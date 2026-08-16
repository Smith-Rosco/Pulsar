using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.Models.Enums;
using Wpf.Ui.Controls;

namespace Pulsar.Views.Dialogs
{
    public partial class DialogHostWindow : FluentWindow
    {
        // Dependency property for ShowMaximize
        public static readonly DependencyProperty ShowMaximizeButtonProperty =
            DependencyProperty.Register(
                nameof(ShowMaximizeButton),
                typeof(bool),
                typeof(DialogHostWindow),
                new PropertyMetadata(true));

        public bool ShowMaximizeButton
        {
            get => (bool)GetValue(ShowMaximizeButtonProperty);
            set => SetValue(ShowMaximizeButtonProperty, value);
        }

        public DialogHostWindow()
        {
            InitializeComponent();

            // Esc cancels the dialog. KeyDown is intentionally used (not
            // PreviewKeyDown) so controls such as HotkeyBox can keep their own
            // Escape behavior (clear hotkey) by marking the event handled first.
            KeyDown += OnKeyDown;

            // Prevent accidental maximization
            StateChanged += OnStateChanged;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            if (DataContext is DialogHostViewModel viewModel)
            {
                viewModel.CancelFromKeyboard();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Configures window resize behavior and title bar buttons.
        /// Called by DialogService after window creation.
        /// </summary>
        public void ConfigureResizeBehavior(bool allowResize, bool showMaximizeButton)
        {
            // Set ResizeMode based on configuration
            ResizeMode = allowResize ? ResizeMode.CanResize : ResizeMode.NoResize;
            
            // Set the dependency property for binding
            ShowMaximizeButton = showMaximizeButton;
        }

        /// <summary>
        /// Prevents window from entering Maximized state if not allowed.
        /// </summary>
        private void OnStateChanged(object? sender, EventArgs e)
        {
            // If window tries to maximize but ResizeMode is NoResize, restore it
            if (WindowState == WindowState.Maximized && ResizeMode == ResizeMode.NoResize)
            {
                WindowState = WindowState.Normal;
            }
        }
    }

    /// <summary>
    /// Converter to check if an object is of a specific type.
    /// </summary>
    public class TypeToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            var targetTypeParam = parameter as Type;
            if (targetTypeParam == null)
                return false;

            return targetTypeParam.IsInstanceOfType(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
