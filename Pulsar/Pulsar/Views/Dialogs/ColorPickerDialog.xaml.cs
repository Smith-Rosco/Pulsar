using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pulsar.Views.Dialogs
{
    public partial class ColorPickerDialog : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private bool _isUpdating;

        private byte _r;
        public byte R
        {
            get => _r;
            set
            {
                if (_r != value)
                {
                    _r = value;
                    OnPropertyChanged(nameof(R));
                    UpdateColorFromRgb();
                }
            }
        }

        private byte _g;
        public byte G
        {
            get => _g;
            set
            {
                if (_g != value)
                {
                    _g = value;
                    OnPropertyChanged(nameof(G));
                    UpdateColorFromRgb();
                }
            }
        }

        private byte _b;
        public byte B
        {
            get => _b;
            set
            {
                if (_b != value)
                {
                    _b = value;
                    OnPropertyChanged(nameof(B));
                    UpdateColorFromRgb();
                }
            }
        }

        private System.Windows.Media.Color _currentColor;
        public System.Windows.Media.Color CurrentColor
        {
            get => _currentColor;
            set
            {
                if (_currentColor != value)
                {
                    _currentColor = value;
                    OnPropertyChanged(nameof(CurrentColor));
                    if (!_isUpdating)
                    {
                        _isUpdating = true;
                        R = _currentColor.R;
                        G = _currentColor.G;
                        B = _currentColor.B;
                        if (HexInput != null) HexInput.Text = _currentColor.ToString();
                        _isUpdating = false;
                    }
                }
            }
        }

        public string SelectedHex => CurrentColor.ToString();

        public class PresetItem
        {
            public string Name { get; set; } = "";
            public System.Windows.Media.SolidColorBrush Brush { get; set; } = System.Windows.Media.Brushes.Transparent;
            public System.Windows.Media.Color Color { get; set; }
        }

        public List<PresetItem> Presets { get; } = new List<PresetItem>();

        public ColorPickerDialog(string initialHex = "")
        {
            InitializeComponent();
            DataContext = this;

            InitializePresets();
            // Wait for UI to be ready or just set it
            // Need to check if PresetList is accessible (it should be partial)
            if (PresetList != null) PresetList.ItemsSource = Presets;

            if (!string.IsNullOrEmpty(initialHex))
            {
                try
                {
                    CurrentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(initialHex);
                }
                catch
                {
                    CurrentColor = System.Windows.Media.Colors.Red;
                }
            }
            else
            {
                CurrentColor = System.Windows.Media.Colors.Red;
            }
            
            this.Loaded += (s, e) => 
            {
                if (PresetList != null) PresetList.ItemsSource = Presets;
            };
        }

        private void InitializePresets()
        {
            // Standard Pulsar Palette
            AddPreset("Red", "#FF0000");
            AddPreset("Orange", "#FFA500");
            AddPreset("Yellow", "#FFD700");
            AddPreset("Green", "#008000");
            AddPreset("Blue", "#0000FF");
            AddPreset("Purple", "#800080");
            AddPreset("Pink", "#FFC0CB");
            AddPreset("Teal", "#008080");
            AddPreset("Cyan", "#00FFFF");
            AddPreset("Black", "#000000");
            AddPreset("White", "#FFFFFF");
            AddPreset("Gray", "#808080");
            
            // Modern UI Colors
            AddPreset("Crimson", "#DC143C");
            AddPreset("DeepSkyBlue", "#00BFFF");
            AddPreset("LimeGreen", "#32CD32");
            AddPreset("Gold", "#FFD700");
            AddPreset("Tomato", "#FF6347");
            AddPreset("SlateBlue", "#6A5ACD");
        }

        private void AddPreset(string name, string hex)
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                Presets.Add(new PresetItem 
                { 
                    Name = name, 
                    Color = color, 
                    Brush = new System.Windows.Media.SolidColorBrush(color) 
                });
            }
            catch { }
        }

        private void UpdateColorFromRgb()
        {
            if (_isUpdating) return;
            _isUpdating = true;
            CurrentColor = System.Windows.Media.Color.FromRgb(R, G, B);
            if (HexInput != null) HexInput.Text = CurrentColor.ToString();
            _isUpdating = false;
        }

        private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;

            if (HexInput == null) return;
            string text = HexInput.Text;
            if (string.IsNullOrWhiteSpace(text)) return;

            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(text);
                _isUpdating = true;
                CurrentColor = color;
                R = color.R;
                G = color.G;
                B = color.B;
                if (ErrorText != null) ErrorText.Visibility = Visibility.Collapsed;
                _isUpdating = false;
            }
            catch
            {
                if (ErrorText != null) ErrorText.Visibility = Visibility.Visible;
            }
        }

        private void PresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is PresetItem item)
            {
                CurrentColor = item.Color;
            }
        }

        private void Select_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
