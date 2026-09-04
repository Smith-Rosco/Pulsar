using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Moq;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Services.ActionFeedback;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    public class PluginActionStrategyTests
    {
        [Fact]
        public async Task ExecuteAsync_WinSwitcherSwitch_NotRunningSlot_ShouldPublishSwitchKind()
        {
            // [Regression] First trigger of a not-running winSwitcher "switch" slot goes
            // through PluginActionStrategy (launch fallback). It must publish
            // TutorialActionKind.Switch so the tutorial's step2 advances immediately,
            // not only on the second (running) trigger.
            var executor = new Mock<IPluginExecutor>();
            executor
                .Setup(r => r.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<PulsarContext>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(PluginResult.Ok());

            var slot = new PluginSlot
            {
                Slot = 1,
                PluginId = "com.pulsar.winswitcher",
                Action = "switch",
                Args = new Dictionary<string, string> { ["app"] = "notepad", ["path"] = "notepad.exe" }
            };

            var strategy = new PluginActionStrategy(
                slot,
                executor.Object,
                Pulsar.Tests.TestHelpers.PulsarContextFactory.CreateTestContext(),
                Mock.Of<ITrayService>(),
                Mock.Of<IActionFeedbackService>(),
                feedbackPresenter: null);

            TutorialActionKind? observedKind = null;
            var receiver = new object();
            WeakReferenceMessenger.Default.Register<ActionExecutionMessage>(receiver, (r, m) => observedKind = m.Kind);

            try
            {
                var context = new Mock<IMenuSession>();
                context.SetupProperty(c => c.IsVisible, true);

                await strategy.ExecuteAsync(new SlotViewModel(1, 0, 0, 40), context.Object);

                observedKind.Should().Be(TutorialActionKind.Switch, "a winSwitcher switch from a not-running slot must advance the switch tutorial step");
            }
            finally
            {
                WeakReferenceMessenger.Default.Unregister<ActionExecutionMessage>(receiver);
            }
        }

        [Fact]
        public async Task ExecuteAsync_CommandPlugin_ShouldPublishCommandKind()
        {
            var executor = new Mock<IPluginExecutor>();
            executor
                .Setup(r => r.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<PulsarContext>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(PluginResult.Ok());

            var slot = new PluginSlot
            {
                Slot = 1,
                PluginId = "com.pulsar.command",
                Action = "sendkeys",
                Args = new Dictionary<string, string> { ["keys"] = "Hello" }
            };

            var strategy = new PluginActionStrategy(
                slot,
                executor.Object,
                Pulsar.Tests.TestHelpers.PulsarContextFactory.CreateTestContext(),
                Mock.Of<ITrayService>(),
                Mock.Of<IActionFeedbackService>(),
                feedbackPresenter: null);

            TutorialActionKind? observedKind = null;
            var receiver = new object();
            WeakReferenceMessenger.Default.Register<ActionExecutionMessage>(receiver, (r, m) => observedKind = m.Kind);

            try
            {
                var context = new Mock<IMenuSession>();
                context.SetupProperty(c => c.IsVisible, true);

                await strategy.ExecuteAsync(new SlotViewModel(1, 0, 0, 40), context.Object);

                observedKind.Should().Be(TutorialActionKind.Command, "command plugin actions must publish Command kind");
            }
            finally
            {
                WeakReferenceMessenger.Default.Unregister<ActionExecutionMessage>(receiver);
            }
        }
    }
}
