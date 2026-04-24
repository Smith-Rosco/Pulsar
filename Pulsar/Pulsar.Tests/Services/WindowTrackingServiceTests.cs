using System;
using FluentAssertions;
using Pulsar.Services.WindowSwitching;

namespace Pulsar.Tests.Services
{
    public class WindowTrackingServiceTests
    {
        [Fact]
        public void SnapshotWindow_ShouldNotPromoteActivationTime_WithoutExplicitActivation()
        {
            var trackingService = new WindowTrackingService();
            IntPtr firstWindow = new(11);
            IntPtr secondWindow = new(22);

            var firstActivation = trackingService.RegisterOrUpdateWindow(firstWindow);
            System.Threading.Thread.Sleep(20);
            var secondActivation = trackingService.RegisterOrUpdateWindow(secondWindow);
            System.Threading.Thread.Sleep(20);

            var firstSnapshot = trackingService.SnapshotWindow(firstWindow);
            var secondSnapshot = trackingService.SnapshotWindow(secondWindow);

            firstSnapshot.ActivationTime.Should().Be(firstActivation.ActivationTime);
            secondSnapshot.ActivationTime.Should().Be(secondActivation.ActivationTime);
            secondSnapshot.ActivationTime.Should().BeAfter(firstSnapshot.ActivationTime);
        }
    }
}
