using FluentAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Pulsar.Core.Messages;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Features.Tutorial.Services.TriggerHandlers;

namespace Pulsar.Tests.Tutorial
{
    public class ActionExecutedTriggerHandlerTests : IDisposable
    {
        public void Dispose()
        {
            WeakReferenceMessenger.Default.Reset();
        }

        [Fact]
        public void Setup_ShouldTrigger_WhenMatchingSuccessfulActionIsPublished()
        {
            var handler = new ActionExecutedTriggerHandler();
            int triggerCount = 0;

            handler.Setup(new TutorialTrigger
            {
                Type = TutorialTriggerType.ActionExecuted,
                TargetValue = "Switch"
            }, () => triggerCount++);

            WeakReferenceMessenger.Default.Send(new ActionExecutionMessage(
                TutorialActionKind.Switch,
                "com.pulsar.winswitcher",
                "switch",
                success: true));

            triggerCount.Should().Be(1);
            handler.Cleanup();
        }

        [Fact]
        public void Setup_ShouldIgnore_NonMatchingOrFailedActions()
        {
            var handler = new ActionExecutedTriggerHandler();
            int triggerCount = 0;

            handler.Setup(new TutorialTrigger
            {
                Type = TutorialTriggerType.ActionExecuted,
                TargetValue = "Command"
            }, () => triggerCount++);

            WeakReferenceMessenger.Default.Send(new ActionExecutionMessage(
                TutorialActionKind.Switch,
                "com.pulsar.winswitcher",
                "switch",
                success: true));

            WeakReferenceMessenger.Default.Send(new ActionExecutionMessage(
                TutorialActionKind.Command,
                "com.pulsar.command",
                "run",
                success: false));

            triggerCount.Should().Be(0);
            handler.Cleanup();
        }
    }
}
