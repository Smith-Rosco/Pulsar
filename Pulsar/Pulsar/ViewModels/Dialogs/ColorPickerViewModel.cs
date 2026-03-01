using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.ViewModels.Base;

namespace Pulsar.ViewModels.Dialogs
{
    /// <summary>
    /// ViewModel for color picker dialog.
    /// Replaces the legacy ColorPickerDialog window.
    /// </summary>
    public partial class ColorPickerViewModel : ObservableObject, IDialogViewModel
    {
        private bool _isUpdating;

        [ObservableProperty]
        private byte _r;

        [ObservableProperty]
        private byte _g;

        [ObservableProperty]
        private byte _b;

        [ObservableProperty]
        private System.Windows.Media.Color _currentColor;

        [ObservableProperty]
        private string _hexInput = string.Empty;

        [ObservableProperty]
        private bool _isHexError;

        public ObservableCollection<PresetItem> Presets { get; } = new();

        public Action<Pulsar.Models.Enums.DialogResult>? RequestClose { get; set; }

        public bool IsScrollable => false;

        public string SelectedHex => CurrentColor.ToString();

        public ColorPickerViewModel(string initialHex = "")
        {
            InitializePresets();

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

            // Initialize RGB from current color
            _isUpdating = true;
            R = CurrentColor.R;
            G = CurrentColor.G;
            B = CurrentColor.B;
            HexInput = CurrentColor.ToString();
            _isUpdating = false;
        }

        partial void OnRChanged(byte value)
        {
            UpdateColorFromRgb();
        }

        partial void OnGChanged(byte value)
        {
            UpdateColorFromRgb();
        }

        partial void OnBChanged(byte value)
        {
            UpdateColorFromRgb();
        }

        partial void OnHexInputChanged(string value)
        {
            if (_isUpdating) return;
            if (string.IsNullOrWhiteSpace(value)) return;

            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
                _isUpdating = true;
                CurrentColor = color;
                R = color.R;
                G = color.G;
                B = color.B;
                IsHexError = false;
                _isUpdating = false;
            }
            catch
            {
                IsHexError = true;
            }
        }

        private void UpdateColorFromRgb()
        {
            if (_isUpdating) return;
            _isUpdating = true;
            CurrentColor = System.Windows.Media.Color.FromRgb(R, G, B);
            HexInput = CurrentColor.ToString();
            _isUpdating = false;
        }

        public void SelectPreset(PresetItem preset)
        {
            CurrentColor = preset.Color;
            _isUpdating = true;
            R = preset.Color.R;
            G = preset.Color.G;
            B = preset.Color.B;
            HexInput = preset.Color.ToString();
            _isUpdating = false;
        }

        [RelayCommand]
        private void SelectPresetCommand(PresetItem preset)
        {
            SelectPreset(preset);
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

        public Task<bool> CanCloseAsync(Pulsar.Models.Enums.DialogResult result)
        {
            return Task.FromResult(true);
        }

        public class PresetItem
        {
            public string Name { get; set; } = "";
            public System.Windows.Media.SolidColorBrush Brush { get; set; } = System.Windows.Media.Brushes.Transparent;
            public System.Windows.Media.Color Color { get; set; }
        }
    }
}
