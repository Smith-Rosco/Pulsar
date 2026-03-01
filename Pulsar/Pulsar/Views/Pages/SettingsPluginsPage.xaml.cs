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
        private string _currentStickyGroup = string.Empty;

        public SettingsPluginsPage(PluginManagerViewModel viewModel, IThemeService themeService)
        {
            // IMPORTANT: Must apply theme BEFORE InitializeComponent() because
            // the XAML contains StaticResource lookups (BasedOn={StaticResource ...}) that
            // require the Wpf.Ui ControlsDictionary to be present in the Page's resources.
            themeService.ApplyTheme(this, themeService.CurrentTheme);

            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>
        /// Handles scroll events to update sticky header
        /// </summary>
        private void PluginsScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateStickyHeader();
        }

        /// <summary>
        /// Updates the sticky header based on current scroll position
        /// </summary>
        private void UpdateStickyHeader()
        {
            if (PluginsItemsControl.Items.Count == 0)
            {
                StickyHeaderOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            var scrollViewer = PluginsScroll;
            var scrollOffset = scrollViewer.VerticalOffset;

            // Find which group is currently at the top
            string? topGroupName = null;
            double minDistance = double.MaxValue;

            for (int i = 0; i < PluginsItemsControl.Items.Count; i++)
            {
                var container = PluginsItemsControl.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (container == null) continue;

                // Find the Expander within the container
                var expander = FindVisualChild<Expander>(container);
                if (expander == null) continue;

                // Get the position of the expander relative to the ScrollViewer
                var transform = expander.TransformToAncestor(scrollViewer);
                var position = transform.Transform(new System.Windows.Point(0, 0));

                // Check if this group is near the top (within 40px threshold)
                if (position.Y <= 40 && position.Y > -expander.ActualHeight)
                {
                    var distance = Math.Abs(position.Y);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        topGroupName = expander.Tag as string;
                    }
                }
            }

            // Update sticky header if group changed
            if (topGroupName != null && topGroupName != _currentStickyGroup)
            {
                _currentStickyGroup = topGroupName;
                StickyHeaderText.Text = topGroupName;
                StickyHeaderOverlay.Visibility = Visibility.Visible;

                // Fade in animation
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                StickyHeaderOverlay.BeginAnimation(OpacityProperty, fadeIn);
            }
            else if (topGroupName == null && _currentStickyGroup != string.Empty)
            {
                // Fade out when no group is at top
                _currentStickyGroup = string.Empty;
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                fadeOut.Completed += (s, e) => StickyHeaderOverlay.Visibility = Visibility.Collapsed;
                StickyHeaderOverlay.BeginAnimation(OpacityProperty, fadeOut);
            }
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
                    break;
                }
            }
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

