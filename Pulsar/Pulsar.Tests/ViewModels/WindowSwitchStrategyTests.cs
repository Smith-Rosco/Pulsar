using System;
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
        public async Task ExecuteAsync_ShouldHideMenuBeforeAttemptingActivation()
        {
            var windowService = new Mock<IWindowService>();
            IMenuSession? observedContext = null;

            windowService
                .Setup(service => service.ActivateWindow(It.IsAny<ProcessWindowInfo>()))
                .Callback(() => observedContext!.IsVisible.Should().BeFalse())
                .Returns(true);

            var strategy = new WindowSwitchStrategy(CreateWindow(), windowService.Object);
            var context = new Mock<IMenuSession>();
            context.SetupProperty(c => c.IsVisible, true);
            observedContext = context.Object;

            await strategy.ExecuteAsync(new SlotViewModel(1, 0, 0, 40), context.Object);

            context.Object.IsVisible.Should().BeFalse();
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
