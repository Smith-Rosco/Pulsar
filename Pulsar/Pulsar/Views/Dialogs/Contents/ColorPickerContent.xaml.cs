using System.Windows;
using System.Windows.Controls;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.Views.Dialogs.Contents
{
    public partial class ColorPickerContent : UserControl
    {
        public ColorPickerContent()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Bridge: relay DataContext to ItemsControl.Tag so buttons inside
            // DataTemplate can bind Command via Tag without RelativeSource
            // crossing the UserControl visual tree boundary.
            if (FindName("PresetsItemsControl") is ItemsControl itemsControl)
            {
                if (DataContext is ColorPickerViewModel vm)
                    itemsControl.Tag = vm;

                DataContextChanged += (_, _) =>
                {
                    if (DataContext is ColorPickerViewModel vm2)
                        itemsControl.Tag = vm2;
                };
            }
        }
    }
}
