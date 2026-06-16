// [Path]: Pulsar/Pulsar/Services/Tutorial/TriggerHandlers/RadialMenuShownTriggerHandler.cs

using System;
using System.ComponentModel;
using Pulsar.Features.Tutorial.Models;
using Pulsar.ViewModels;

namespace Pulsar.Features.Tutorial.Services.TriggerHandlers
{
    /// <summary>
    /// 轮盘菜单显示触发器处理器
    /// 监听 RadialMenuViewModel 的可见性变化
    /// </summary>
    public class RadialMenuShownTriggerHandler : ITriggerHandler
    {
        private readonly RadialMenuViewModel _radialMenuViewModel;
        private PropertyChangedEventHandler? _propertyChangedHandler;
        private Action? _onTriggered;

        public RadialMenuShownTriggerHandler(RadialMenuViewModel radialMenuViewModel)
        {
            _radialMenuViewModel = radialMenuViewModel;
        }

        public void Setup(TutorialTrigger trigger, Action onTriggered)
        {
            _onTriggered = onTriggered;

            _propertyChangedHandler = (s, e) =>
            {
                if (e.PropertyName == nameof(RadialMenuViewModel.IsVisible) 
                    && _radialMenuViewModel.IsVisible)
                {
                    var expectedMode = trigger.TargetValue;
                    
                    // If no specific mode is required, trigger immediately
                    if (string.IsNullOrEmpty(expectedMode))
                    {
                        _onTriggered?.Invoke();
                        return;
                    }

                    // Check if current mode matches expected mode
                    var currentMode = _radialMenuViewModel.CurrentMode.ToString();
                    if (currentMode == expectedMode)
                    {
                        _onTriggered?.Invoke();
                    }
                }
            };

            _radialMenuViewModel.PropertyChanged += _propertyChangedHandler;
        }

        public void Cleanup()
        {
            if (_propertyChangedHandler != null)
            {
                _radialMenuViewModel.PropertyChanged -= _propertyChangedHandler;
            }

            _propertyChangedHandler = null;
            _onTriggered = null;
        }
    }
}
