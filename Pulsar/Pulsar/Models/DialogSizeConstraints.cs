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
        /// Small dialog preset (400x300, suitable for simple inputs).
        /// </summary>
        public static DialogSizeConstraints Small => new()
        {
            Width = 400,
            Height = 300,
            MinWidth = 350,
            MinHeight = 250,
            MaxWidth = 500,
            MaxHeight = 400
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
            MaxHeight = 600
        };

        /// <summary>
        /// Large dialog preset (800x600, suitable for complex content).
        /// </summary>
        public static DialogSizeConstraints Large => new()
        {
            Width = 800,
            Height = 600,
            MinWidth = 700,
            MinHeight = 500,
            MaxWidth = 1200,
            MaxHeight = 800
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
            MaxHeight = 800
        };

        /// <summary>
        /// Default preset (uses DialogHostWindow's default size).
        /// </summary>
        public static DialogSizeConstraints Default => new()
        {
            Width = 600,
            Height = 450,
            MinWidth = 400,
            MinHeight = 300
        };
    }
}
