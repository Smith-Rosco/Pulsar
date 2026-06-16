// [Path]: Pulsar/Pulsar/Services/Tutorial/TriggerHandlers/PageNavigatedTriggerHandler.cs

using System;
using System.ComponentModel;
using Pulsar.Features.Tutorial.Models;
using Pulsar.ViewModels;

namespace Pulsar.Features.Tutorial.Services.TriggerHandlers
{
    /// <summary>
    /// 页面导航触发器处理器
    /// 监听 SettingsViewModel 的页面导航事件
    /// </summary>
    public class PageNavigatedTriggerHandler : ITriggerHandler
    {
        private readonly SettingsViewModel _settingsViewModel;
        private PropertyChangedEventHandler? _propertyChangedHandler;

        public PageNavigatedTriggerHandler(SettingsViewModel settingsViewModel)
        {
            _settingsViewModel = settingsViewModel;
        }

        public void Setup(TutorialTrigger trigger, Action onTriggered)
        {
            _propertyChangedHandler = (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.CurrentView))
                {
                    // trigger.TargetValue should be "Slots" or "Settings"
                    if (_settingsViewModel.CurrentView == trigger.TargetValue)
                    {
                        onTriggered();
                    }
                }
            };

            _settingsViewModel.PropertyChanged += _propertyChangedHandler;
            
            // 检查初始状态：如果已经在目标页面，立即触发
            if (_settingsViewModel.CurrentView == trigger.TargetValue)
            {
                onTriggered();
            }
        }

        public void Cleanup()
        {
            if (_propertyChangedHandler != null)
            {
                _settingsViewModel.PropertyChanged -= _propertyChangedHandler;
            }

            _propertyChangedHandler = null;
        }
    }
}
