using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Views.Controls
{
    public partial class HotkeyBox : UserControl
    {
        public static readonly DependencyProperty HotkeyProperty =
            DependencyProperty.Register(nameof(Hotkey), typeof(HotkeyConfig), typeof(HotkeyBox),
                new FrameworkPropertyMetadata(new HotkeyConfig(), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged));

        public static readonly DependencyProperty ValidationResultProperty =
            DependencyProperty.Register(nameof(ValidationResult), typeof(HotkeyValidationResult), typeof(HotkeyBox),
                new PropertyMetadata(null, OnValidationResultChanged));

        public static readonly DependencyProperty ActionIdProperty =
            DependencyProperty.Register(nameof(ActionId), typeof(string), typeof(HotkeyBox),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(HotkeyBox),
                new PropertyMetadata("(None)"));

        public static readonly RoutedEvent HotkeyChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(HotkeyChanged), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(HotkeyBox));

        public HotkeyConfig Hotkey
        {
            get => (HotkeyConfig)GetValue(HotkeyProperty);
            set => SetValue(HotkeyProperty, value);
        }

        public HotkeyValidationResult? ValidationResult
        {
            get => (HotkeyValidationResult?)GetValue(ValidationResultProperty);
            set => SetValue(ValidationResultProperty, value);
        }

        public string ActionId
        {
            get => (string)GetValue(ActionIdProperty);
            set => SetValue(ActionIdProperty, value);
        }

        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        public event RoutedEventHandler HotkeyChanged
        {
            add => AddHandler(HotkeyChangedEvent, value);
            remove => RemoveHandler(HotkeyChangedEvent, value);
        }

        public string HotkeyText
        {
            get => (string)GetValue(HotkeyTextProperty);
            set => SetValue(HotkeyTextProperty, value);
        }

        public static readonly DependencyProperty HotkeyTextProperty =
            DependencyProperty.Register(nameof(HotkeyText), typeof(string), typeof(HotkeyBox),
                new PropertyMetadata(string.Empty));

        public Brush BorderBrushColor
        {
            get => (Brush)GetValue(BorderBrushColorProperty);
            set => SetValue(BorderBrushColorProperty, value);
        }

        public static readonly DependencyProperty BorderBrushColorProperty =
            DependencyProperty.Register(nameof(BorderBrushColor), typeof(Brush), typeof(HotkeyBox),
                new PropertyMetadata(Brushes.Transparent));

        public string ConflictTooltipText
        {
            get => (string)GetValue(ConflictTooltipTextProperty);
            set => SetValue(ConflictTooltipTextProperty, value);
        }

        public static readonly DependencyProperty ConflictTooltipTextProperty =
            DependencyProperty.Register(nameof(ConflictTooltipText), typeof(string), typeof(HotkeyBox),
                new PropertyMetadata(string.Empty));

        private IHotkeyService? _hotkeyService;

        public HotkeyBox()
        {
            InitializeComponent();
            HotkeyText = string.Empty;
            BorderBrushColor = Brushes.Transparent;
        }

        private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = (HotkeyBox)d;
            box.UpdateDisplay();
        }

        private static void OnValidationResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var box = (HotkeyBox)d;
            box.UpdateValidationFeedback();
        }

        private void UpdateDisplay()
        {
            if (Hotkey == null || Hotkey.IsEmpty)
            {
                HotkeyText = PlaceholderText;
            }
            else
            {
                HotkeyText = Hotkey.DisplayText;
            }
        }

        private void UpdateValidationFeedback()
        {
            var vr = ValidationResult;
            if (vr == null || (!vr.HasIssues && !vr.IsEmpty))
            {
                ConflictBadge.Visibility = Visibility.Collapsed;
                BorderBrushColor = Brushes.Transparent;
                ConflictTooltipText = string.Empty;
                return;
            }

            if (vr.IsEmpty)
            {
                ConflictBadge.Visibility = Visibility.Collapsed;
                BorderBrushColor = Brushes.Transparent;
                ConflictTooltipText = string.Empty;
                return;
            }

            if (vr.IsSystemReserved)
            {
                ConflictBadge.Visibility = Visibility.Visible;
                BorderBrushColor = new SolidColorBrush(Color.FromRgb(255, 191, 0));
                ConflictTooltipText = "This combination is reserved by Windows";
            }
            else if (vr.Conflicts.Count > 0)
            {
                ConflictBadge.Visibility = Visibility.Visible;
                BorderBrushColor = new SolidColorBrush(Color.FromRgb(255, 0, 0));
                ConflictTooltipText = $"Conflict: already assigned to \"{vr.Conflicts[0].ConflictingActionId}\"";
            }
            else
            {
                ConflictBadge.Visibility = Visibility.Collapsed;
                BorderBrushColor = Brushes.Transparent;
                ConflictTooltipText = string.Empty;
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            // Handle clear keys
            if (e.Key == Key.Back || e.Key == Key.Delete || e.Key == Key.Escape)
            {
                Hotkey = new HotkeyConfig();
                RaiseHotkeyChanged();
                return;
            }

            // Ignore modifier-only presses
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LWin || e.Key == Key.RWin ||
                e.Key == Key.System)
            {
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            var mods = new List<string>();
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods.Add("Control");
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods.Add("Shift");
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods.Add("Alt");
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) mods.Add("Windows");

            Hotkey = new HotkeyConfig
            {
                Key = key.ToString(),
                Modifiers = string.Join(",", mods)
            };

            RaiseHotkeyChanged();
        }

        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            _hotkeyService ??= App.Current.Services.GetService<IHotkeyService>();
            _hotkeyService?.Pause();
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            _hotkeyService?.Resume();
        }

        private void RaiseHotkeyChanged()
        {
            RaiseEvent(new RoutedEventArgs(HotkeyChangedEvent));
        }
    }
}
