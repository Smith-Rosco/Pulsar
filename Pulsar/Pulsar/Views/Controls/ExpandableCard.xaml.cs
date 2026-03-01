using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pulsar.Views.Controls
{
    /// <summary>
    /// Reusable expandable card control with icon, title, actions, and customizable content.
    /// Used across Settings pages (Plugins, Slots) for consistent UX.
    /// </summary>
    public partial class ExpandableCard : UserControl
    {
        #region Dependency Properties

        // Icon
        public static readonly DependencyProperty IconKeyProperty =
            DependencyProperty.Register(nameof(IconKey), typeof(string), typeof(ExpandableCard), new PropertyMetadata(string.Empty));

        // Title and Subtitle
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ExpandableCard), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(ExpandableCard), new PropertyMetadata(string.Empty));

        // Header Content (additional content in header, e.g., badges, progress bars)
        public static readonly DependencyProperty HeaderContentProperty =
            DependencyProperty.Register(nameof(HeaderContent), typeof(object), typeof(ExpandableCard), new PropertyMetadata(null));

        public static readonly DependencyProperty HeaderContentTemplateProperty =
            DependencyProperty.Register(nameof(HeaderContentTemplate), typeof(DataTemplate), typeof(ExpandableCard), new PropertyMetadata(null));

        // Expanded Content
        public static readonly DependencyProperty ExpandedContentProperty =
            DependencyProperty.Register(nameof(ExpandedContent), typeof(object), typeof(ExpandableCard), new PropertyMetadata(null));

        public static readonly DependencyProperty ExpandedContentTemplateProperty =
            DependencyProperty.Register(nameof(ExpandedContentTemplate), typeof(DataTemplate), typeof(ExpandableCard), new PropertyMetadata(null));

        // Action Buttons (up to 3 buttons in header)
        public static readonly DependencyProperty PrimaryActionCommandProperty =
            DependencyProperty.Register(nameof(PrimaryActionCommand), typeof(ICommand), typeof(ExpandableCard), new PropertyMetadata(null));

        public static readonly DependencyProperty PrimaryActionIconProperty =
            DependencyProperty.Register(nameof(PrimaryActionIcon), typeof(string), typeof(ExpandableCard), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty PrimaryActionTooltipProperty =
            DependencyProperty.Register(nameof(PrimaryActionTooltip), typeof(string), typeof(ExpandableCard), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty PrimaryActionVisibilityProperty =
            DependencyProperty.Register(nameof(PrimaryActionVisibility), typeof(Visibility), typeof(ExpandableCard), new PropertyMetadata(Visibility.Collapsed));

        public static readonly DependencyProperty SecondaryActionCommandProperty =
            DependencyProperty.Register(nameof(SecondaryActionCommand), typeof(ICommand), typeof(ExpandableCard), new PropertyMetadata(null));

        public static readonly DependencyProperty SecondaryActionIconProperty =
            DependencyProperty.Register(nameof(SecondaryActionIcon), typeof(string), typeof(ExpandableCard), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SecondaryActionTooltipProperty =
            DependencyProperty.Register(nameof(SecondaryActionTooltip), typeof(string), typeof(ExpandableCard), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SecondaryActionVisibilityProperty =
            DependencyProperty.Register(nameof(SecondaryActionVisibility), typeof(Visibility), typeof(ExpandableCard), new PropertyMetadata(Visibility.Collapsed));

        // Toggle Switch
        public static readonly DependencyProperty IsToggleEnabledProperty =
            DependencyProperty.Register(nameof(IsToggleEnabled), typeof(bool), typeof(ExpandableCard), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty IsToggleVisibleProperty =
            DependencyProperty.Register(nameof(IsToggleVisible), typeof(bool), typeof(ExpandableCard), new PropertyMetadata(true));

        public static readonly DependencyProperty CanToggleProperty =
            DependencyProperty.Register(nameof(CanToggle), typeof(bool), typeof(ExpandableCard), new PropertyMetadata(true));

        // Context Menu
        public static readonly DependencyProperty CardContextMenuProperty =
            DependencyProperty.Register(nameof(CardContextMenu), typeof(ContextMenu), typeof(ExpandableCard), new PropertyMetadata(null));

        // Expansion State
        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(ExpandableCard), new PropertyMetadata(false));

        #endregion

        #region Properties

        public string IconKey
        {
            get => (string)GetValue(IconKeyProperty);
            set => SetValue(IconKeyProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public object HeaderContent
        {
            get => GetValue(HeaderContentProperty);
            set => SetValue(HeaderContentProperty, value);
        }

        public DataTemplate HeaderContentTemplate
        {
            get => (DataTemplate)GetValue(HeaderContentTemplateProperty);
            set => SetValue(HeaderContentTemplateProperty, value);
        }

        public object ExpandedContent
        {
            get => GetValue(ExpandedContentProperty);
            set => SetValue(ExpandedContentProperty, value);
        }

        public DataTemplate ExpandedContentTemplate
        {
            get => (DataTemplate)GetValue(ExpandedContentTemplateProperty);
            set => SetValue(ExpandedContentTemplateProperty, value);
        }

        public ICommand PrimaryActionCommand
        {
            get => (ICommand)GetValue(PrimaryActionCommandProperty);
            set => SetValue(PrimaryActionCommandProperty, value);
        }

        public string PrimaryActionIcon
        {
            get => (string)GetValue(PrimaryActionIconProperty);
            set => SetValue(PrimaryActionIconProperty, value);
        }

        public string PrimaryActionTooltip
        {
            get => (string)GetValue(PrimaryActionTooltipProperty);
            set => SetValue(PrimaryActionTooltipProperty, value);
        }

        public Visibility PrimaryActionVisibility
        {
            get => (Visibility)GetValue(PrimaryActionVisibilityProperty);
            set => SetValue(PrimaryActionVisibilityProperty, value);
        }

        public ICommand SecondaryActionCommand
        {
            get => (ICommand)GetValue(SecondaryActionCommandProperty);
            set => SetValue(SecondaryActionCommandProperty, value);
        }

        public string SecondaryActionIcon
        {
            get => (string)GetValue(SecondaryActionIconProperty);
            set => SetValue(SecondaryActionIconProperty, value);
        }

        public string SecondaryActionTooltip
        {
            get => (string)GetValue(SecondaryActionTooltipProperty);
            set => SetValue(SecondaryActionTooltipProperty, value);
        }

        public Visibility SecondaryActionVisibility
        {
            get => (Visibility)GetValue(SecondaryActionVisibilityProperty);
            set => SetValue(SecondaryActionVisibilityProperty, value);
        }

        public bool IsToggleEnabled
        {
            get => (bool)GetValue(IsToggleEnabledProperty);
            set => SetValue(IsToggleEnabledProperty, value);
        }

        public bool IsToggleVisible
        {
            get => (bool)GetValue(IsToggleVisibleProperty);
            set => SetValue(IsToggleVisibleProperty, value);
        }

        public bool CanToggle
        {
            get => (bool)GetValue(CanToggleProperty);
            set => SetValue(CanToggleProperty, value);
        }

        public ContextMenu CardContextMenu
        {
            get => (ContextMenu)GetValue(CardContextMenuProperty);
            set => SetValue(CardContextMenuProperty, value);
        }

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        #endregion

        public ExpandableCard()
        {
            InitializeComponent();
        }
    }
}
