using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.ViewModels.Strategies;
using System;
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

        // [New] Strategy Pattern
        public IActionStrategy ActionStrategy { get; set; } = new NoOpStrategy();
        
        public async Task ExecuteAsync(RadialMenuViewModel context)
        {
            if (ActionStrategy != null)
            {
                await ActionStrategy.ExecuteAsync(this, context);
            }
        }

        public SlotViewModel(int index, double x, double y, double size)
        {
            SlotIndex = index;
            X = x;
            Y = y;
            Size = size;
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
            set => SetProperty(ref _size, value);
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

        // [New] Magnetic Animation Offset (from physics)
        private double _magneticOffsetX;
        private double _magneticOffsetY;

        // [New] Combined Offset (Magnetic only) - Bound to XAML
        [ObservableProperty]
        private double _offsetX;

        [ObservableProperty]
        private double _offsetY;

        // [New] Physics Logic
        private const double Stiffness = 0.2;  // Spring constant (How strong is the spring?)
        private const double Damping = 0.75;   // Friction (How fast does it stop?)
        
        // Target offset driven by Mouse Magnetism
        public double TargetOffsetX { get; set; }
        public double TargetOffsetY { get; set; }

        // [New] Physics Velocity for Spring Animation
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }

        public void ResetAnimation()
        {
            CurrentScale = 1.0;   // [Fix] Start fully visible for instant response
            CurrentOpacity = 1.0; // [Fix] Start fully visible
            _magneticOffsetX = 0;
            _magneticOffsetY = 0;
            OffsetX = 0;
            OffsetY = 0;
            VelocityX = 0;
            VelocityY = 0;
            TargetOffsetX = 0;
            TargetOffsetY = 0;
        }

        public void UpdatePhysics()
        {
            // 1. Spring Force (Hooke's Law: F = -k * x)
            // Calculate force pulling towards TargetOffset
            double forceX = (TargetOffsetX - _magneticOffsetX) * Stiffness;
            double forceY = (TargetOffsetY - _magneticOffsetY) * Stiffness;

            // 2. Apply Force to Velocity
            VelocityX += forceX;
            VelocityY += forceY;

            // 3. Apply Damping (Friction)
            VelocityX *= Damping;
            VelocityY *= Damping;

            // 4. Update Position
            _magneticOffsetX += VelocityX;
            _magneticOffsetY += VelocityY;

            // Snap to zero if very close to stop jitter
            if (Math.Abs(VelocityX) < 0.01 && Math.Abs(TargetOffsetX - _magneticOffsetX) < 0.01) _magneticOffsetX = TargetOffsetX;
            if (Math.Abs(VelocityY) < 0.01 && Math.Abs(TargetOffsetY - _magneticOffsetY) < 0.01) _magneticOffsetY = TargetOffsetY;

            // 5. Set final offset (magnetic only)
            OffsetX = _magneticOffsetX;
            OffsetY = _magneticOffsetY;
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