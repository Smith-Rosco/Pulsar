using CommunityToolkit.Mvvm.ComponentModel;
using Pulsar.ViewModels.Strategies;
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

        /// <summary>
        /// Holds the underlying data (ProcessWindowInfo, PluginSlot, or List<ProcessWindowInfo>)
        /// </summary>
        public object? DataContext { get; set; }

        /// <summary>
        /// Identifies the type of data held
        /// </summary>
        public SlotType Type { get; set; } = SlotType.None;

        // [New] Magnetic Animation Offset
        [ObservableProperty]
        private double _offsetX;

        [ObservableProperty]
        private double _offsetY;

        // [New] Physics Velocity for Spring Animation
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
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