using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.ViewModels.Strategies;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.ViewModels
{
    public partial class SlotViewModel : ObservableObject
    {
        private int _slotIndex;
        private double _x;
        private double _y;
        private double _size;
        private string _label = string.Empty;
        private bool _isActive;
        private string _iconKey = string.Empty;
        private readonly ILocalizationService? _loc;

        // [New] Strategy Pattern
        public IActionStrategy ActionStrategy { get; set; } = new NoOpStrategy();
        
        public async Task ExecuteAsync(RadialMenuViewModel context, CancellationToken cancellationToken = default)
        {
            if (ActionStrategy != null)
            {
                await ActionStrategy.ExecuteAsync(this, context, cancellationToken);
            }
        }

        public SlotViewModel(int index, double x, double y, double size, ILocalizationService? localizationService = null)
        {
            SlotIndex = index;
            X = x;
            Y = y;
            Size = size;
            _loc = localizationService;
            HealthBadgeText = _loc?["Slot.Ready"] ?? "Ready";
        }

        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        public double Size
        {
            get => _size;
            set
            {
                if (SetProperty(ref _size, value))
                {
                    OnPropertyChanged(nameof(ShowTypeBadge));
                }
            }
        }

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public int SlotIndex
        {
            get => _slotIndex;
            set => SetProperty(ref _slotIndex, value);
        }

        public string IconKey
        {
            get => _iconKey;
            set => SetProperty(ref _iconKey, value);
        }

        // [New] Helper for Badge Visibility
        public bool HasBadge => BadgeCount > 1;

        public bool ShowTypeBadge => !string.IsNullOrWhiteSpace(TypeBadge) && Size >= 52;

        public void LoadIconData(string iconKey)
        {
            IconKey = iconKey;
            IconImage = null; // Clear direct image if using key
        }

        [ObservableProperty]
        private bool _isRecommended;
        
        // [New] Badge Count for Multi-Window Slots
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasBadge))] // [Fix] Ensure UI updates when BadgeCount changes
        private int _badgeCount;

        // === [New] Context Data ===
        
        [ObservableProperty]
        private System.Windows.Media.ImageSource? _iconImage;

        // [New] Custom Color for Slot Background/Border
        [ObservableProperty]
        private System.Windows.Media.Brush? _customFillBrush;

        [ObservableProperty]
        private System.Windows.Media.Brush? _customStrokeBrush;

        [ObservableProperty]
        private System.Windows.Media.Brush? _customForegroundBrush;

        [ObservableProperty]
        private string _typeBadge = string.Empty;

        [ObservableProperty]
        private string _typeToneKey = "SlotTypeBrushDefault";

        [ObservableProperty]
        private string _healthBadgeText = "Ready";

        [ObservableProperty]
        private string _healthToneKey = "SlotHealthBrushReady";

        public void SetColor(string? hexColor)
        {
            if (string.IsNullOrWhiteSpace(hexColor))
            {
                CustomFillBrush = null;
                CustomStrokeBrush = null;
                CustomForegroundBrush = null;
                return;
            }

            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
                
                // 1. Fill: Semi-transparent (Glassy look)
                var fillBrush = new System.Windows.Media.SolidColorBrush(color);
                fillBrush.Opacity = 0.25; // Enhance visibility while keeping transparency
                fillBrush.Freeze();
                CustomFillBrush = fillBrush;

                // 2. Stroke: Solid (High contrast border)
                var strokeBrush = new System.Windows.Media.SolidColorBrush(color);
                strokeBrush.Opacity = 0.9;
                strokeBrush.Freeze();
                CustomStrokeBrush = strokeBrush;

                // 3. Foreground: Adaptive Contrast
                // Luminance formula: 0.299R + 0.587G + 0.114B
                double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
                
                // If background is bright (lum > 0.5), use Black text. Otherwise White.
                // Note: Since our fill is only 25% opacity, the background color matters too.
                // Assuming dark theme background, we mainly care if the stroke/fill makes it too bright.
                // A simple heuristic: if the custom color is very bright, use black to stand out against the glow/stroke.
                var foreColor = luminance > 0.7 ? System.Windows.Media.Colors.Black : System.Windows.Media.Colors.White;
                var foreBrush = new System.Windows.Media.SolidColorBrush(foreColor);
                foreBrush.Freeze();
                CustomForegroundBrush = foreBrush;
            }
            catch
            {
                CustomFillBrush = null;
                CustomStrokeBrush = null;
                CustomForegroundBrush = null;
            }
        }

        public void ApplyPresentation(SlotPresentation? presentation)
        {
            var resolved = presentation ?? SlotPresentation.Empty;
            TypeBadge = resolved.TypeBadge;
            TypeToneKey = resolved.TypeToneKey;
            HealthBadgeText = resolved.HealthBadgeText;
            HealthToneKey = resolved.HealthToneKey;
            SetColor(resolved.ColorHex);
            OnPropertyChanged(nameof(ShowTypeBadge));
        }

        public void ClearPresentation()
        {
            ApplyPresentation(SlotPresentation.Empty);
        }

        /// <summary>
        /// Holds the underlying data (ProcessWindowInfo, PluginSlot, or List<ProcessWindowInfo>)
        /// </summary>
        public object? DataContext { get; set; }

        /// <summary>
        /// Identifies the type of data held
        /// </summary>
        public SlotType Type { get; set; } = SlotType.None;

        // [New] Animation Properties (Entrance & Physics)
        [ObservableProperty]
        private double _currentScale = 0.0; // Start invisible
        
        [ObservableProperty]
        private double _currentOpacity = 0.0;

        // [New] Enabled State for Ghost Slots
        [ObservableProperty]
        private bool _isEnabled = true;

        private const double MagneticSmoothFactor = 0.22;

        // [New] Combined Offset (Magnetic only) - Bound to XAML
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RenderOffsetX))]
        private double _offsetX;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RenderOffsetY))]
        private double _offsetY;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RenderOffsetX))]
        private double _animationOffsetX;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RenderOffsetY))]
        private double _animationOffsetY;

        public double RenderOffsetX => OffsetX + AnimationOffsetX;
        public double RenderOffsetY => OffsetY + AnimationOffsetY;

        public void ResetAnimation()
        {
            CurrentScale = 1.0;   // [Fix] Start fully visible for instant response
            CurrentOpacity = 1.0; // [Fix] Start fully visible
            OffsetX = 0;
            OffsetY = 0;
            AnimationOffsetX = 0;
            AnimationOffsetY = 0;
        }

        public void UpdateMagneticOffset(double desiredOffsetX, double desiredOffsetY)
        {
            var nextOffsetX = OffsetX + (desiredOffsetX - OffsetX) * MagneticSmoothFactor;
            var nextOffsetY = OffsetY + (desiredOffsetY - OffsetY) * MagneticSmoothFactor;

            if (Math.Abs(desiredOffsetX - nextOffsetX) < 0.05)
            {
                nextOffsetX = desiredOffsetX;
            }

            if (Math.Abs(desiredOffsetY - nextOffsetY) < 0.05)
            {
                nextOffsetY = desiredOffsetY;
            }

            OffsetX = nextOffsetX;
            OffsetY = nextOffsetY;
        }
    }

    public enum SlotType
    {
        None,
        Action,     // Standard plugin action
        Process,    // A running process (Root level of Switcher) - Holds List<ProcessWindowInfo>
        Window,     // A specific window instance (Sub level) - Holds ProcessWindowInfo
        Folder      // A folder of actions
    }
}
