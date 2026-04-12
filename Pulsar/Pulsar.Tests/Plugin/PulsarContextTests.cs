using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;
using Xunit;

namespace Pulsar.Tests.Plugin
{
    public class PulsarContextTests
    {
        [Fact]
        public async Task GetTargetExePathAsync_ShouldReturnEmpty_WhenProcessLookupFails()
        {
            var windowService = new Mock<IWindowService>();
            windowService.Setup(x => x.GetPreviousWindow()).Returns(IntPtr.Zero);
            windowService.Setup(x => x.GetProcessWindowsAsync(It.IsAny<int>()))
                .ReturnsAsync(new List<ProcessWindowInfo>());

            var context = PulsarContext.Capture(windowService.Object);

            var exePath = await context.GetTargetExePathAsync();

            exePath.Should().BeEmpty();
            context.TargetExePath.Should().BeEmpty();
        }

        [Fact]
        public async Task GetTargetExePathAsync_ShouldCacheResolvedValue()
        {
            var context = PulsarContextFactory.CreateTestContext();

            var first = await context.GetTargetExePathAsync();
            var second = await context.GetTargetExePathAsync();

            second.Should().Be(first);
            context.TargetExePath.Should().Be(first);
        }
    }
}
