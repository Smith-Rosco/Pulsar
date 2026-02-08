using Wpf.Ui.Controls;

namespace Pulsar.Core.Messages
{
    public class SnackbarMessage
    {
        public string Title { get; }
        public string Content { get; }
        public ControlAppearance Appearance { get; }
        public SymbolRegular Icon { get; }

        public SnackbarMessage(string title, string content, 
                               ControlAppearance appearance = ControlAppearance.Secondary,
                               SymbolRegular icon = SymbolRegular.Info24)
        {
            Title = title;
            Content = content;
            Appearance = appearance;
            Icon = icon;
        }
    }
}
