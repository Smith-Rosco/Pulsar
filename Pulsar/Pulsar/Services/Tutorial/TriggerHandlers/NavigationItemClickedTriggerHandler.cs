// [Path]: Pulsar/Pulsar/Services/Tutorial/TriggerHandlers/NavigationItemClickedTriggerHandler.cs

using System;
using Pulsar.Models.Tutorial;
using Wpf.Ui.Controls;

namespace Pulsar.Services.Tutorial.TriggerHandlers
{
    /// <summary>
    /// 导航项点击触发器处理器
    /// 监听 NavigationView 的 SelectionChanged 事件，确保用户真正点击了导航项
    /// </summary>
    public class NavigationItemClickedTriggerHandler : ITriggerHandler
    {
        private readonly NavigationView _navigationView;
        private Action? _onTriggered;
        private string? _targetTag;
        private bool _hasTriggered = false;

        public NavigationItemClickedTriggerHandler(NavigationView navigationView)
        {
            _navigationView = navigationView ?? throw new ArgumentNullException(nameof(navigationView));
        }

        public void Setup(TutorialTrigger trigger, Action onTriggered)
        {
            _onTriggered = onTriggered;
            _targetTag = trigger.TargetValue; // e.g., "Slots"
            _hasTriggered = false;

            _navigationView.SelectionChanged += OnNavigationSelectionChanged;
        }

        private void OnNavigationSelectionChanged(NavigationView sender, System.Windows.RoutedEventArgs args)
        {
            if (_hasTriggered)
            {
                return; // 防止重复触发
            }

            if (sender.SelectedItem is NavigationViewItem item)
            {
                var itemTag = item.Tag?.ToString();

                if (itemTag == _targetTag)
                {
                    _hasTriggered = true;
                    _onTriggered?.Invoke();
                }
            }
        }

        public void Cleanup()
        {
            if (_navigationView != null)
            {
                _navigationView.SelectionChanged -= OnNavigationSelectionChanged;
            }

            _onTriggered = null;
            _targetTag = null;
            _hasTriggered = false;
        }
    }
}
