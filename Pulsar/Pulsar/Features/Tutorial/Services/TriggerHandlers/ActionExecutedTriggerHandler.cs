using System;
using CommunityToolkit.Mvvm.Messaging;
using Pulsar.Core.Messages;
using Pulsar.Features.Tutorial.Models;

namespace Pulsar.Features.Tutorial.Services.TriggerHandlers
{
    public sealed class ActionExecutedTriggerHandler : ITriggerHandler
    {
        private Action? _onTriggered;
        private TutorialActionKind? _expectedKind;
        private bool _isRegistered;

        public void Setup(TutorialTrigger trigger, Action onTriggered)
        {
            _onTriggered = onTriggered;

            if (Enum.TryParse<TutorialActionKind>(trigger.TargetValue, true, out TutorialActionKind expectedKind))
            {
                _expectedKind = expectedKind;
            }
            else
            {
                _expectedKind = null;
            }

            WeakReferenceMessenger.Default.Register<ActionExecutionMessage>(this, OnActionExecuted);
            _isRegistered = true;
        }

        private void OnActionExecuted(object recipient, ActionExecutionMessage message)
        {
            if (!message.Success)
            {
                return;
            }

            if (_expectedKind.HasValue && message.Kind != _expectedKind.Value)
            {
                return;
            }

            _onTriggered?.Invoke();
        }

        public void Cleanup()
        {
            if (_isRegistered)
            {
                WeakReferenceMessenger.Default.Unregister<ActionExecutionMessage>(this);
                _isRegistered = false;
            }

            _expectedKind = null;
            _onTriggered = null;
        }
    }
}
