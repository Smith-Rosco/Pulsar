using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Strategies;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    public class WindowSwitchStrategyTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldBeginExecutionBeforeAttemptingActivation()
        {
            var windowService = new Mock<IWindowService>();
            var order = new List<string>();
            var slot = new SlotViewModel(1, 0, 0, 40);

            windowService
                .Setup(service => service.ActivateWindow(It.IsAny<ProcessWindowInfo>()))
                .Callback(() => order.Add("activate"))
                .Returns(true);

            var strategy = new WindowSwitchStrategy(CreateWindow(), windowService.Object);
            var context = new Mock<IMenuSession>();
            context.SetupProperty(c => c.IsVisible, true);
            context
                .Setup(c => c.BeginExecution(It.IsAny<SlotViewModel>()))
                .Callback(() => order.Add("begin"));

            await strategy.ExecuteAsync(slot, context.Object);

            // The strategy no longer writes IsVisible itself. It declares intent
            // through the seam, and the seam owns the mark→hide order — so this
            // test no longer has to restate an invariant it does not own.
            order.Should().Equal("begin", "activate");
            context.Verify(c => c.BeginExecution(slot), Times.Once);
            context.VerifySet(c => c.IsVisible = false, Times.Never);
            windowService.Verify(service => service.ActivateWindow(It.IsAny<ProcessWindowInfo>()), Times.Once);
        }

        private static ProcessWindowInfo CreateWindow()
        {
            return new ProcessWindowInfo
            {
                Handle = new IntPtr(42),
                ProcessName = "testapp",
                Title = "Test Window"
            };
        }
    }
}
