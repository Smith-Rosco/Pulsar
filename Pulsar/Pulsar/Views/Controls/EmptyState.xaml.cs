using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace Pulsar.Views.Controls
{
    public partial class EmptyState : UserControl
    {
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(SymbolRegular?), typeof(EmptyState), new PropertyMetadata(null));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty HintProperty =
            DependencyProperty.Register(nameof(Hint), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ActionTextProperty =
            DependencyProperty.Register(nameof(ActionText), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ActionIconProperty =
            DependencyProperty.Register(nameof(ActionIcon), typeof(SymbolRegular?), typeof(EmptyState), new PropertyMetadata(null));

        public static readonly DependencyProperty ActionCommandProperty =
            DependencyProperty.Register(nameof(ActionCommand), typeof(ICommand), typeof(EmptyState), new PropertyMetadata(null));

        public static readonly DependencyProperty ActionButtonStyleProperty =
            DependencyProperty.Register(nameof(ActionButtonStyle), typeof(Style), typeof(EmptyState), new PropertyMetadata(null));

        public static readonly DependencyProperty ActionVisibilityProperty =
            DependencyProperty.Register(nameof(ActionVisibility), typeof(Visibility), typeof(EmptyState), new PropertyMetadata(Visibility.Visible));

        public static readonly DependencyProperty HasBorderProperty =
            DependencyProperty.Register(nameof(HasBorder), typeof(bool), typeof(EmptyState), new PropertyMetadata(false));

        public EmptyState()
        {
            InitializeComponent();
        }

        public SymbolRegular? Icon
        {
            get => (SymbolRegular?)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Hint
        {
            get => (string)GetValue(HintProperty);
            set => SetValue(HintProperty, value);
        }

        public string ActionText
        {
            get => (string)GetValue(ActionTextProperty);
            set => SetValue(ActionTextProperty, value);
        }

        public SymbolRegular? ActionIcon
        {
            get => (SymbolRegular?)GetValue(ActionIconProperty);
            set => SetValue(ActionIconProperty, value);
        }

        public ICommand? ActionCommand
        {
            get => (ICommand?)GetValue(ActionCommandProperty);
            set => SetValue(ActionCommandProperty, value);
        }

        public Style? ActionButtonStyle
        {
            get => (Style?)GetValue(ActionButtonStyleProperty);
            set => SetValue(ActionButtonStyleProperty, value);
        }

        public Visibility ActionVisibility
        {
            get => (Visibility)GetValue(ActionVisibilityProperty);
            set => SetValue(ActionVisibilityProperty, value);
        }

        public bool HasBorder
        {
            get => (bool)GetValue(HasBorderProperty);
            set => SetValue(HasBorderProperty, value);
        }
    }
}
