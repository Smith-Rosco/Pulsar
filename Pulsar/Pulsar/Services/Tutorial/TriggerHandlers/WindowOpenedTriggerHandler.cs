// [Path]: Pulsar/Pulsar/Services/Tutorial/TriggerHandlers/WindowOpenedTriggerHandler.cs

using System;
using System.Windows;
using System.Windows.Threading;
using Pulsar.Models.Tutorial;

namespace Pulsar.Services.Tutorial.TriggerHandlers
{
    /// <summary>
    /// 窗口打开触发器处理器
    /// 监听特定窗口的打开事件
    /// </summary>
    public class WindowOpenedTriggerHandler : ITriggerHandler
    {
        private Action? _onTriggered;
        private DispatcherTimer? _pollTimer;
        private string? _targetWindowType;
        private bool _hasTriggered;

        public void Setup(TutorialTrigger trigger, Action onTriggered)
        {
            _onTriggered = onTriggered;
            _targetWindowType = trigger.TargetValue;
            _hasTriggered = false;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // First check current state.
                if (IsWindowOpen(_targetWindowType))
                {
                    _hasTriggered = true;
                    _onTriggered?.Invoke();
                    return;
                }

                // Poll for a short interval. This avoids RegisterClassHandler which can't be unregistered.
                _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(250)
                };

                _pollTimer.Tick += (s, e) =>
                {
                    if (_hasTriggered)
                    {
                        return;
                    }

                    if (IsWindowOpen(_targetWindowType))
                    {
                        _hasTriggered = true;
                        _pollTimer?.Stop();
                        _onTriggered?.Invoke();
                    }
                };

                _pollTimer.Start();
            });
        }

        private static bool IsWindowOpen(string? windowTypeName)
        {
            if (string.IsNullOrWhiteSpace(windowTypeName))
            {
                return false;
            }

            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window.IsVisible && window.GetType().Name == windowTypeName)
                {
                    return true;
                }
            }

            return false;
        }

        public void Cleanup()
        {
            _onTriggered = null;
            _hasTriggered = true;
            _targetWindowType = null;

            try
            {
                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            _pollTimer?.Stop();
                        }
                        catch
                        {
                        }

                        _pollTimer = null;
                    });
                }
                else
                {
                    _pollTimer = null;
                }
            }
            catch
            {
                _pollTimer = null;
            }
        }
    }
}
