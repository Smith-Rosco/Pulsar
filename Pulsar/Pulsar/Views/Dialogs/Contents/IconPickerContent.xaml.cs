using System.Windows.Controls;
using System.Windows.Input;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class IconPickerContent : UserControl
    {
        public IconPickerContent()
        {
            InitializeComponent();
            Loaded += (s, e) => SearchBox.Focus();
        }
    }
}
