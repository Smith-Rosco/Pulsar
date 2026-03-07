namespace Pulsar.Models
{
    /// <summary>
    /// Defines size constraints for dialog windows.
    /// </summary>
    public class DialogSizeConstraints
    {
        /// <summary>
        /// Explicit width (null = use default).
        /// </summary>
        public double? Width { get; set; }

        /// <summary>
        /// Explicit height (null = use default).
        /// </summary>
        public double? Height { get; set; }

        /// <summary>
        /// Minimum width constraint.
        /// </summary>
        public double? MinWidth { get; set; }

        /// <summary>
        /// Minimum height constraint.
        /// </summary>
        public double? MinHeight { get; set; }

        /// <summary>
        /// Maximum width constraint.
        /// </summary>
        public double? MaxWidth { get; set; }

        /// <summary>
        /// Maximum height constraint.
        /// </summary>
        public double? MaxHeight { get; set; }

    /// <summary>
    /// If true, window will size to content (ignores Width/Height).
    /// </summary>
    public bool SizeToContent { get; set; }

    /// <summary>
    /// If true, allows user to resize the dialog window.
    /// </summary>
    public bool AllowResize { get; set; }

    /// <summary>
    /// If true, shows maximize/minimize buttons in title bar.
    /// </summary>
    public bool ShowMaximizeButton { get; set; }

    /// <summary>
    /// Extra small dialog preset (350x200, suitable for simple confirmations).
    /// </summary>
    public static DialogSizeConstraints XSmall => new()
    {
        Width = 350,
        Height = 200,
        MinWidth = 320,
        MinHeight = 180,
        MaxWidth = 400,
        MaxHeight = 250,
        AllowResize = false,
        ShowMaximizeButton = false
    };

    /// <summary>
    /// Small dialog preset (380x240, suitable for simple inputs and detailed confirmations).
    /// </summary>
    public static DialogSizeConstraints Small => new()
    {
        Width = 380,
        Height = 240,
        MinWidth = 350,
        MinHeight = 220,
        MaxWidth = 450,
        MaxHeight = 300,
        AllowResize = false,
        ShowMaximizeButton = false
    };

    /// <summary>
    /// Medium dialog preset (600x450, suitable for forms and pickers).
    /// </summary>
    public static DialogSizeConstraints Medium => new()
    {
        Width = 600,
        Height = 450,
        MinWidth = 500,
        MinHeight = 400,
        MaxWidth = 800,
        MaxHeight = 600,
        AllowResize = false,
        ShowMaximizeButton = false
    };

    /// <summary>
    /// Large dialog preset (800x600, suitable for complex content).
    /// Allows resizing but no maximize button.
    /// </summary>
    public static DialogSizeConstraints Large => new()
    {
        Width = 800,
        Height = 600,
        MinWidth = 700,
        MinHeight = 500,
        MaxWidth = 1200,
        MaxHeight = 800,
        AllowResize = true,
        ShowMaximizeButton = false
    };

    /// <summary>
    /// Large resizable dialog preset (800x600, suitable for content with many items).
    /// Allows resizing and maximizing for viewing large lists (e.g., icon picker).
    /// </summary>
    public static DialogSizeConstraints LargeResizable => new()
    {
        Width = 800,
        Height = 600,
        MinWidth = 700,
        MinHeight = 500,
        AllowResize = true,
        ShowMaximizeButton = true
    };

    /// <summary>
    /// Auto-sizing preset (sizes to content with min/max constraints).
    /// </summary>
    public static DialogSizeConstraints Auto => new()
    {
        SizeToContent = true,
        MinWidth = 400,
        MinHeight = 300,
        MaxWidth = 1200,
        MaxHeight = 800,
        AllowResize = false,
        ShowMaximizeButton = false
    };

    /// <summary>
    /// Default preset (uses DialogHostWindow's default size).
    /// </summary>
    public static DialogSizeConstraints Default => new()
    {
        Width = 600,
        Height = 450,
        MinWidth = 400,
        MinHeight = 300,
        AllowResize = false,
        ShowMaximizeButton = false
    };
    }
}
