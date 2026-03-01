using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace Pulsar.Views.Controls
{
    public partial class SettingsBanner : UserControl
    {
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(SettingsBanner), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SeverityProperty =
            DependencyProperty.Register(nameof(Severity), typeof(ControlAppearance), typeof(SettingsBanner), new PropertyMetadata(ControlAppearance.Secondary));

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(SettingsBanner), new PropertyMetadata(false));

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public ControlAppearance Severity
        {
            get => (ControlAppearance)GetValue(SeverityProperty);
            set => SetValue(SeverityProperty, value);
        }

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public SettingsBanner()
        {
            InitializeComponent();
        }
    }
}
