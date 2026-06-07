using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Pulsar.Helpers;

namespace Pulsar.Views.Controls
{
    public partial class IconSelector : UserControl
    {
        public static readonly DependencyProperty IconKeyProperty =
            DependencyProperty.Register(nameof(IconKey), typeof(string), typeof(IconSelector),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIconKeyChanged));

        private static readonly DependencyPropertyKey DisplayTextPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(DisplayText), typeof(string), typeof(IconSelector),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DisplayTextProperty = DisplayTextPropertyKey.DependencyProperty;

        public string IconKey
        {
            get => (string)GetValue(IconKeyProperty);
            set => SetValue(IconKeyProperty, value);
        }

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            private set => SetValue(DisplayTextPropertyKey, value);
        }

        public event EventHandler? BrowseClicked;

        public IconSelector()
        {
            InitializeComponent();
        }

        private static void OnIconKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var selector = (IconSelector)d;
            selector.DisplayText = IconHelper.ResolveIconDisplay(e.NewValue as string);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                var text = Clipboard.GetText();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    var normalized = IconHelper.NormalizeIconKey(text);
                    SetCurrentValue(IconKeyProperty, normalized);
                    e.Handled = true;
                    return;
                }
            }
            base.OnKeyDown(e);
        }

        private void Orb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BrowseClicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            BrowseClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
