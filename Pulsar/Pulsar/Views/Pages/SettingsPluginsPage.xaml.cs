using Pulsar.ViewModels.Settings;
using Pulsar.Services.Interfaces;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Pulsar.Views.Pages
{
    public partial class SettingsPluginsPage : Page
    {
        public SettingsPluginsPage(PluginManagerViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Apply theme AFTER InitializeComponent().
            // If applied before, the XAML-defined <Page.Resources> replaces the ResourceDictionary
            // instance and discards injected dictionaries (ControlsDictionaries / ThemesDictionary / Pulsar Theme.*).
            themeService.ApplyTheme(this, themeService.CurrentTheme, updateGlobal: false);
        }

        /// <summary>
        /// Jump to Core Plugins section
        /// </summary>
        private void JumpToCoreButton_Click(object sender, RoutedEventArgs e)
        {
            ScrollToGroup("Core Plugins");
        }

        /// <summary>
        /// Jump to Extension Plugins section
        /// </summary>
        private void JumpToExtensionButton_Click(object sender, RoutedEventArgs e)
        {
            ScrollToGroup("Extension Plugins");
        }

        /// <summary>
        /// Smoothly scrolls to a specific group by name
        /// </summary>
        private void ScrollToGroup(string groupName)
        {
            for (int i = 0; i < PluginsItemsControl.Items.Count; i++)
            {
                var item = PluginsItemsControl.Items[i];
                var container = PluginsItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container == null) continue;

                var expander = FindVisualChild<Expander>(container);
                if (expander?.Tag as string == groupName)
                {
                    // Get the position of the expander
                    var transform = expander.TransformToAncestor(PluginsScroll);
                    var position = transform.Transform(new System.Windows.Point(0, 0));

                    // Animate scroll to position
                    var targetOffset = PluginsScroll.VerticalOffset + position.Y - 10; // 10px padding from top
                    AnimateScroll(PluginsScroll.VerticalOffset, targetOffset);
                    
                    // Add highlight pulse animation
                    HighlightGroupHeader(expander);
                    break;
                }
            }
        }

        /// <summary>
        /// Adds a visual highlight pulse to the group header
        /// </summary>
        private void HighlightGroupHeader(Expander expander)
        {
            // Find the TextBlock header
            var header = FindVisualChild<TextBlock>(expander);
            if (header == null) return;

            // Store original foreground
            var originalBrush = header.Foreground;

            // Create color animation to accent color and back
            var colorAnimation = new ColorAnimation
            {
                To = (System.Windows.Media.Color)System.Windows.Application.Current.Resources["SystemAccentColor"],
                Duration = TimeSpan.FromMilliseconds(200),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(2) // Pulse twice
            };

            // Apply animation to a new SolidColorBrush
            var animatedBrush = new SolidColorBrush(((SolidColorBrush)originalBrush).Color);
            header.Foreground = animatedBrush;
            
            colorAnimation.Completed += (s, e) =>
            {
                // Restore original brush after animation
                header.Foreground = originalBrush;
            };

            animatedBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);
        }

        /// <summary>
        /// Animates smooth scrolling
        /// </summary>
        private void AnimateScroll(double from, double to)
        {
            var scrollAnimator = new ScrollAnimator(PluginsScroll);
            var animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            scrollAnimator.BeginAnimation(ScrollAnimator.ScrollOffsetProperty, animation);
        }

        /// <summary>
        /// Helper method to find visual children of a specific type
        /// </summary>
        private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                    return typedChild;

                var result = FindVisualChild<T>(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        /// <summary>
        /// Helper class to enable ScrollViewer animation
        /// </summary>
        private class ScrollAnimator : Animatable
        {
            private readonly ScrollViewer _scrollViewer;

            public static readonly DependencyProperty ScrollOffsetProperty =
                DependencyProperty.Register("ScrollOffset", typeof(double), typeof(ScrollAnimator),
                    new PropertyMetadata(0.0, OnScrollOffsetChanged));

            public ScrollAnimator(ScrollViewer scrollViewer)
            {
                _scrollViewer = scrollViewer;
            }

            public double ScrollOffset
            {
                get => (double)GetValue(ScrollOffsetProperty);
                set => SetValue(ScrollOffsetProperty, value);
            }

            private static void OnScrollOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            {
                var animator = (ScrollAnimator)d;
                animator._scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
            }

            protected override Freezable CreateInstanceCore()
            {
                return new ScrollAnimator(_scrollViewer);
            }
        }
    }
}
